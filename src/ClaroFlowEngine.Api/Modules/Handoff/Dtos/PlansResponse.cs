namespace ClaroFlowEngine.Api.Modules.Handoff.Dtos;

/// <summary>Resposta de GET /plans — formato diferente de PlanInfoDto (inclui data_gb), conforme spec-tecnica §5.5.</summary>
public record PlansResponse(List<PlanSummaryDto> Plans);

public record PlanSummaryDto(string Code, string Name, int DataGb, int MonthlyPriceCents);
