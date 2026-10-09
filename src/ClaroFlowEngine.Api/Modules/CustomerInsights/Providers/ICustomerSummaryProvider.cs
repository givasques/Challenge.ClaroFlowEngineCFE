using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;

/// <summary>
/// Gera o resumo do cliente a partir do retrato minimizado (FASE 4.4, A.4). Cada provedor implementa o mesmo contrato.
/// </summary>
public interface ICustomerSummaryProvider
{
    /// <summary>Nome na configuração: "rules" ou "openai_compatible".</summary>
    string Name { get; }

    Task<CustomerSummaryResult> GenerateAsync(CustomerPortrait portrait, CancellationToken cancellationToken);
}

/// <summary>
/// Resultado de um provedor: conteúdo, origem e modelo (null no provedor por regras). Tokens são opcionais:
/// só os provedores que os informam os preenchem (usados no log, A.8).
/// </summary>
public sealed record CustomerSummaryResult(
    CustomerSummaryContent Content,
    string Source,
    string? Model,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? ReasoningTokens = null);

/// <summary>
/// Falha previsível de um provedor externo. <see cref="Reason"/> é o <c>fallback_reason</c> da resposta
/// (ai_error, ai_invalid_response, ai_rate_limited, ai_timeout). Não carrega prompt, resposta nem chave.
/// </summary>
public sealed class AiProviderException : Exception
{
    public AiProviderException(string reason, string message)
        : base(message)
    {
        Reason = reason;
    }

    public string Reason { get; }
}
