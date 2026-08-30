using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.Context.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Modules.Context.Services;

public class ContextService : IContextService
{
    private readonly CfeDbContext _db;
    private readonly ITransitionRecorder _transitionRecorder;
    private readonly IJourneyExpirationService _expirationService;
    private readonly ICurrentChannelAccessor _currentChannel;
    private readonly CfeOptions _cfeOptions;
    private readonly ILogger<ContextService> _logger;

    public ContextService(
        CfeDbContext db,
        ITransitionRecorder transitionRecorder,
        IJourneyExpirationService expirationService,
        ICurrentChannelAccessor currentChannel,
        IOptions<CfeOptions> cfeOptions,
        ILogger<ContextService> logger)
    {
        _db = db;
        _transitionRecorder = transitionRecorder;
        _expirationService = expirationService;
        _currentChannel = currentChannel;
        _cfeOptions = cfeOptions.Value;
        _logger = logger;
    }

    public async Task<(JourneySummaryResponse Response, bool WasCreated)> OpenAsync(
        OpenJourneyRequest request, CancellationToken cancellationToken)
    {
        ValidateOpenRequest(request);

        var customerExists = await _db.Customers.AsNoTracking().AnyAsync(c => c.Id == request.CustomerId, cancellationToken);
        if (!customerExists)
            throw new NotFoundException("customer_not_found", $"Cliente {request.CustomerId} não encontrado.");

        // Idempotência (spec-funcional §6.6 / spec-tecnica §5.3): só uma jornada open por cliente+intent.
        var existing = await _db.JourneyContexts
            .FirstOrDefaultAsync(j => j.CustomerId == request.CustomerId
                && j.Intent == request.Intent && j.Status == JourneyStatus.Open, cancellationToken);

        if (existing is not null)
        {
            var justExpired = _expirationService.TryExpireIfInactive(existing);
            if (!justExpired)
            {
                _transitionRecorder.Record(existing.Id, request.OriginChannel, TransitionEventTypes.JourneyReopenAttempted,
                    "Tentativa de abrir nova jornada com uma já ativa para o mesmo cliente e intenção.",
                    new { attempted_origin_channel = request.OriginChannel });
                await _db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Journey reopen attempted for customer {CustomerId}, returning existing journey", request.CustomerId);
                return (ToSummary(existing), false);
            }

            // A jornada anterior acabou de expirar por inatividade — persiste isso antes de abrir uma nova.
            await _db.SaveChangesAsync(cancellationToken);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var journey = new JourneyContext
        {
            CustomerId = request.CustomerId,
            OriginChannel = request.OriginChannel,
            Intent = request.Intent,
            CurrentStep = request.InitialStep,
            Payload = request.Payload ?? new(),
            Status = JourneyStatus.Open,
        };
        _db.JourneyContexts.Add(journey);
        await _db.SaveChangesAsync(cancellationToken); // precisa do Id gerado para registrar a transição

        _transitionRecorder.Record(journey.Id, request.OriginChannel, TransitionEventTypes.JourneyStarted,
            "Jornada iniciada.", new { intent = request.Intent, initial_step = request.InitialStep });
        await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Journey opened for customer {CustomerId} on {Channel} with intent {Intent}",
            request.CustomerId, request.OriginChannel, request.Intent);

        return (ToSummary(journey), true);
    }

