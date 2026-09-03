namespace ClaroFlowEngine.Api.Modules.Panel.Dtos;

/// <summary>Resposta de GET /journeys/active — jornadas com status 'open', mais recentes primeiro.</summary>
public record ActiveJourneysResponse(List<ActiveJourneyDto> Journeys, int Total);

public record ActiveJourneyDto(
    Guid Id,
    ActiveJourneyCustomerDto Customer,
    string Intent,
    string IntentLabel,
    string OriginChannel,
    string OriginChannelLabel,
    string CurrentChannel,
    string CurrentChannelLabel,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int MinutesSinceStart,
    // Sempre 'open' hoje; só varia quando ?include_escalated=true é usado (FASE 3.5, item A.8).
    string Status);

public record ActiveJourneyCustomerDto(Guid Id, string FullName, string Cpf);

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
