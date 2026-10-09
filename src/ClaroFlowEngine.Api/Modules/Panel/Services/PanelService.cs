using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using ClaroFlowEngine.Api.Modules.Panel.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Modules.Panel.Services;

/// <summary>
/// Endpoints do painel do atendente e da Central operacional do gestor (FASE 4.2) — jornadas ativas, alertas
/// de inatividade, busca da fila e métricas operacionais — e ações de fechamento/escalação (FASE 3.5).
/// </summary>
public class PanelService : IPanelService
{
    private static readonly TimeSpan MetricsWindow = TimeSpan.FromDays(30);
    private const int MaxDescriptionLength = 500;
    private const int CpfDigitsLength = 11;
    private const int MinNumericSearchDigits = 3;
    private const int MinNameSearchLength = 2;

    private readonly CfeDbContext _db;
    private readonly ITransitionRecorder _transitionRecorder;
    private readonly ICurrentChannelAccessor _currentChannel;
    private readonly ICurrentPanelUserAccessor _currentPanelUser;
    private readonly IJourneyExpirationService _expirationService;
    private readonly CfeOptions _cfeOptions;

    public PanelService(
        CfeDbContext db, ITransitionRecorder transitionRecorder, ICurrentChannelAccessor currentChannel,
        ICurrentPanelUserAccessor currentPanelUser, IJourneyExpirationService expirationService, IOptions<CfeOptions> cfeOptions)
    {
        _db = db;
        _transitionRecorder = transitionRecorder;
        _currentChannel = currentChannel;
        _currentPanelUser = currentPanelUser;
        _expirationService = expirationService;
        _cfeOptions = cfeOptions.Value;
    }

    public async Task<ActiveJourneysResponse> GetActiveJourneysAsync(bool includeEscalated, CancellationToken cancellationToken)
    {
        // Padrão do MVP (A.8): só 'open'. include_escalated=true é opcional e não altera a regra de alerta:
        // jornadas escaladas continuam fora da fila de inatividade.
        var journeys = await GetOperationalJourneysAsync(includeEscalated, DateTime.UtcNow, null, null, cancellationToken);
        return new ActiveJourneysResponse(journeys.Select(ToActiveJourneyDto).ToList(), journeys.Count);
    }

    public async Task<ActiveJourneysResponse> SearchActiveJourneysAsync(string query, CancellationToken cancellationToken)
    {
        var term = (query ?? string.Empty).Trim();
        var isNumeric = IsNumericQuery(term);
        var digitCount = term.Count(char.IsDigit);

        if (isNumeric ? digitCount < MinNumericSearchDigits : term.Length < MinNameSearchLength)
            throw new ValidationException("invalid_query",
                $"Informe pelo menos {MinNumericSearchDigits} dígitos ou {MinNameSearchLength} letras para buscar.");

        var journeys = await GetOperationalJourneysAsync(false, DateTime.UtcNow, null, term, cancellationToken);
        return new ActiveJourneysResponse(journeys.Select(ToActiveJourneyDto).ToList(), journeys.Count);
    }

    public async Task<ActiveAlertsResponse> GetActiveAlertsAsync(CancellationToken cancellationToken)
    {
        EnsurePanelChannel();

        var now = DateTime.UtcNow;
        var attentionCutoff = now.AddMinutes(-_cfeOptions.JourneyAttentionThresholdMinutes);
        var journeys = await GetOperationalJourneysAsync(false, now, attentionCutoff, null, cancellationToken);

        var alerts = journeys
            .Where(j => j.RequiresAttention)
            .OrderBy(j => JourneyAlertLevel.RankOf(j.AlertLevel))
            .ThenByDescending(j => j.MinutesSinceLastActivity)
            .ThenBy(j => j.LastActivityAt)
            .Select(ToActiveAlertDto)
            .ToList();

        return new ActiveAlertsResponse(
            alerts,
            alerts.Count,
            alerts.Count(a => a.AlertLevel == JourneyAlertLevel.Warning),
            alerts.Count(a => a.AlertLevel == JourneyAlertLevel.Critical),
            _cfeOptions.JourneyAttentionThresholdMinutes,
            _cfeOptions.JourneyCriticalThresholdMinutes);
    }

