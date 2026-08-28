using ClaroFlowEngine.Api.Common.Contracts;
using System.Text.RegularExpressions;

namespace ClaroFlowEngine.Api.Common.Extensions;

/// <summary>
/// Validação de formato dos identificadores por canal, conforme spec-funcional §6.1 e §6.2.
/// CPF é validado com dígitos verificadores reais (ETAPA 2, Passo 0, item 3.5) — não apenas formato.
/// Vive em Common porque é usada por mais de um módulo (Identity e Handoff).
/// </summary>
public static partial class IdentifierFormat
{
    private static readonly string[] SupportedChannels =
        [Channels.Whatsapp, Channels.App, Channels.Cpf, Channels.Call];

    public static bool IsSupportedChannel(string channel) => SupportedChannels.Contains(channel);

    /// <summary>Remove pontos e traços, mantendo só os dígitos (uso interno de CPF).</summary>
    public static string SanitizeCpf(string rawCpf) => DigitsOnlyRegex().Replace(rawCpf, "");

    /// <summary>Valida formato (11 dígitos) e dígitos verificadores (algoritmo padrão brasileiro).</summary>
    public static bool IsValidCpf(string rawCpf)
    {
        var digits = SanitizeCpf(rawCpf);
        if (digits.Length != 11 || !digits.All(char.IsDigit)) return false;
        if (digits.Distinct().Count() == 1) return false; // rejeita 00000000000, 11111111111 etc.

        var d = digits.Select(c => c - '0').ToArray();

        var sum1 = 0;
        for (var i = 0; i < 9; i++) sum1 += d[i] * (10 - i);
        var check1 = sum1 * 10 % 11;
        if (check1 == 10) check1 = 0;
        if (d[9] != check1) return false;

        var sum2 = 0;
        for (var i = 0; i < 10; i++) sum2 += d[i] * (11 - i);
        var check2 = sum2 * 10 % 11;
        if (check2 == 10) check2 = 0;
        return d[10] == check2;
    }

    public static bool IsValidIdentifier(string channel, string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return false;

        return channel switch
        {
            Channels.Cpf => IsValidCpf(identifier),
            Channels.Whatsapp => WhatsappRegex().IsMatch(identifier),
            Channels.App => AppLoginRegex().IsMatch(identifier),
            // "call" não tem formato definido na spec — aceitamos qualquer identificador não vazio,
            // de forma semelhante ao canal "app", até que uma regra específica seja definida.
            Channels.Call => identifier.Length is >= 3 and <= 100,
            _ => false
        };
    }

    [GeneratedRegex(@"[.\-]")]
    private static partial Regex DigitsOnlyRegex();

    [GeneratedRegex(@"^\d{12,13}$")]
    private static partial Regex WhatsappRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_.-]{3,100}$")]
    private static partial Regex AppLoginRegex();
}
