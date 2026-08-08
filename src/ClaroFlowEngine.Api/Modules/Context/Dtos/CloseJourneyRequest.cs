namespace ClaroFlowEngine.Api.Modules.Context.Dtos;

/// <summary>Payload para encerrar uma jornada (UC07). Outcome: "concluded" ou "abandoned".</summary>
public record CloseJourneyRequest(
    string Outcome,
    string Channel,
    string? Reason = null);
