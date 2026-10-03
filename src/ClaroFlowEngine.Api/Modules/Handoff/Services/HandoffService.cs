using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.Handoff.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ClaroFlowEngine.Api.Modules.Handoff.Services;

public class HandoffService : IHandoffService
{
    // Deep link só faz sentido para canais com front-end web simulado (App/WhatsApp).
    // "call" é um canal válido de jornada, mas não tem UI para abrir um link.
    private static readonly string[] DeepLinkTargetChannels = [Channels.App, Channels.Whatsapp];

    private readonly CfeDbContext _db;
    private readonly ITransitionRecorder _transitionRecorder;
    private readonly IJourneyExpirationService _expirationService;
    private readonly CfeOptions _cfeOptions;
    private readonly ChannelsOptions _channelsOptions;
    private readonly ILogger<HandoffService> _logger;

    public HandoffService(
        CfeDbContext db,
        ITransitionRecorder transitionRecorder,
        IJourneyExpirationService expirationService,
        IOptions<CfeOptions> cfeOptions,
        IOptions<ChannelsOptions> channelsOptions,
        ILogger<HandoffService> logger)
    {
        _db = db;
        _transitionRecorder = transitionRecorder;
        _expirationService = expirationService;
        _cfeOptions = cfeOptions.Value;
        _channelsOptions = channelsOptions.Value;
        _logger = logger;
    }

    public async Task<GenerateHandoffResponse> GenerateAsync(GenerateHandoffRequest request, CancellationToken cancellationToken)
    {
        if (!DeepLinkTargetChannels.Contains(request.TargetChannel))
            throw new ValidationException("invalid_target_channel", $"Canal de destino '{request.TargetChannel}' não é suportado para handoff.");

        var journey = await _db.JourneyContexts.FirstOrDefaultAsync(j => j.Id == request.JourneyContextId, cancellationToken)
            ?? throw new NotFoundException("journey_not_found", $"Jornada {request.JourneyContextId} não encontrada.");

        if (_expirationService.TryExpireIfInactive(journey))
            await _db.SaveChangesAsync(cancellationToken);

        if (journey.Status != JourneyStatus.Open)
            throw new ConflictException("journey_not_open",
                $"Jornada está no estado '{journey.Status}' e não pode gerar handoff.", new { status = journey.Status });

        var token = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(_cfeOptions.HandoffTokenTtlMinutes);

        _db.HandoffTokens.Add(new HandoffToken
        {
            JourneyContextId = journey.Id,
            Token = token,
            TargetChannel = request.TargetChannel,
            ExpiresAt = expiresAt,
        });

        _transitionRecorder.Record(journey.Id, request.TargetChannel, TransitionEventTypes.DeepLinkGenerated,
            $"Deep link gerado com validade de {_cfeOptions.HandoffTokenTtlMinutes} min.",
            new { token_expires_at = expiresAt, target_channel = request.TargetChannel });

        await _db.SaveChangesAsync(cancellationToken);

        var baseUrl = request.TargetChannel switch
        {
            Channels.App => _channelsOptions.AppSimBaseUrl,
            Channels.Whatsapp => _channelsOptions.WhatsappSimBaseUrl,
            _ => throw new InvalidOperationException("Canal já validado, branch inalcançável."),
        };

        _logger.LogInformation(
            "Handoff token generated for journey {JourneyId} targeting {TargetChannel}", journey.Id, request.TargetChannel);

        return new GenerateHandoffResponse(token, request.TargetChannel, $"{baseUrl}/?token={token}", expiresAt);
    }

