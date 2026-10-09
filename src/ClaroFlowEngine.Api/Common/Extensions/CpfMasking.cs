using System.Text.RegularExpressions;

namespace ClaroFlowEngine.Api.Common.Extensions;

/// <summary>
/// Mascaramento de CPF para exibição e logs (FASE 4.3, item A.1). Formato único no sistema inteiro:
/// mostra os 6 dígitos do meio, esconde os 3 primeiros e os 2 verificadores (***.456.789-**).
/// Vive em Common porque é usada por mais de um módulo (Context, Identity, Panel, Opportunities, Handoff, Lgpd).
/// </summary>
public static partial class CpfMasking
{
    /// <summary>
    /// 11 dígitos → mascarado. CPF já anonimizado (hash SHA-256 de 64 caracteres) ou nulo/vazio → null.
    /// </summary>
    public static string? Mask(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return null;
        if (cpf.Length != 11) return null; // inclui o hash de 64 caracteres do cliente anonimizado

        return $"***.{cpf.Substring(3, 3)}.{cpf.Substring(6, 3)}-**";
    }

    /// <summary>
    /// Mascara qualquer segmento de 11 dígitos num path de requisição (ex: a rota do direito ao
    /// esquecimento, /customers/{cpf}/right-to-be-forgotten), para uso só em logging — a rota real
    /// não muda (FASE 4.3, item A.3). Nenhuma outra rota do projeto usa um segmento só-dígitos de
    /// 11 caracteres, então a troca é segura de forma genérica.
    /// </summary>
    public static string? MaskInPath(string? path)
        => path is null ? null : ElevenDigitsSegmentRegex().Replace(path, m => Mask(m.Value) ?? m.Value);

    [GeneratedRegex(@"(?<=/)\d{11}(?=/|$)")]
    private static partial Regex ElevenDigitsSegmentRegex();
}
