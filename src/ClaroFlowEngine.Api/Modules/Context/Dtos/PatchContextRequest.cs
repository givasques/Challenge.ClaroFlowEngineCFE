namespace ClaroFlowEngine.Api.Modules.Context.Dtos;

/// <summary>Payload para atualizar etapa e/ou dados coletados de uma jornada aberta (UC04).</summary>
public record PatchContextRequest(
    string? CurrentStep = null,
    Dictionary<string, object>? PayloadMerge = null);
