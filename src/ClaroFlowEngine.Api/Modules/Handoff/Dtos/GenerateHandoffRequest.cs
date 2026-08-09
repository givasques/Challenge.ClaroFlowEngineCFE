namespace ClaroFlowEngine.Api.Modules.Handoff.Dtos;

/// <summary>Payload para gerar um deep link de handoff (UC05).</summary>
public record GenerateHandoffRequest(Guid JourneyContextId, string TargetChannel);
