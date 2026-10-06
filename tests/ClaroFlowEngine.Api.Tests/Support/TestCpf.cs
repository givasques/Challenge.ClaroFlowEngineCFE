namespace ClaroFlowEngine.Api.Tests.Support;

/// <summary>
/// Gera CPFs de teste válidos e aleatórios. O banco de teste persiste entre execuções, então CPFs fixos
/// colidiriam com o ux_customers_cpf da execução anterior.
/// </summary>
public static class TestCpf
{
    public static string New()
    {
        while (true)
        {
            var random = Random.Shared;
            var baseDigits = string.Concat(Enumerable.Range(0, 9).Select(_ => (char)('0' + random.Next(10))));
            if (baseDigits.Distinct().Count() == 1) continue;

            var cpf = baseDigits + CheckDigit(baseDigits, 10);
            return cpf + CheckDigit(cpf, 11);
        }
    }

    private static char CheckDigit(string digits, int startWeight)
    {
        var sum = digits.Select((c, i) => (c - '0') * (startWeight - i)).Sum();
        var remainder = sum * 10 % 11;
        return (char)('0' + (remainder == 10 ? 0 : remainder));
    }
}