    public async Task<MetricsSummaryResponse> GetMetricsSummaryAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var windowStart = now - MetricsWindow;

        // "Hoje" é o dia civil de Brasília (FASE 4.2, opção P7), não o dia UTC: a meia-noite local vira UTC
        // explicitamente, porque DateOnly.ToDateTime produz Kind=Unspecified, que o Npgsql rejeita para timestamptz.
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(_cfeOptions.BusinessTimeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, timeZone));
        var todayStartUtc = LocalMidnightToUtc(todayLocal, timeZone);
        var tomorrowStartUtc = LocalMidnightToUtc(todayLocal.AddDays(1), timeZone);

        var tmaMedianSeconds = await ComputeTmaMedianSecondsAsync(windowStart, cancellationToken);

        var journeysToday = await _db.JourneyContexts
            .AsNoTracking()
            .CountAsync(j => j.CreatedAt >= todayStartUtc && j.CreatedAt < tomorrowStartUtc, cancellationToken);

        var closedInWindow = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.CreatedAt >= windowStart
                && (j.Status == JourneyStatus.Concluded || j.Status == JourneyStatus.Abandoned || j.Status == JourneyStatus.Expired))
            .Select(j => j.Status)
            .ToListAsync(cancellationToken);

        int? conclusionRatePercent = closedInWindow.Count == 0
            ? null
            : (int)Math.Round(closedInWindow.Count(s => s == JourneyStatus.Concluded) * 100.0 / closedInWindow.Count);

        var mostUsedChannel = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.CreatedAt >= windowStart)
            .GroupBy(j => j.OriginChannel)
            .Select(g => new { Channel = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        var topChannel = mostUsedChannel
            .OrderByDescending(g => g.Total)
            .ThenBy(g => g.Channel, StringComparer.Ordinal)
            .Select(g => g.Channel)
            .FirstOrDefault();

        return new MetricsSummaryResponse(
            tmaMedianSeconds,
            PanelLabels.TmaLabel(tmaMedianSeconds),
            journeysToday,
            conclusionRatePercent,
            topChannel,
            topChannel is null ? null : PanelLabels.Channel(topChannel),
            new MetricsPeriodDto(windowStart, now, (int)MetricsWindow.TotalDays));
    }

    /// <summary>
    /// Mediana calculada em memória (dataset pequeno no protótipo) — EF Core/Npgsql não traduz
    /// PERCENTILE_CONT para LINQ, e uma FromSqlRaw só pra isso não compensa a complexidade aqui.
    /// </summary>
    private async Task<long?> ComputeTmaMedianSecondsAsync(DateTime windowStart, CancellationToken cancellationToken)
    {
        var durations = await _db.JourneyContexts
            .AsNoTracking()
            .Where(j => j.Status == JourneyStatus.Concluded && j.ClosedAt != null && j.ClosedAt >= windowStart)
            .Select(j => (long)(j.ClosedAt!.Value - j.CreatedAt).TotalSeconds)
            .ToListAsync(cancellationToken);

        if (durations.Count == 0) return null;

        durations.Sort();
        var mid = durations.Count / 2;
        return durations.Count % 2 == 0
            ? (durations[mid - 1] + durations[mid]) / 2
            : durations[mid];
    }

    public async Task<ConcludeJourneyResponse> ConcludeJourneyAsync(
        Guid journeyId, ConcludeJourneyRequest request, CancellationToken cancellationToken)
    {
        EnsurePanelChannel();

        if (!ResolutionCategory.IsValid(request.ResolutionCategory))
            throw new ValidationException("invalid_resolution_category", "resolution_category inválida.");
        ValidateDescription(request.Description);

        var journey = await GetOpenJourneyOrThrowAsync(journeyId, cancellationToken);

        var closedAt = DateTime.UtcNow;
        journey.Status = JourneyStatus.Concluded;
        journey.ClosedAt = closedAt;
        journey.UpdatedAt = closedAt;
        journey.Payload["resolution_category"] = request.ResolutionCategory;
        if (!string.IsNullOrWhiteSpace(request.Description))
            journey.Payload["resolution_description"] = request.Description;

        var label = ResolutionCategory.Label(request.ResolutionCategory);
        _transitionRecorder.Record(journey.Id, Channels.Panel, TransitionEventTypes.JourneyConcludedByAgent,
            $"Jornada concluída pelo atendente — {label}",
            PanelUserMetadata.Merge(
                new { resolution_category = request.ResolutionCategory, description = request.Description },
                _currentPanelUser));

        await _db.SaveChangesAsync(cancellationToken);

        return new ConcludeJourneyResponse(journey.Id, journey.Status, closedAt, request.ResolutionCategory, label);
    }

    public async Task<EscalateJourneyResponse> EscalateJourneyAsync(
        Guid journeyId, EscalateJourneyRequest request, CancellationToken cancellationToken)
    {
        EnsurePanelChannel();

        if (!EscalationArea.IsValid(request.EscalationArea))
            throw new ValidationException("invalid_escalation_area", "escalation_area inválida.");
        ValidateDescription(request.Description);

        var journey = await GetOpenJourneyOrThrowAsync(journeyId, cancellationToken);

        var escalatedAt = DateTime.UtcNow;
        journey.Status = JourneyStatus.Escalated;
        journey.UpdatedAt = escalatedAt;
        journey.EscalatedAt = escalatedAt;
        // Sem ClosedAt de propósito (A.5): a jornada não fechou, só foi transferida — continua "viva" no CFE.
        journey.Payload["escalation_area"] = request.EscalationArea;
        if (!string.IsNullOrWhiteSpace(request.Description))
            journey.Payload["escalation_description"] = request.Description;

        var label = EscalationArea.Label(request.EscalationArea);
        _transitionRecorder.Record(journey.Id, Channels.Panel, TransitionEventTypes.JourneyEscalated,
            $"Jornada escalada para {label}",
            PanelUserMetadata.Merge(
                new { escalation_area = request.EscalationArea, description = request.Description },
                _currentPanelUser));

        await _db.SaveChangesAsync(cancellationToken);

        return new EscalateJourneyResponse(journey.Id, journey.Status, escalatedAt, request.EscalationArea, label);
    }

    /// <summary>
    /// Lista de jornadas abertas com canal atual, inatividade e classificação de alerta. Usada pela fila,
    /// pelos alertas e pela busca. Antes de classificar, aplica a expiração reativa, para não mostrar como alerta
    /// uma jornada que já venceu.
    /// </summary>
    private async Task<List<OperationalJourney>> GetOperationalJourneysAsync(
        bool includeEscalated, DateTime now, DateTime? lastActivityCutoff, string? searchTerm, CancellationToken cancellationToken)
    {
        await ExpireInactiveJourneysAsync(cancellationToken);

        IQueryable<JourneyContext> journeysQuery = _db.JourneyContexts.AsNoTracking();

        if (lastActivityCutoff is not null)
        {
            var cutoff = lastActivityCutoff.Value;
            journeysQuery = journeysQuery.Where(j => j.Status == JourneyStatus.Open && j.UpdatedAt <= cutoff);
        }
        else
        {
            journeysQuery = journeysQuery.Where(
                j => j.Status == JourneyStatus.Open || (includeEscalated && j.Status == JourneyStatus.Escalated));
        }

        if (searchTerm is not null)
            journeysQuery = ApplyCustomerSearch(journeysQuery, searchTerm);

        var journeySnapshots = await journeysQuery
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new
            {
                j.Id,
                j.CustomerId,
                CustomerFullName = j.Customer.FullName,
                CustomerCpf = j.Customer.Cpf,
                CustomerAnonymizedAt = j.Customer.AnonymizedAt,
                j.Intent,
                j.CurrentStep,
                j.OriginChannel,
                j.CreatedAt,
                j.UpdatedAt,
                j.Status,
            })
            .ToListAsync(cancellationToken);

        if (journeySnapshots.Count == 0)
            return [];

        var journeyIds = journeySnapshots.Select(j => j.Id).ToList();
        var customerIds = journeySnapshots.Select(j => j.CustomerId).Distinct().ToList();

        // Canal atual = canal da transição operacional mais recente. panel_accessed não entra na lista,
        // então abrir o painel não muda o canal da jornada.
        var operationalActivityTypes = TransitionEventTypes.OperationalActivityTypes;
        var operationalTransitions = await _db.JourneyTransitions
            .AsNoTracking()
            .Where(t => t.JourneyContextId != null
                && journeyIds.Contains(t.JourneyContextId.Value)
                && operationalActivityTypes.Contains(t.EventType))
            .Select(t => new { t.JourneyContextId, t.Channel, t.OccurredAt })
            .ToListAsync(cancellationToken);

        var currentChannelById = operationalTransitions
            .GroupBy(t => t.JourneyContextId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.OccurredAt).First().Channel);

        // Telefone do WhatsApp mais recente de cada cliente, para a busca e para a fila. Cliente anonimizado fica de fora.
        var customerPhones = await _db.IdentityLinks
            .AsNoTracking()
            .Where(link => customerIds.Contains(link.CustomerId)
                && link.Channel == Channels.Whatsapp
                && link.Customer.AnonymizedAt == null)
            .Select(link => new { link.CustomerId, link.Identifier, link.CreatedAt })
            .ToListAsync(cancellationToken);

        var phoneByCustomer = customerPhones
            .GroupBy(link => link.CustomerId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(link => link.CreatedAt).First().Identifier);

        return journeySnapshots.Select(j =>
        {
            var currentChannel = currentChannelById.GetValueOrDefault(j.Id, j.OriginChannel);
            var minutesSinceLastActivity = ElapsedWholeMinutes(now, j.UpdatedAt);
            var classification = ClassifyAlert(j.Status, minutesSinceLastActivity);

            return new OperationalJourney(
                j.Id,
                new ActiveJourneyCustomerDto(
                    j.CustomerId,
                    j.CustomerFullName,
                    CpfMasking.Mask(j.CustomerCpf),
                    j.CustomerAnonymizedAt is not null ? "Removido (LGPD)" : null,
                    PhoneMasking.Mask(phoneByCustomer.GetValueOrDefault(j.CustomerId))),
                j.Intent,
                j.CurrentStep,
                j.OriginChannel,
                currentChannel,
                j.CreatedAt,
                j.UpdatedAt,
                j.UpdatedAt,
                ElapsedWholeMinutes(now, j.CreatedAt),
                minutesSinceLastActivity,
                classification.RequiresAttention,
                classification.AlertLevel,
                j.Status);
        }).ToList();
    }

    /// <summary>
    /// Mesma expiração reativa usada por Context e Handoff (IJourneyExpirationService), aplicada em lote. O FOR UPDATE
    /// serializa as duas consultas que o painel dispara quase ao mesmo tempo, evitando duas transições journey_expired
    /// para a mesma jornada.
    /// </summary>
    private async Task ExpireInactiveJourneysAsync(CancellationToken cancellationToken)
    {
        var expirationCutoff = DateTime.UtcNow.AddHours(-_cfeOptions.JourneyInactivityTtlHours);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        // A interpolação é parametrizada pelo EF Core/Npgsql; não há concatenação de SQL.
        var candidates = await _db.JourneyContexts
            .FromSqlInterpolated($"SELECT * FROM journey_contexts WHERE status = {JourneyStatus.Open} AND updated_at < {expirationCutoff} FOR UPDATE")
            .ToListAsync(cancellationToken);

        var expiredAny = false;
        foreach (var journey in candidates)
            expiredAny |= _expirationService.TryExpireIfInactive(journey);

        if (expiredAny)
            await _db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private AlertClassification ClassifyAlert(string status, int minutesSinceLastActivity)
    {
        if (status != JourneyStatus.Open)
            return new AlertClassification(false, JourneyAlertLevel.Normal);

        if (minutesSinceLastActivity >= _cfeOptions.JourneyCriticalThresholdMinutes)
            return new AlertClassification(true, JourneyAlertLevel.Critical);

        if (minutesSinceLastActivity >= _cfeOptions.JourneyAttentionThresholdMinutes)
            return new AlertClassification(true, JourneyAlertLevel.Warning);

        return new AlertClassification(false, JourneyAlertLevel.Normal);
    }

    /// <summary>
    /// Com dígitos (e separadores comuns de telefone/CPF), filtra por CPF exato de 11 dígitos ou por telefone parcial.
    /// Com letras, filtra por nome parcial. CPF de cliente anonimizado nunca casa, porque o CPF dele virou hash.
    /// </summary>
    private static IQueryable<JourneyContext> ApplyCustomerSearch(IQueryable<JourneyContext> journeys, string term)
    {
        if (IsNumericQuery(term))
        {
            var digits = new string(term.Where(char.IsDigit).ToArray());
            var isCpf = digits.Length == CpfDigitsLength;

            return journeys.Where(j =>
                (isCpf && j.Customer.AnonymizedAt == null && j.Customer.Cpf == digits)
                || j.Customer.IdentityLinks.Any(l => l.Channel == Channels.Whatsapp && l.Identifier.Contains(digits)));
        }

        var lowered = term.ToLowerInvariant();
        return journeys.Where(j => j.Customer.FullName.ToLower().Contains(lowered));
    }

    private static bool IsNumericQuery(string term) =>
        term.Any(char.IsDigit)
        && term.All(c => char.IsDigit(c) || char.IsWhiteSpace(c) || c is '(' or ')' or '-' or '+' or '.');

    private static DateTime LocalMidnightToUtc(DateOnly date, TimeZoneInfo timeZone) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), timeZone);

    private static int ElapsedWholeMinutes(DateTime now, DateTime timestamp) =>
        (int)Math.Min(int.MaxValue, Math.Max(0, Math.Floor((now - timestamp).TotalMinutes)));

    private static ActiveJourneyDto ToActiveJourneyDto(OperationalJourney journey) => new(
        journey.Id,
        journey.Customer,
        journey.Intent,
        PanelLabels.Intent(journey.Intent),
        journey.CurrentStep,
        PanelLabels.CurrentStep(journey.CurrentStep),
        journey.OriginChannel,
        PanelLabels.Channel(journey.OriginChannel),
        journey.CurrentChannel,
        PanelLabels.Channel(journey.CurrentChannel),
        journey.CreatedAt,
        journey.UpdatedAt,
        journey.LastActivityAt,
        journey.MinutesSinceStart,
        journey.MinutesSinceLastActivity,
        journey.RequiresAttention,
        journey.AlertLevel,
        journey.Status);

    private static ActiveAlertDto ToActiveAlertDto(OperationalJourney journey) => new(
        journey.Id,
        journey.Customer,
        journey.Intent,
        PanelLabels.Intent(journey.Intent),
        journey.CurrentStep,
        PanelLabels.CurrentStep(journey.CurrentStep),
        journey.OriginChannel,
        PanelLabels.Channel(journey.OriginChannel),
        journey.CurrentChannel,
        PanelLabels.Channel(journey.CurrentChannel),
        journey.CreatedAt,
        journey.UpdatedAt,
        journey.LastActivityAt,
        journey.MinutesSinceStart,
        journey.MinutesSinceLastActivity,
        journey.AlertLevel);

    private void EnsurePanelChannel()
    {
        if (_currentChannel.Channel != Channels.Panel)
            throw new ForbiddenException("channel_not_allowed", "Esta ação só pode ser executada pelo painel do atendente.");
    }

    private static void ValidateDescription(string? description)
    {
        if (description is { Length: > MaxDescriptionLength })
            throw new ValidationException("invalid_description", $"description deve ter no máximo {MaxDescriptionLength} caracteres.");
    }

    private async Task<JourneyContext> GetOpenJourneyOrThrowAsync(Guid journeyId, CancellationToken cancellationToken)
    {
        var journey = await _db.JourneyContexts.FirstOrDefaultAsync(j => j.Id == journeyId, cancellationToken)
            ?? throw new NotFoundException("journey_not_found", $"Jornada {journeyId} não encontrada.");

        if (journey.Status != JourneyStatus.Open)
            throw new ConflictException("journey_not_open", $"Jornada está no estado '{journey.Status}'.", new { status = journey.Status });

        return journey;
    }

    private readonly record struct AlertClassification(bool RequiresAttention, string AlertLevel);

    private sealed record OperationalJourney(
        Guid Id,
        ActiveJourneyCustomerDto Customer,
        string Intent,
        string CurrentStep,
        string OriginChannel,
        string CurrentChannel,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime LastActivityAt,
        int MinutesSinceStart,
        int MinutesSinceLastActivity,
        bool RequiresAttention,
        string AlertLevel,
        string Status);
}
