using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Modules.Lgpd.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ClaroFlowEngine.Api.Modules.Lgpd.Services;

/// <summary>
/// Direito ao esquecimento (Art. 18 LGPD, FASE 3.4) — anonimiza dados pessoais identificáveis do
/// cliente mantendo o histórico operacional (jornadas, transições, faturas) íntegro para auditoria.
/// </summary>
public class LgpdService : ILgpdService
{
    // Chaves de payload de jornada que podem carregar dado pessoal (nenhuma é gravada pelo fluxo atual
    // do bot hoje, exceto customer_description — removidas mesmo assim, defensivamente, caso um fluxo
    // futuro passe a persistir nome/telefone/CPF ali).
    private static readonly string[] PersonalPayloadKeys = ["customer_name", "cpf", "phone", "customer_description"];

    private readonly CfeDbContext _db;
    private readonly ITransitionRecorder _transitionRecorder;
    private readonly ICurrentChannelAccessor _currentChannel;
    private readonly ILogger<LgpdService> _logger;

    public LgpdService(
        CfeDbContext db,
        ITransitionRecorder transitionRecorder,
        ICurrentChannelAccessor currentChannel,
        ILogger<LgpdService> logger)
    {
        _db = db;
        _transitionRecorder = transitionRecorder;
        _currentChannel = currentChannel;
        _logger = logger;
    }

    public async Task<RightToBeForgottenResponse> ExerciseRightToBeForgottenAsync(string cpf, CancellationToken cancellationToken)
    {
        if (!IdentifierFormat.IsValidCpf(cpf))
            throw new ValidationException("invalid_cpf", "CPF inválido — verifique os dígitos digitados.");

        var actingChannel = _currentChannel.Channel;
        if (actingChannel != Channels.App && actingChannel != Channels.Panel)
        {
            throw new ForbiddenException(
                "channel_not_allowed",
                "Este canal não pode exercer o direito ao esquecimento em nome do cliente.");
        }

        var sanitizedCpf = IdentifierFormat.SanitizeCpf(cpf);

        // Depois de anonimizado, customers.cpf guarda o hash, não o CPF em texto puro — então a busca
        // precisa considerar as duas formas (CPF ativo OU hash de um CPF já anonimizado) para que uma
        // segunda chamada com o CPF original ainda encontre o cliente e caia no 409 de idempotência,
        // em vez de um 404 (o hash é determinístico, então recalculá-lo aqui é seguro).
        var hashedCpf = Sha256Hex(sanitizedCpf);
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Cpf == sanitizedCpf || c.Cpf == hashedCpf, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Cliente com CPF {sanitizedCpf} não encontrado.");

        if (customer.AnonymizedAt is not null)
        {
            throw new ConflictException(
                "already_anonymized",
                $"Customer already anonymized at {customer.AnonymizedAt:O}");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var customerId = customer.Id;
        var anonymizedAt = DateTime.UtcNow;

        // customers: nome/CPF anonimizados; hash (não reversível) preserva a capacidade de detectar
        // duplicidade em auditoria sem expor o CPF original.
        customer.FullName = "Cliente Removido";
        customer.Cpf = Sha256Hex(sanitizedCpf);
        customer.AnonymizedAt = anonymizedAt;
        customer.AnonymizationSource = actingChannel;

        // identity_links: identifier hasheado, mas channel e customer_id preservados (necessários pra auditoria).
        var identityLinks = await _db.IdentityLinks
            .Where(l => l.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        foreach (var link in identityLinks)
            link.Identifier = Sha256Hex(link.Identifier);

        // journey_contexts.payload: remove só as chaves com dado pessoal; campos operacionais (plano
        // selecionado, fatura, motivo de contestação, protocolo) continuam intactos para estatística.
        var journeys = await _db.JourneyContexts
            .Where(j => j.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        foreach (var journey in journeys)
            foreach (var key in PersonalPayloadKeys)
                journey.Payload.Remove(key);

        var journeyIds = journeys.Select(j => j.Id).ToList();

        // handoff_tokens: qualquer token ainda ativo do cliente é invalidado, para não sobreviver à anonimização.
        var activeTokens = await _db.HandoffTokens
            .Where(t => journeyIds.Contains(t.JourneyContextId) && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
            token.UsedAt = anonymizedAt;

        // journey_transitions não é alterada (é o próprio log auditável) — só contada para o response.
        var transitionsCount = await _db.JourneyTransitions
            .CountAsync(t => t.JourneyContextId != null && journeyIds.Contains(t.JourneyContextId.Value), cancellationToken);

        // Transição de auditoria da própria operação — órfã (sem journey_context_id), pois o direito ao
        // esquecimento é exercido pelo cliente como um todo, não por uma jornada específica.
        _transitionRecorder.Record(
            journeyContextId: null,
            channel: actingChannel,
            eventType: TransitionEventTypes.DataAnonymizationRequested,
            description: "Direito ao esquecimento exercido — dados pessoais anonimizados conforme Art. 18 LGPD",
            metadata: new
            {
                trigger = actingChannel,
                operations_performed = new[] { "customer_record", "identity_links", "journey_payloads", "handoff_tokens" },
            });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Right to be forgotten exercised for customer {CustomerId} via {Channel}", customerId, actingChannel);

        return new RightToBeForgottenResponse(
            "anonymized",
            customerId,
            anonymizedAt,
            new AnonymizationOperationsDto(
                CustomerRecord: "anonymized",
                IdentityLinks: "hashed",
                JourneyPayloads: "cleaned",
                JourneysPreserved: journeys.Count,
                TransitionsPreserved: transitionsCount));
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