    public async Task<JourneySummaryResponse> UpdateAsync(
        Guid journeyId, PatchContextRequest request, CancellationToken cancellationToken)
    {
        if (request.CurrentStep is null && request.PayloadMerge is null)
            throw new ValidationException("empty_patch", "Informe current_step e/ou payload_merge.");

        if (request.CurrentStep is { Length: > 50 })
            throw new ValidationException("invalid_current_step", "current_step deve ter até 50 caracteres.");

        var journey = await _db.JourneyContexts.FirstOrDefaultAsync(j => j.Id == journeyId, cancellationToken)
            ?? throw new NotFoundException("journey_not_found", $"Jornada {journeyId} não encontrada.");

        await EnsureOpenOrThrowAsync(journey, cancellationToken);

        var previousStep = journey.CurrentStep;

        if (!string.IsNullOrWhiteSpace(request.CurrentStep))
            journey.CurrentStep = request.CurrentStep;

        if (request.PayloadMerge is not null)
        {
            foreach (var (key, value) in request.PayloadMerge)
                journey.Payload[key] = value;
        }

        journey.UpdatedAt = DateTime.UtcNow;

        // Canal real resolvido pelo ChannelAuthMiddleware a partir do X-Channel-Token (Fase 5).
        // Fallback para OriginChannel só em cenário anômalo (ex: chamada de teste sem passar pelo middleware).
        var actingChannel = _currentChannel.Channel ?? journey.OriginChannel;
        _transitionRecorder.Record(journey.Id, actingChannel, TransitionEventTypes.StepUpdated,
            "Etapa e/ou dados da jornada atualizados.",
            new { previous_step = previousStep, current_step = journey.CurrentStep, updated_keys = request.PayloadMerge?.Keys.ToArray() ?? [] });

        await _db.SaveChangesAsync(cancellationToken);

        return ToSummary(journey);
    }

    public async Task<JourneyDetailResponse> GetByIdAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var journey = await _db.JourneyContexts
            .Include(j => j.Customer)
            .FirstOrDefaultAsync(j => j.Id == journeyId, cancellationToken)
            ?? throw new NotFoundException("journey_not_found", $"Jornada {journeyId} não encontrada.");

        if (_expirationService.TryExpireIfInactive(journey))
            await _db.SaveChangesAsync(cancellationToken);

