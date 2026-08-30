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
}
