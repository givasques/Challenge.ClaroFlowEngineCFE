namespace ClaroFlowEngine.Api.Modules.Identity.Dtos;

/// <summary>Payload de entrada para resolução/criação de identidade unificada.</summary>
public record ResolveIdentityRequest(
    string Channel,
    string Identifier,
    string? CpfHint = null,
    string? FullNameHint = null);