        return await ToDetailAsync(journey, cancellationToken);
    }

    public async Task<ActiveJourneyResponse> GetActiveByCustomerAsync(
        Guid customerId, bool includeHistory, int historyLimit, CancellationToken cancellationToken)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, cancellationToken)
            ?? throw new NotFoundException("customer_not_found", $"Cliente {customerId} não encontrado.");

        var active = await _db.JourneyContexts
            .FirstOrDefaultAsync(j => j.CustomerId == customerId && j.Status == JourneyStatus.Open, cancellationToken);

        if (active is not null && _expirationService.TryExpireIfInactive(active))
        {
            await _db.SaveChangesAsync(cancellationToken);
            active = null; // acabou de expirar — não conta mais como ativa
        }

        List<JourneySummaryResponse>? recentJourneys = null;
        if (includeHistory)
        {
            var excludedId = active?.Id ?? Guid.Empty;
            recentJourneys = await _db.JourneyContexts
                .AsNoTracking()
                .Where(j => j.CustomerId == customerId && j.Status != JourneyStatus.Open && j.Id != excludedId)
                .OrderByDescending(j => j.CreatedAt)
                .Take(historyLimit)
                .Select(j => new JourneySummaryResponse(
                    j.Id, j.CustomerId, j.OriginChannel, j.Intent, j.CurrentStep, j.Payload, j.Status, j.CreatedAt, j.UpdatedAt, j.ClosedAt))
                .ToListAsync(cancellationToken);
        }

        // Sem jornada ativa e sem pedido de histórico: não há nada relevante a mostrar (UC09 A2 cobre o caso com histórico).
        if (active is null && !includeHistory)
            throw new NotFoundException("active_journey_not_found", "Não há jornada ativa para este cliente.");

        // UC09 passo 5: o painel do atendente audita o acesso à jornada. Registrado só aqui (na busca inicial
        // por cliente), não em GetByIdAsync — que também é usado pelo polling do painel a cada poucos segundos;
        // gravar a cada poll inundaria o próprio histórico que o painel exibe.
        // Deduplicado por tempo (ETAPA 2, Passo B, item 5.5): trocar de aba e voltar ao mesmo cliente em
        // menos de PanelAccessDedupMinutes não gera uma nova entrada — só uma consulta real após esse intervalo.
        if (active is not null && _currentChannel.Channel == Channels.Panel)
        {
            var lastPanelAccess = await _db.JourneyTransitions
                .AsNoTracking()
                .Where(t => t.JourneyContextId == active.Id
                    && t.EventType == TransitionEventTypes.PanelAccessed
                    && t.Channel == Channels.Panel)
                .OrderByDescending(t => t.OccurredAt)
                .FirstOrDefaultAsync(cancellationToken);

            var dedupWindow = TimeSpan.FromMinutes(_cfeOptions.PanelAccessDedupMinutes);
            var shouldRecord = lastPanelAccess is null || DateTime.UtcNow - lastPanelAccess.OccurredAt > dedupWindow;

            if (shouldRecord)
            {
                _transitionRecorder.Record(active.Id, Channels.Panel, TransitionEventTypes.PanelAccessed,
                    "Painel do atendente consultou esta jornada.");
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        var customerSummary = await BuildCustomerSummaryAsync(customer, cancellationToken);
        JourneyDetailResponse? journeyDetail = active is null
            ? null
            : new JourneyDetailResponse(
                active.Id, active.CustomerId, customerSummary,
                active.OriginChannel, active.Intent, active.CurrentStep, active.Payload, active.Status,
                active.CreatedAt, active.UpdatedAt, active.ClosedAt);

        return new ActiveJourneyResponse(customerSummary, journeyDetail, recentJourneys);
    }

    public async Task<TransitionsResponse> GetTransitionsAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var exists = await _db.JourneyContexts.AsNoTracking().AnyAsync(j => j.Id == journeyId, cancellationToken);
        if (!exists)
            throw new NotFoundException("journey_not_found", $"Jornada {journeyId} não encontrada.");

        var transitions = await _db.JourneyTransitions
            .AsNoTracking()
            .Where(t => t.JourneyContextId == journeyId)
            .OrderByDescending(t => t.OccurredAt)
            .Select(t => new TransitionDto(t.Id, t.Channel, t.EventType, t.Description, t.Metadata, t.OccurredAt))
            .ToListAsync(cancellationToken);

        return new TransitionsResponse(journeyId, transitions);
    }

    public async Task<JourneySummaryResponse> CloseAsync(
        Guid journeyId, CloseJourneyRequest request, CancellationToken cancellationToken)
    {
        if (request.Outcome is not (JourneyStatus.Concluded or JourneyStatus.Abandoned))
            throw new ValidationException("invalid_outcome", "outcome deve ser 'concluded' ou 'abandoned'.");

        if (!Channels.JourneyChannels.Contains(request.Channel))
            throw new ValidationException("invalid_channel", $"Canal '{request.Channel}' não é suportado.");

        var journey = await _db.JourneyContexts.FirstOrDefaultAsync(j => j.Id == journeyId, cancellationToken)
            ?? throw new NotFoundException("journey_not_found", $"Jornada {journeyId} não encontrada.");

        await EnsureOpenOrThrowAsync(journey, cancellationToken, notOpenErrorCode: "journey_already_closed");

        journey.Status = request.Outcome;
        journey.ClosedAt = DateTime.UtcNow;
        journey.UpdatedAt = DateTime.UtcNow;

        _transitionRecorder.Record(journey.Id, request.Channel, TransitionEventTypes.JourneyClosed,
            $"Jornada encerrada como '{request.Outcome}'.",
            new { outcome = request.Outcome, reason = request.Reason });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Journey {JourneyId} closed with outcome {Outcome}", journey.Id, request.Outcome);

        return ToSummary(journey);
    }

    /// <summary>Aplica a checagem de expiração (UC08, via serviço compartilhado) e garante que a jornada ainda está open; senão, lança 409.</summary>
    private async Task EnsureOpenOrThrowAsync(JourneyContext journey, CancellationToken cancellationToken, string notOpenErrorCode = "journey_not_open")
    {
        if (_expirationService.TryExpireIfInactive(journey))
        {
            await _db.SaveChangesAsync(cancellationToken);
            throw new ConflictException(notOpenErrorCode, "A jornada expirou por inatividade.", new { status = journey.Status });
        }

        if (journey.Status != JourneyStatus.Open)
            throw new ConflictException(notOpenErrorCode, $"Jornada está no estado '{journey.Status}'.", new { status = journey.Status });
    }

    private static void ValidateOpenRequest(OpenJourneyRequest request)
    {
        if (request.CustomerId == Guid.Empty)
            throw new ValidationException("invalid_customer_id", "customer_id é obrigatório.");

        if (!Channels.JourneyChannels.Contains(request.OriginChannel))
            throw new ValidationException("invalid_origin_channel", $"Canal de origem '{request.OriginChannel}' não é suportado.");

        // Intent não é restrito a "change_plan" aqui: o CFE é desenhado para ser genérico (spec-funcional §1);
        // a restrição "só troca de plano disponível" é uma regra de conversa do bot (Fase 6), não do backend.
        if (string.IsNullOrWhiteSpace(request.Intent) || request.Intent.Length > 50)
            throw new ValidationException("invalid_intent", "intent é obrigatório e deve ter até 50 caracteres.");

        if (string.IsNullOrWhiteSpace(request.InitialStep) || request.InitialStep.Length > 50)
            throw new ValidationException("invalid_initial_step", "initial_step é obrigatório e deve ter até 50 caracteres.");
    }

    private static JourneySummaryResponse ToSummary(JourneyContext j) => new(
        j.Id, j.CustomerId, j.OriginChannel, j.Intent, j.CurrentStep, j.Payload, j.Status, j.CreatedAt, j.UpdatedAt, j.ClosedAt);

    /// <summary>Monta o DTO de detalhe, com o cliente enriquecido via <see cref="BuildCustomerSummaryAsync"/>.</summary>
    private async Task<JourneyDetailResponse> ToDetailAsync(JourneyContext j, CancellationToken cancellationToken)
    {
        var customer = await BuildCustomerSummaryAsync(j.Customer, cancellationToken);

        return new JourneyDetailResponse(
            j.Id, j.CustomerId, customer,
            j.OriginChannel, j.Intent, j.CurrentStep, j.Payload, j.Status, j.CreatedAt, j.UpdatedAt, j.ClosedAt);
    }

    /// <summary>
    /// Enriquece o cliente com telefone (via identity_links, canal whatsapp), plano ativo (via customer_plans)
    /// e agregados de jornadas (ETAPA 2, Passo B) — independente de uma jornada específica, para que o bloco
    /// de dados do cliente no painel apareça mesmo sem jornada ativa (FASE 3, item C.2).
    /// </summary>
    private async Task<CustomerSummaryDto> BuildCustomerSummaryAsync(Customer customer, CancellationToken cancellationToken)
    {
        var phone = await _db.IdentityLinks
            .AsNoTracking()
            .Where(l => l.CustomerId == customer.Id && l.Channel == Channels.Whatsapp)
            .Select(l => l.Identifier)
            .FirstOrDefaultAsync(cancellationToken);

        var currentPlan = await _db.CustomerPlans
            .AsNoTracking()
            .Where(cp => cp.CustomerId == customer.Id && cp.Active)
            .Select(cp => new PlanInfoDto(cp.Plan.Code, cp.Plan.Name, cp.Plan.MonthlyPriceCents))
            .FirstOrDefaultAsync(cancellationToken);

        // Dataset pequeno no protótipo, agregado em memória em vez de várias queries GROUP BY separadas.
        var journeyStats = await _db.JourneyContexts
            .AsNoTracking()
            .Where(other => other.CustomerId == customer.Id)
            .Select(other => new { other.OriginChannel, other.Status, other.CreatedAt })
            .ToListAsync(cancellationToken);

        var preferredChannel = journeyStats
            .GroupBy(s => s.OriginChannel)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Max(s => s.CreatedAt))
            .Select(g => g.Key)
            .FirstOrDefault();

        var journeyCounts = new JourneyCountsDto(
            Total: journeyStats.Count,
            Concluded: journeyStats.Count(s => s.Status == JourneyStatus.Concluded),
            Abandoned: journeyStats.Count(s => s.Status == JourneyStatus.Abandoned),
            Expired: journeyStats.Count(s => s.Status == JourneyStatus.Expired));

        return new CustomerSummaryDto(
            customer.Id, customer.FullName, customer.Cpf, phone, currentPlan,
            customer.CreatedAt, preferredChannel, journeyCounts, customer.BillingDueDay, customer.Segment);
    }
}
