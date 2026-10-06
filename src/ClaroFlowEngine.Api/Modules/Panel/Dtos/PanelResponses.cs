namespace ClaroFlowEngine.Api.Modules.Panel.Dtos;

/// <summary>Resposta de GET /journeys/active e POST /journeys/active/search — jornadas abertas, mais recentes primeiro.</summary>
public record ActiveJourneysResponse(List<ActiveJourneyDto> Journeys, int Total);

public record ActiveJourneyDto(
    Guid Id,
    ActiveJourneyCustomerDto Customer,
    string Intent,
    string IntentLabel,
    string CurrentStep,
    string CurrentStepLabel,
    string OriginChannel,
    string OriginChannelLabel,
    string CurrentChannel,
    string CurrentChannelLabel,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    int MinutesSinceStart,
    int MinutesSinceLastActivity,
    bool RequiresAttention,
    string AlertLevel,
    // Sempre 'open' hoje; só varia quando ?include_escalated=true é usado (FASE 3.5, item A.8).
    string Status);

/// <summary>
/// Cliente exibido nas telas do painel. O CPF nunca vai completo: só a forma mascarada e o rótulo de LGPD (FASE 4.3).
/// O telefone é o identificador do canal WhatsApp, usado na busca da fila (FASE 4.2).
/// </summary>
public record ActiveJourneyCustomerDto(Guid Id, string FullName, string? CpfMasked, string? CpfLabel = null, string? Phone = null);

/// <summary>Resposta de GET /alerts/active — somente jornadas abertas acima do limite de atenção (FASE 4.2).</summary>
public record ActiveAlertsResponse(
    List<ActiveAlertDto> Alerts,
    int Total,
    int Warning,
    int Critical,
    int AttentionThresholdMinutes,
    int CriticalThresholdMinutes);

public record ActiveAlertDto(
    Guid JourneyId,
    ActiveJourneyCustomerDto Customer,
    string Intent,
    string IntentLabel,
    string CurrentStep,
    string CurrentStepLabel,
    string OriginChannel,
    string OriginChannelLabel,
    string CurrentChannel,
    string CurrentChannelLabel,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime LastActivityAt,
    int MinutesSinceStart,
    int MinutesSinceLastActivity,
    string AlertLevel);

/// <summary>
/// Requisição de POST /journeys/active/search. Nome: busca parcial. Telefone: busca parcial pelos dígitos.
/// CPF: só com os 11 dígitos completos. O texto vai no corpo, nunca na URL, para não cair no log de requisições.
/// </summary>
public record SearchActiveJourneysRequest(string? Query);

/// <summary>Resposta de GET /metrics/summary — indicadores agregados dos últimos 30 dias (exceto "jornadas hoje").</summary>
public record MetricsSummaryResponse(
    long? TmaMedianSeconds,
    string TmaMedianLabel,
    int JourneysToday,
    int? ConclusionRatePercent,
    string? MostUsedChannel,
    string? MostUsedChannelLabel,
    MetricsPeriodDto Period);

public record MetricsPeriodDto(DateTime From, DateTime To, int WindowDays);

/// <summary>Requisição de POST /journeys/{id}/conclude (FASE 3.5).</summary>
public record ConcludeJourneyRequest(string ResolutionCategory, string? Description);

/// <summary>Resposta de POST /journeys/{id}/conclude.</summary>
public record ConcludeJourneyResponse(
    Guid JourneyId, string Status, DateTime ClosedAt, string ResolutionCategory, string ResolutionCategoryLabel);

/// <summary>Requisição de POST /journeys/{id}/escalate (FASE 3.5).</summary>
public record EscalateJourneyRequest(string EscalationArea, string? Description);

/// <summary>Resposta de POST /journeys/{id}/escalate.</summary>
public record EscalateJourneyResponse(
    Guid JourneyId, string Status, DateTime EscalatedAt, string EscalationArea, string EscalationAreaLabel);
