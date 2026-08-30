namespace ClaroFlowEngine.Api.Modules.Identity.Dtos;

/// <summary>Resultado da resolução de identidade: cliente unificado e o link que foi usado/criado.</summary>
public record ResolveIdentityResponse(
    Guid UnifiedCustomerId,
    CustomerSummaryDto Customer,
    bool WasCreated,
    ResolvedLinkDto ResolvedLink);

/// <summary>
/// CurrentPlan é usado pelo bot do WhatsApp para informar o plano atual do cliente antes de mostrar
/// a lista de troca de plano (FASE 3, item B.1) — null quando o cliente não tem plano ativo.
/// </summary>
public record CustomerSummaryDto(Guid Id, string FullName, string Cpf, PlanInfoDto? CurrentPlan = null);

public record PlanInfoDto(string Code, string Name, int MonthlyPriceCents);

public record ResolvedLinkDto(string Channel, string Identifier);