    public async Task<ResolveTokenResponse> ResolveTokenAsync(string token, string? identifier, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ValidationException("invalid_token", "token é obrigatório.");

        var handoffToken = await _db.HandoffTokens
            .Include(t => t.JourneyContext).ThenInclude(j => j.Customer)
            .FirstOrDefaultAsync(t => t.Token == token, cancellationToken)
            ?? throw new NotFoundException("token_not_found", "Token de handoff não encontrado.");

        if (handoffToken.UsedAt is not null)
        {
            // Distingue "já usado normalmente" de "revogado por tentativas com conta errada" (FASE 4.3, item B.4) —
            // o contador nunca é zerado, então atingir o limite aqui significa que foi este o motivo da revogação.
            if (handoffToken.OwnerMismatchAttempts >= _cfeOptions.HandoffMaxOwnerMismatchAttempts)
                throw new GoneException("token_revoked",
                    "Este link foi cancelado por segurança após várias tentativas com a conta incorreta. Inicie um novo atendimento.");

            throw new GoneException("token_already_used", "Este link já foi utilizado.");
        }

        if (handoffToken.ExpiresAt < DateTime.UtcNow)
            throw new GoneException("token_expired", "Este link expirou.");

        var journey = handoffToken.JourneyContext;

        // A jornada pode ter expirado por inatividade entre a geração do link e a tentativa de uso.
        if (_expirationService.TryExpireIfInactive(journey))
            await _db.SaveChangesAsync(cancellationToken);

        if (journey.Status == JourneyStatus.Expired)
            throw new GoneException("journey_expired", "A jornada associada a este link expirou por inatividade.");

        if (journey.Status is JourneyStatus.Concluded or JourneyStatus.Abandoned)
            throw new GoneException("journey_closed", "A jornada associada a este link já foi encerrada.");

        // A partir daqui, journey.Status == Open.

        // FASE 4.3, item B.3: identifier passa a ser obrigatório — é a conta do App (ou o telefone do
        // WhatsApp) que acabou de "logar" no canal de destino, usada para confirmar que quem abriu o
        // link é o dono da jornada. As checagens de token/jornada acima continuam primeiro e na mesma
        // ordem; só depois delas, e antes de marcar o token como usado, entra a checagem de dono.
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ValidationException("missing_identifier", "identifier é obrigatório.");

        // O handoff nunca cria vínculo novo (B.3) — o vínculo já existe previamente (seed, no protótipo;
        // cadastro real da Claro, numa implantação real). Sem vínculo ou vínculo de outro cliente, a
        // conta não é reconhecida como dona desta jornada; nos dois casos o token NÃO é marcado como usado.
        var link = await _db.IdentityLinks.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Channel == handoffToken.TargetChannel && l.Identifier == identifier, cancellationToken);

        if (link is null)
        {
            await RegisterOwnerMismatchAsync(handoffToken, journey, "app_account_not_recognized",
                "Não reconhecemos esta conta do App. Entre com a conta vinculada ao seu cadastro Claro.", cancellationToken);
        }

        if (link!.CustomerId != journey.CustomerId)
        {
            await RegisterOwnerMismatchAsync(handoffToken, journey, "handoff_customer_mismatch",
                "Este link pertence a outra conta. Entre com a conta que iniciou o atendimento.", cancellationToken);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        handoffToken.UsedAt = DateTime.UtcNow;

        _transitionRecorder.Record(journey.Id, handoffToken.TargetChannel, TransitionEventTypes.JourneyResumed,
            "Cliente abriu o deep link. Contexto recuperado pelo CFE.", new { token = handoffToken.Token });

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Handoff token resolved for journey {JourneyId} on {Channel}", journey.Id, handoffToken.TargetChannel);

        var planDetails = await BuildPlanDetailsAsync(journey, cancellationToken);
        var invoiceDetails = await BuildInvoiceDetailsAsync(journey, cancellationToken);

        return new ResolveTokenResponse(
            UnifiedCustomerId: journey.CustomerId,
            JourneyContext: new ResolvedJourneyDto(journey.Id, journey.Intent, journey.CurrentStep, journey.Payload, journey.Status),
            Customer: new HandoffCustomerDto(
                journey.Customer.FullName,
                CpfMasking.Mask(journey.Customer.Cpf),
                journey.Customer.AnonymizedAt is not null ? "Removido (LGPD)" : null),
            PlanDetails: planDetails,
            InvoiceDetails: invoiceDetails);
    }

