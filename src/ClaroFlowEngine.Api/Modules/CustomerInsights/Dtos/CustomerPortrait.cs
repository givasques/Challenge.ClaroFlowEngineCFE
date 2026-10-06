namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;

/// <summary>
/// Retrato minimizado do cliente, o único dado que sai do CFE para o provedor de IA (FASE 4.4, A.3).
/// Lista de permissão: só entram textos já rotulados em português, contagens e tempos relativos.
/// Não há nome, CPF, telefone, conta do App, e-mail, identificadores nem texto livre.
/// </summary>
public sealed record CustomerPortrait(
    PortraitCounts Counts,
    IReadOnlyList<PortraitJourney> Journeys,
    IReadOnlyList<PortraitPlan> Plans,
    IReadOnlyList<PortraitDispute> Disputes,
    IReadOnlyList<PortraitOpportunity> Opportunities);

public sealed record PortraitCounts(int Total, int Open, int Concluded, int Abandoned, int Expired, int Escalated);

public sealed record PortraitJourney(
    string Intent,
    string Status,
    string OriginChannel,
    IReadOnlyList<string> ChannelsVisited,
    string CurrentStep,
    string? Resolution,
    string? EscalationArea,
    string? DisputeReason,
    string StartedAgo,
    int DaysSinceStart,
    string Duration,
    string LastActivityAgo,
    int MinutesSinceLastActivity);

public sealed record PortraitPlan(string Name, string Price, bool Current);

public sealed record PortraitDispute(string Reason, string ReferenceMonth, string Amount, int DaysSinceStart);

public sealed record PortraitOpportunity(string Category, string Urgency, string Status);
