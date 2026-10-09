namespace ClaroFlowEngine.Api.Modules.Handoff.Dtos;

/// <summary>Resposta de GET /context/resolve?token= — contexto completo para o canal de destino retomar a jornada (UC06).</summary>
public record ResolveTokenResponse(
    Guid UnifiedCustomerId,
    ResolvedJourneyDto JourneyContext,
    HandoffCustomerDto Customer,
    PlanDetailsDto? PlanDetails,
    InvoiceDetailsDto? InvoiceDetails);

public record ResolvedJourneyDto(
    Guid Id,
    string Intent,
    string CurrentStep,
    Dictionary<string, object> Payload,
    string Status);

/// <summary>
/// Cópia local e enxuta do cliente — evita acoplar o módulo Handoff a Identity/Context.
/// CpfMasked é null quando o cliente já foi anonimizado (Art. 18 LGPD) — nesse caso CpfLabel traz o rótulo (FASE 4.3, item A.2).
/// </summary>
public record HandoffCustomerDto(string FullName, string? CpfMasked, string? CpfLabel = null);

public record PlanDetailsDto(PlanInfoDto? CurrentPlan, PlanInfoDto? SelectedPlan);

public record PlanInfoDto(string Code, string Name, int MonthlyPriceCents);

/// <summary>
/// Fatura selecionada pelo cliente durante a contestação de cobrança (ETAPA 2, Passo C, item 6.5) —
/// embutida no resolve do handoff quando o payload da jornada já tem `invoice_id`, evitando
/// um segundo round-trip do App só para buscar a fatura.
/// </summary>
public record InvoiceDetailsDto(
    Guid Id,
    DateOnly ReferenceMonth,
    string ReferenceLabel,
    DateOnly DueDate,
    int TotalCents,
    string Status,
    List<InvoiceItemInfoDto> Items);

public record InvoiceItemInfoDto(Guid Id, int Sequence, string Description, string Category, int AmountCents);
