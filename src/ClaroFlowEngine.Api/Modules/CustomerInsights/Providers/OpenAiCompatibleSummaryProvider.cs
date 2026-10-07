using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;

/// <summary>
/// Provedor para qualquer serviço com API compatível com OpenAI (FASE 4.4, B.1). HTTP direto, sem SDK de provedor.
/// Falhas viram <see cref="AiProviderException"/> com o motivo de fallback. Nunca loga chave, prompt nem resposta.
/// </summary>
public sealed class OpenAiCompatibleSummaryProvider : ICustomerSummaryProvider
{
    public const string ProviderName = "openai_compatible";
    public const string HttpClientName = "ai-summary";

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private static readonly JsonSerializerOptions PortraitJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        // Sem escape de acentos: o prompt chega legível ao modelo.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiSummaryOptions _options;
    private readonly ILogger<OpenAiCompatibleSummaryProvider> _logger;

    public OpenAiCompatibleSummaryProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<AiSummaryOptions> options,
        ILogger<OpenAiCompatibleSummaryProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => ProviderName;

    public async Task<CustomerSummaryResult> GenerateAsync(CustomerPortrait portrait, CancellationToken cancellationToken)
    {
        var userMessage = CustomerSummaryPrompt.UserPrefix + JsonSerializer.Serialize(portrait, PortraitJson);

        var useResponseFormat = true;
        for (var attempt = 1; ; attempt++)
        {
            var (status, body) = await PostAsync(userMessage, useResponseFormat, cancellationToken);

            if (status == 200)
                return ParseSuccess(body);

            // B.1: o provedor recusou response_format. Repete uma vez sem o campo; a validação continua valendo.
            if (status == 400 && useResponseFormat && body.Contains("response_format", StringComparison.OrdinalIgnoreCase))
            {
                useResponseFormat = false;
                continue;
            }

            // B.1: retry curto só em 429 e 503, uma vez, dentro do timeout.
            if ((status == 429 || status == 503) && attempt == 1)
            {
                await Task.Delay(RetryDelay, cancellationToken);
                continue;
            }

            _logger.LogWarning("Provedor de IA respondeu {StatusCode}", status);
            throw status == 429
                ? new AiProviderException("ai_rate_limited", "Provedor de IA com limite de uso atingido.")
                : new AiProviderException("ai_error", $"Provedor de IA respondeu {status}.");
        }
    }

    private async Task<(int Status, string Body)> PostAsync(string userMessage, bool useResponseFormat, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = _options.Model,
            ["messages"] = new object[]
            {
                new { role = "system", content = CustomerSummaryPrompt.System },
                new { role = "user", content = userMessage },
            },
            ["temperature"] = _options.Temperature,
            ["max_tokens"] = _options.MaxOutputTokens,
        };
        if (useResponseFormat)
            body["response_format"] = new { type = "json_object" };
        if (!string.IsNullOrWhiteSpace(_options.ReasoningEffort))
            body["reasoning_effort"] = _options.ReasoningEffort;

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return ((int)response.StatusCode, responseBody);
        }
        catch (HttpRequestException)
        {
            // Servidor fora do ar, conexão recusada ou resposta cortada. A mensagem não é logada: pode citar o endereço.
            throw new AiProviderException("ai_error", "Não foi possível falar com o provedor de IA.");
        }
    }

    private CustomerSummaryResult ParseSuccess(string responseBody)
    {
        string content;
        int? inputTokens = null;
        int? outputTokens = null;
        int? reasoningTokens = null;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            content = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()
                ?? throw new AiProviderException("ai_invalid_response", "Resposta do provedor sem conteúdo.");

            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var prompt) && prompt.TryGetInt32(out var p)) inputTokens = p;
                if (usage.TryGetProperty("completion_tokens", out var completion) && completion.TryGetInt32(out var c)) outputTokens = c;
                // Raciocínio conta dentro de completion_tokens; o detalhamento, quando o provedor informa, vem separado.
                if (usage.TryGetProperty("completion_tokens_details", out var details)
                    && details.TryGetProperty("reasoning_tokens", out var reasoning) && reasoning.TryGetInt32(out var r))
                    reasoningTokens = r;
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiProviderException("ai_invalid_response", "Envelope da resposta do provedor fora do formato esperado.");
        }

        var parsed = SummaryResponseValidator.Parse(content);
        return new CustomerSummaryResult(parsed, "ai", _options.Model, inputTokens, outputTokens, reasoningTokens);
    }
}
