namespace ClaroFlowEngine.Api.Modules.Identity.Dtos;

/// <summary>Resultado da resolução de identidade: cliente unificado e o link que foi usado/criado.</summary>
public record ResolveIdentityResponse(
    Guid UnifiedCustomerId,
    CustomerSummaryDto Customer,
    bool WasCreated,
    ResolvedLinkDto ResolvedLink);

public record CustomerSummaryDto(Guid Id, string FullName, string Cpf);

public record ResolvedLinkDto(string Channel, string Identifier);
