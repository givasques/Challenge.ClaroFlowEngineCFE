using System.Text.RegularExpressions;
using System.Text.Json;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;

/// <summary>
/// Valida a resposta do modelo (FASE 4.4, B.3). Falha em qualquer checagem vira <c>ai_invalid_response</c>.
/// O modelo não recebe CPF, telefone nem e-mail; se algum aparecer na resposta, algo saiu errado, então descarta tudo.
/// </summary>
internal static class SummaryResponseValidator
{
    private const int MaxSummaryLength = 400;
    private const int MaxPointLength = 160;
    private const int MaxApproachLength = 250;
    private const int MaxPoints = 4;

    private static readonly Regex CpfPattern = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"\(\d{2}\)\s?\d{4,5}-?\d{4}|\b55\d{10,11}\b|\b\d{2}\s?9\d{4}-?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    /// <summary>Conteúdo do campo "content" da resposta do provedor (já extraído do envelope da API).</summary>
    public static CustomerSummaryContent Parse(string content)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(content);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw Invalid("a resposta não é JSON");
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid("a resposta não é um objeto");

        if (!root.TryGetProperty("summary", out var summaryElement) || summaryElement.ValueKind != JsonValueKind.String)
            throw Invalid("campo summary ausente ou inválido");
        if (!root.TryGetProperty("attention_points", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
            throw Invalid("campo attention_points ausente ou inválido");
        if (!root.TryGetProperty("suggested_approach", out var approachElement) || approachElement.ValueKind != JsonValueKind.String)
            throw Invalid("campo suggested_approach ausente ou inválido");

        var summary = Truncate(summaryElement.GetString() ?? string.Empty, MaxSummaryLength).Trim();
        if (summary.Length == 0) throw Invalid("summary vazio");

        var points = new List<string>();
        foreach (var item in pointsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw Invalid("attention_points com item que não é texto");
            var point = Truncate((item.GetString() ?? string.Empty).Trim(), MaxPointLength);
            if (point.Length > 0) points.Add(point);
            if (points.Count == MaxPoints) break;
        }

        var approach = Truncate((approachElement.GetString() ?? string.Empty).Trim(), MaxApproachLength);

        var all = string.Join(' ', [summary, .. points, approach]);
        if (CpfPattern.IsMatch(all) || PhonePattern.IsMatch(all) || EmailPattern.IsMatch(all))
            throw Invalid("a resposta contém dado pessoal");

        return new CustomerSummaryContent(summary, points, approach.Length == 0 ? null : approach);
    }

    private static AiProviderException Invalid(string detail) =>
        new("ai_invalid_response", $"Resposta do provedor de IA inválida: {detail}.");

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
