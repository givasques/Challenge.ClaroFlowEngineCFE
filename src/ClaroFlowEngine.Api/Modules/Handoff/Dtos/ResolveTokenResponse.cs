namespace ClaroFlowEngine.Api.Modules.Handoff.Dtos;

/// <summary>Resposta de GET /context/resolve?token= — contexto completo para o canal de destino retomar a jornada (UC06).</summary>
public record ResolveTokenResponse(
    Guid UnifiedCustomerId,
    ResolvedJourneyDto JourneyContext,
    HandoffCustomerDto Customer,
    PlanDetailsDto? PlanDetails);

public record ResolvedJourneyDto(
    Guid Id,
    string Intent,
    string CurrentStep,
    Dictionary<string, object> Payload,
    string Status);

/// <summary>Cópia local e enxuta do cliente — evita acoplar o módulo Handoff a Identity/Context.</summary>
public record HandoffCustomerDto(string FullName, string Cpf);

public record PlanDetailsDto(PlanInfoDto? CurrentPlan, PlanInfoDto? SelectedPlan);

public record PlanInfoDto(string Code, string Name, int MonthlyPriceCents);
