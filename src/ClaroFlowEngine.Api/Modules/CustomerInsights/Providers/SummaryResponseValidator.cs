using System.Text.Json;
using System.Text.RegularExpressions;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;

/// <summary>
/// Valida a resposta do modelo (FASE 4.4, B.3). Falha em qualquer checagem vira <c>ai_invalid_response</c>, com o
/// motivo da falha na mensagem (sem o conteúdo da resposta). Textos acima dos limites são cortados, não rejeitados.
/// </summary>
public static class SummaryResponseValidator
{
    private const int MaxSummaryLength = 400;
    private const int MaxPointLength = 160;
    private const int MaxApproachLength = 250;
    private const int MaxPoints = 4;

    // Bloco de código markdown que alguns modelos devolvem em volta do JSON.
    private static readonly Regex CodeBlockPattern = new(@"```[A-Za-z]*\s*(?<body>.*?)\s*```", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex CpfPattern = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);

    // Telefone: DDD entre parênteses com hífen, DDI 55 com 13 dígitos, ou DDD com espaço, nono dígito 9 e hífen.
    // Não casa valor em reais (R$ 129,90), data (06/10/2026) nem mês de referência (08/2026).
    private static readonly Regex PhonePattern = new(@"\(\d{2}\)\s?9?\d{4}-\d{4}\b|\b55\d{11}\b|\b\d{2}\s9\d{4}-\d{4}\b", RegexOptions.Compiled);

    private static readonly Regex EmailPattern = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    /// <summary>Conteúdo do campo "content" da resposta do provedor (já extraído do envelope da API).</summary>
    public static CustomerSummaryContent Parse(string content)
    {
        var json = RemoveCodeBlocks(content);

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(json);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw Invalid("JSON não interpretável");
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw Invalid("JSON não é um objeto");

        if (!root.TryGetProperty("summary", out var summaryElement) || summaryElement.ValueKind != JsonValueKind.String)
            throw Invalid("campo summary ausente ou não é texto");
        if (!root.TryGetProperty("attention_points", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
            throw Invalid("campo attention_points ausente ou não é lista");
        if (!root.TryGetProperty("suggested_approach", out var approachElement) || approachElement.ValueKind != JsonValueKind.String)
            throw Invalid("campo suggested_approach ausente ou não é texto");

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
        if (CpfPattern.IsMatch(all)) throw Invalid("contém padrão de CPF");
        if (PhonePattern.IsMatch(all)) throw Invalid("contém padrão de telefone");
        if (EmailPattern.IsMatch(all)) throw Invalid("contém padrão de e-mail");

        return new CustomerSummaryContent(summary, points, approach.Length == 0 ? null : approach);
    }

    /// <summary>Se a resposta vier dentro de bloco markdown, usa só o corpo do bloco. Caso contrário, devolve o texto.</summary>
    internal static string RemoveCodeBlocks(string content)
    {
        var match = CodeBlockPattern.Match(content);
        return match.Success ? match.Groups["body"].Value.Trim() : content.Trim();
    }

    private static AiProviderException Invalid(string detail) =>
        new("ai_invalid_response", $"Resposta do provedor de IA inválida: {detail}.");

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