    /// <summary>
    /// Registra a tentativa bloqueada (FASE 4.3, item B.4) e, ao atingir o limite, revoga o token
    /// (marca como usado e registra o cancelamento). Sempre lança: 403 (motivo da recusa recebido)
    /// ou, ao atingir o limite, 410 token_revoked — o chamador nunca segue adiante depois de chamar isto.
    /// </summary>
    private async Task RegisterOwnerMismatchAsync(
        HandoffToken handoffToken, JourneyContext journey, string mismatchErrorCode, string mismatchMessage, CancellationToken cancellationToken)
    {
        handoffToken.OwnerMismatchAttempts++;

        _transitionRecorder.Record(journey.Id, handoffToken.TargetChannel, TransitionEventTypes.HandoffOwnerMismatch,
            "Tentativa de abrir o link de continuação com outra conta foi bloqueada.",
            new { attempt = handoffToken.OwnerMismatchAttempts });

        if (handoffToken.OwnerMismatchAttempts >= _cfeOptions.HandoffMaxOwnerMismatchAttempts)
        {
            handoffToken.UsedAt = DateTime.UtcNow;
            _transitionRecorder.Record(journey.Id, handoffToken.TargetChannel, TransitionEventTypes.HandoffTokenRevoked,
                "Link de continuação cancelado após múltiplas tentativas com outra conta.", null);

            _logger.LogWarning(
                "Handoff token revoked for journey {JourneyId} after {Attempts} owner mismatch attempts",
                journey.Id, handoffToken.OwnerMismatchAttempts);

            await _db.SaveChangesAsync(cancellationToken);
            throw new GoneException("token_revoked",
                "Este link foi cancelado por segurança após várias tentativas com a conta incorreta. Inicie um novo atendimento.");
        }

        await _db.SaveChangesAsync(cancellationToken);
        throw new ForbiddenException(mismatchErrorCode, mismatchMessage);
    }

    public async Task<PlansResponse> GetActivePlansAsync(CancellationToken cancellationToken)
    {
        var plans = await _db.Plans
            .AsNoTracking()
            .Where(p => p.Active)
            .OrderBy(p => p.DataGb)
            .Select(p => new PlanSummaryDto(p.Code, p.Name, p.DataGb, p.MonthlyPriceCents))
            .ToListAsync(cancellationToken);

        return new PlansResponse(plans);
    }

    private async Task<PlanDetailsDto?> BuildPlanDetailsAsync(JourneyContext journey, CancellationToken cancellationToken)
    {
        var currentPlan = await _db.CustomerPlans
            .Where(cp => cp.CustomerId == journey.CustomerId && cp.Active)
            .Select(cp => new PlanInfoDto(cp.Plan.Code, cp.Plan.Name, cp.Plan.MonthlyPriceCents))
            .FirstOrDefaultAsync(cancellationToken);

        PlanInfoDto? selectedPlan = null;
        var selectedCode = ExtractSelectedPlanCode(journey.Payload);
        if (!string.IsNullOrWhiteSpace(selectedCode))
        {
            selectedPlan = await _db.Plans
                .Where(p => p.Code == selectedCode)
                .Select(p => new PlanInfoDto(p.Code, p.Name, p.MonthlyPriceCents))
                .FirstOrDefaultAsync(cancellationToken);
        }

        return currentPlan is null && selectedPlan is null ? null : new PlanDetailsDto(currentPlan, selectedPlan);
    }

    /// <summary>Embute a fatura selecionada quando `payload.invoice_id` existir (ETAPA 2, Passo C, item 6.5).</summary>
    private async Task<InvoiceDetailsDto?> BuildInvoiceDetailsAsync(JourneyContext journey, CancellationToken cancellationToken)
    {
        var invoiceIdRaw = ExtractPayloadString(journey.Payload, "invoice_id");
        if (invoiceIdRaw is null || !Guid.TryParse(invoiceIdRaw, out var invoiceId)) return null;

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken);

        if (invoice is null) return null;

        var items = invoice.Items
            .OrderBy(it => it.Sequence)
            .Select(it => new InvoiceItemInfoDto(it.Id, it.Sequence, it.Description, it.Category, it.AmountCents))
            .ToList();

        return new InvoiceDetailsDto(
            invoice.Id, invoice.ReferenceMonth, InvoiceFormatting.ReferenceLabel(invoice.ReferenceMonth),
            invoice.DueDate, invoice.TotalCents, invoice.Status, items);
    }

    // Valores no payload (coluna JSONB) chegam como JsonElement quando desserializados para Dictionary<string, object>.
    private static string? ExtractSelectedPlanCode(Dictionary<string, object> payload) => ExtractPayloadString(payload, "selected_plan_code");

    private static string? ExtractPayloadString(Dictionary<string, object> payload, string key)
    {
        if (!payload.TryGetValue(key, out var raw)) return null;

        return raw switch
        {
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString(),
            string s => s,
            _ => raw?.ToString(),
        };
    }
}
