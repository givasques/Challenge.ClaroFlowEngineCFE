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
    int MinutesSinceStart);

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
