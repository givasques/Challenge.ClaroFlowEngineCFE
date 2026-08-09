using ClaroFlowEngine.Api.Common.Contracts;
using System.Text.RegularExpressions;

namespace ClaroFlowEngine.Api.Common.Extensions;

/// <summary>
/// Validação de formato dos identificadores por canal, conforme spec-funcional §6.1 e §6.2.
/// Não valida dígito verificador de CPF — os CPFs do seed são fictícios, só o formato importa.
/// Vive em Common porque é usada por mais de um módulo (Identity e Handoff).
/// </summary>
public static partial class IdentifierFormat
{
    private static readonly string[] SupportedChannels =
        [Channels.Whatsapp, Channels.App, Channels.Cpf, Channels.Call];

    public static bool IsSupportedChannel(string channel) => SupportedChannels.Contains(channel);

    /// <summary>Remove pontos e traços, mantendo só os dígitos (uso interno de CPF).</summary>
    public static string SanitizeCpf(string rawCpf) => DigitsOnlyRegex().Replace(rawCpf, "");

    public static bool IsValidCpf(string rawCpf) => DigitsOnlyRegex().Replace(rawCpf, "") is { Length: 11 } digits
        && digits.All(char.IsDigit);

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
