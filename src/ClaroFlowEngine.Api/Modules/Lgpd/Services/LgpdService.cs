using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Modules.Lgpd.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
    private readonly ICurrentPanelUserAccessor _currentPanelUser;
    private readonly ILogger<LgpdService> _logger;

    public LgpdService(
        CfeDbContext db,
        ITransitionRecorder transitionRecorder,
        ICurrentChannelAccessor currentChannel,
        ICurrentPanelUserAccessor currentPanelUser,
        ILogger<LgpdService> logger)
    {
        _db = db;
        _transitionRecorder = transitionRecorder;
        _currentChannel = currentChannel;
        _currentPanelUser = currentPanelUser;
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
            ?? throw new NotFoundException("customer_not_found", $"Cliente com CPF {CpfMasking.Mask(sanitizedCpf)} não encontrado.");

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

        // Resumos do cliente gerados por IA (FASE 4.4, A.7): texto derivado de dado pessoal, apagado junto.
        var aiSummaries = await _db.CustomerAiSummaries
            .Where(s => s.CustomerId == customerId)
            .ToListAsync(cancellationToken);
        _db.CustomerAiSummaries.RemoveRange(aiSummaries);

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
            metadata: PanelUserMetadata.Merge(
                new
                {
                    trigger = actingChannel,
                    operations_performed = new[] { "customer_record", "identity_links", "journey_payloads", "handoff_tokens", "customer_ai_summaries" },
                },
                _currentPanelUser));

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

    public async Task<CpfRevealResponse> RevealCpfAsync(Guid customerId, string reason, CancellationToken cancellationToken)
    {
        if (!CpfRevealReason.IsValid(reason))
            throw new ValidationException("invalid_reveal_reason", "Motivo da revelação inválido ou ausente.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", "Cliente não encontrado.");

        if (customer.AnonymizedAt is not null)
            throw new ConflictException("already_anonymized", $"Customer already anonymized at {customer.AnonymizedAt:O}");

        var revealedAt = DateTime.UtcNow;

        // Transição órfã (sem journey_context_id), mesmo padrão de data_anonymization_requested — o
        // CPF nunca entra no metadata nem na descrição, só o motivo (FASE 4.3, item A.4).
        _transitionRecorder.Record(
            journeyContextId: null,
            channel: Channels.Panel,
            eventType: TransitionEventTypes.CustomerCpfRevealed,
            description: "Atendente revelou o CPF completo do cliente.",
            metadata: PanelUserMetadata.Merge(new { reason }, _currentPanelUser));

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "CPF revealed for customer {CustomerId} by panel user {PanelUserId}, reason {Reason}",
            customerId, _currentPanelUser.UserId, reason);

        return new CpfRevealResponse(customer.Cpf, revealedAt);
    }

    // Cópia local e enxuta, mesmo critério já usado em outras DTOs do projeto (evita acoplar o módulo
    // Lgpd a Panel/Identity só para 2-3 rótulos) — espelha PanelLabels.IntentLabels/ChannelLabels.
    private static readonly Dictionary<string, string> IntentLabels = new()
    {
        ["change_plan"] = "Troca de plano",
        ["dispute_charge"] = "Contestação de cobrança",
    };

    private static readonly Dictionary<string, string> ChannelLabels = new()
    {
        [Channels.Whatsapp] = "WhatsApp",
        [Channels.App] = "App Minha Claro",
        [Channels.Call] = "Central telefônica",
        [Channels.Cpf] = "CPF",
    };

    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public async Task<(byte[] JsonBytes, string FileName)> ExportDataAsync(DataExportRequest request, CancellationToken cancellationToken)
    {
        var actingChannel = _currentChannel.Channel;
        if (actingChannel != Channels.App && actingChannel != Channels.Panel)
            throw new ForbiddenException("channel_not_allowed", "Este canal não pode exportar dados do cliente.");

        Guid customerId;
        if (actingChannel == Channels.Panel)
        {
            if (request.CustomerId is null || request.AppAccount is not null)
                throw new ValidationException("invalid_export_request", "Para o painel, envie apenas customer_id.");
            customerId = request.CustomerId.Value;
        }
        else
        {
            if (request.AppAccount is null || request.CustomerId is not null)
                throw new ValidationException("invalid_export_request", "Para o App, envie apenas app_account.");

            var link = await _db.IdentityLinks.AsNoTracking()
                .FirstOrDefaultAsync(l => l.Channel == Channels.App && l.Identifier == request.AppAccount, cancellationToken)
                ?? throw new NotFoundException("customer_not_found", "Cliente não encontrado.");
            customerId = link.CustomerId;
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", "Cliente não encontrado.");

        if (customer.AnonymizedAt is not null)
            throw new ConflictException("already_anonymized", $"Customer already anonymized at {customer.AnonymizedAt:O}");

        var identityLinks = await _db.IdentityLinks.AsNoTracking()
            .Where(l => l.CustomerId == customerId).OrderBy(l => l.CreatedAt).ToListAsync(cancellationToken);

        var journeys = await _db.JourneyContexts.AsNoTracking()
            .Where(j => j.CustomerId == customerId).OrderBy(j => j.CreatedAt).ToListAsync(cancellationToken);
        var journeyIds = journeys.Select(j => j.Id).ToList();

        var transitionsByJourney = (await _db.JourneyTransitions.AsNoTracking()
                .Where(t => t.JourneyContextId != null && journeyIds.Contains(t.JourneyContextId.Value))
                .OrderBy(t => t.OccurredAt)
                .ToListAsync(cancellationToken))
            .GroupBy(t => t.JourneyContextId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var customerPlans = await _db.CustomerPlans.AsNoTracking().Include(cp => cp.Plan)
            .Where(cp => cp.CustomerId == customerId).OrderBy(cp => cp.StartedAt).ToListAsync(cancellationToken);

        var invoices = await _db.Invoices.AsNoTracking().Include(i => i.Items)
            .Where(i => i.CustomerId == customerId).OrderBy(i => i.ReferenceMonth).ToListAsync(cancellationToken);

        var opportunities = await _db.Opportunities.AsNoTracking()
            .Where(o => o.CustomerId == customerId).OrderBy(o => o.DetectedAt).ToListAsync(cancellationToken);

        var export = new CustomerDataExport(
            ExportFormatVersion: "1.0",
            GeneratedAt: DateTime.UtcNow,
            LegalBasis: "LGPD, Art. 18, V (portabilidade dos dados)",
            Controller: "Claro Flow Engine (ambiente acadêmico de demonstração)",
            Customer: new CustomerExportDto(customer.Id, customer.FullName, customer.Cpf, customer.CreatedAt),
            ChannelIdentities: identityLinks
                .Select(l => new ChannelIdentityExportDto(l.Channel, ChannelLabels.GetValueOrDefault(l.Channel, l.Channel), l.Identifier, l.CreatedAt))
                .ToList(),
            Journeys: journeys
                .Select(j => new JourneyExportDto(
                    j.Id,
                    j.Intent,
                    IntentLabels.GetValueOrDefault(j.Intent, j.Intent),
                    j.Status,
                    JourneyStatus.Label(j.Status),
                    j.OriginChannel,
                    j.CreatedAt,
                    j.ClosedAt,
                    j.Payload,
                    transitionsByJourney.GetValueOrDefault(j.Id, [])
                        .Select(t => new TransitionHistoryExportDto(t.EventType, t.Description, t.Channel, t.OccurredAt))
                        .ToList()))
                .ToList(),
            CustomerPlans: customerPlans
                .Select(cp => new CustomerPlanExportDto(cp.Plan.Code, cp.Plan.Name, cp.Plan.MonthlyPriceCents, cp.Active, cp.StartedAt))
                .ToList(),
            Invoices: invoices
                .Select(i => new InvoiceExportDto(
                    i.ReferenceMonth, InvoiceFormatting.ReferenceLabel(i.ReferenceMonth), i.DueDate, i.TotalCents, i.Status,
                    i.Items.OrderBy(it => it.Sequence)
                        .Select(it => new InvoiceItemExportDto(it.Description, it.Category, it.AmountCents))
                        .ToList()))
                .ToList(),
            CommercialOpportunities: opportunities
                .Select(o => new OpportunityExportDto(OpportunityCategory.Label(o.Category), OpportunityStatus.Label(o.Status), o.DetectedAt))
                .ToList());

        var json = JsonSerializer.SerializeToUtf8Bytes(export, ExportJsonOptions);

        // Transição órfã (sem journey_context_id), mesmo padrão de data_anonymization_requested — sem
        // dado pessoal na descrição; se for o painel, o usuário entra no metadata (FASE 4.3, item C.3).
        _transitionRecorder.Record(
            journeyContextId: null,
            channel: actingChannel,
            eventType: TransitionEventTypes.DataPortabilityRequested,
            description: "Exportação de dados solicitada (portabilidade, Art. 18, V da LGPD)",
            metadata: PanelUserMetadata.Merge(null, _currentPanelUser));

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Data export generated for customer {CustomerId} via {Channel} by panel user {PanelUserId}, {Bytes} bytes",
            customerId, actingChannel, _currentPanelUser.UserId, json.Length);

        var fileName = $"dados-cliente-{customerId.ToString()[..8]}-{DateTime.UtcNow:yyyy-MM-dd}.json";
        return (json, fileName);
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
