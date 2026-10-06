namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Motivos pré-definidos de contestação de cobrança (FASE 3, Bloco A).</summary>
public static class DisputeReason
{
    public const string ServiceNotContracted = "service_not_contracted";
    public const string HigherThanExpected = "higher_than_expected";
    public const string DuplicateCharge = "duplicate_charge";
    public const string CancelledServiceStillCharged = "cancelled_service_still_charged";
    public const string AfterPortability = "after_portability";
    public const string Other = "other";

    // Textos iguais aos de DISPUTE_REASON_LABELS no painel (FASE 4.4, P43: todo código que vai ao modelo tem rótulo).
    private static readonly Dictionary<string, string> Labels = new()
    {
        [ServiceNotContracted] = "Cobrança de serviço que não contratei",
        [HigherThanExpected] = "Valor cobrado maior que o esperado",
        [DuplicateCharge] = "Cobrança em duplicidade",
        [CancelledServiceStillCharged] = "Serviço cancelado ainda sendo cobrado",
        [AfterPortability] = "Cobrança após portabilidade",
        [Other] = "Outro motivo",
    };

    public static bool IsValid(string reason) => Labels.ContainsKey(reason);

    public static string Label(string reason) => Labels.GetValueOrDefault(reason, reason);
}
