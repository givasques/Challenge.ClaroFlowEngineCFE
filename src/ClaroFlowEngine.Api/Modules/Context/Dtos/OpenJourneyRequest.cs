namespace ClaroFlowEngine.Api.Modules.Context.Dtos;

/// <summary>Payload para abrir uma nova jornada (UC01).</summary>
public record OpenJourneyRequest(
    Guid CustomerId,
    string OriginChannel,
    string Intent,
    string InitialStep,
    Dictionary<string, object>? Payload = null);
