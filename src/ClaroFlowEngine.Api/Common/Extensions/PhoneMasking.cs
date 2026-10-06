namespace ClaroFlowEngine.Api.Common.Extensions;

/// <summary>
/// Mascaramento de telefone para exibição (FASE 4.2, item de telefone da Central). Mesmo espírito do CPF:
/// mostra só os últimos 4 dígitos e o DDD, no formato (11) *****-8888.
/// Aceita o identificador do canal WhatsApp com DDI 55 (13 dígitos) ou sem DDI (10 ou 11 dígitos).
/// </summary>
public static class PhoneMasking
{
    public static string? Mask(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 13 && digits.StartsWith("55", StringComparison.Ordinal))
            digits = digits[2..];
        if (digits.Length is not (10 or 11)) return null;

        return $"({digits[..2]}) *****-{digits[^4..]}";
    }
}
