namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;

/// <summary>Corpo opcional de POST /customers/{customerId}/ai-summary.</summary>
public record CustomerSummaryRequest(bool ForceRefresh = false);

/// <summary>
/// Resposta de POST /customers/{customerId}/ai-summary. Falha da IA não é erro HTTP: volta o resumo por regras
/// com <c>fallback_reason</c> preenchido.
/// </summary>
public record CustomerSummaryResponse(
    string Summary,
    IReadOnlyList<string> AttentionPoints,
    string? SuggestedApproach,
    string Source,
    string SourceLabel,
    string? Model,
    DateTime GeneratedAt,
    bool Cached,
    string? FallbackReason,
    string? FallbackLabel);

/// <summary>Conteúdo do resumo, como vem do provedor e como é guardado no cache (só para a IA).</summary>
public record CustomerSummaryContent(
    string Summary,
    IReadOnlyList<string> AttentionPoints,
    string? SuggestedApproach);
