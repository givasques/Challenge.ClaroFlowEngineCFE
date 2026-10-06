namespace ClaroFlowEngine.Api.Configuration;

/// <summary>
/// Resumo do cliente com IA (FASE 4.4). Sem chave, o resumo vem do provedor por regras.
/// A API sobe mesmo sem chave: a falta de configuração vira aviso, não erro de subida.
/// </summary>
public class AiSummaryOptions
{
    public const string SectionName = "AiSummary";

    public bool Enabled { get; set; } = true;

    /// <summary>"rules" (sem IA) ou "openai_compatible" (qualquer serviço com API compatível com OpenAI).</summary>
    public string Provider { get; set; } = "rules";

    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>Nunca versionada: vem de AiSummary__ApiKey (variável de ambiente ou user-secrets).</summary>
    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 20;
    public int MaxOutputTokens { get; set; } = 600;
    public double Temperature { get; set; } = 0.2;

    /// <summary>Jornadas mais recentes enviadas ao provedor.</summary>
    public int MaxJourneys { get; set; } = 20;

    /// <summary>Gerações por atendente por minuto. Respostas do cache não contam.</summary>
    public int RequestsPerUserPerMinute { get; set; } = 6;

    /// <summary>
    /// Provedor externo configurado o bastante para ser chamado. Ollama local (sem chave) é aceito quando
    /// BaseUrl aponta para o próprio host.
    /// </summary>
    public bool IsRemoteProviderConfigured()
    {
        if (Provider == "rules") return false;
        if (string.IsNullOrWhiteSpace(BaseUrl) || string.IsNullOrWhiteSpace(Model)) return false;
        if (!string.IsNullOrWhiteSpace(ApiKey)) return true;

        return BaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || BaseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || BaseUrl.Contains("host.docker.internal", StringComparison.OrdinalIgnoreCase);
    }
}
