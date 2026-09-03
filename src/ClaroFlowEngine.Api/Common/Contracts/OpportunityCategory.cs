namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Categorias de oportunidade comercial detectadas a partir de jornadas históricas (FASE 3.6).</summary>
public static class OpportunityCategory
{
    public const string AbandonedPlanChange = "abandoned_plan_change";
    public const string AbandonedDispute = "abandoned_dispute";
    public const string ActiveEngagedCustomer = "active_engaged_customer";
    public const string InactiveCustomer = "inactive_customer";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [AbandonedPlanChange] = "Abandonou troca de plano",
        [AbandonedDispute] = "Abandonou contestação",
        [ActiveEngagedCustomer] = "Cliente ativo (upsell)",
        [InactiveCustomer] = "Cliente inativo (reativação)",
    };

    private static readonly Dictionary<string, string> Urgencies = new()
    {
        [AbandonedPlanChange] = OpportunityUrgency.High,
        [AbandonedDispute] = OpportunityUrgency.Critical,
        [ActiveEngagedCustomer] = OpportunityUrgency.Low,
        [InactiveCustomer] = OpportunityUrgency.Medium,
    };

    private static readonly Dictionary<string, int> ValidityDays = new()
    {
        [AbandonedPlanChange] = 30,
        [AbandonedDispute] = 7,
        [ActiveEngagedCustomer] = 60,
        [InactiveCustomer] = 90,
    };

    private static readonly Dictionary<string, string> SuggestedActions = new()
    {
        [AbandonedPlanChange] = "Oferecer plano de interesse com desconto ou plano intermediário",
        [AbandonedDispute] = "Contato imediato — cliente pode estar em risco de churn",
        [ActiveEngagedCustomer] = "Cliente engajado — oferecer produtos complementares ou upgrade",
        [InactiveCustomer] = "Reengajamento — check-in + benefício exclusivo",
    };

    public static string Label(string category) => Labels.GetValueOrDefault(category, category);
    public static string Urgency(string category) => Urgencies.GetValueOrDefault(category, OpportunityUrgency.Medium);
    public static int ValidityDaysFor(string category) => ValidityDays.GetValueOrDefault(category, 30);
    public static string SuggestedAction(string category) => SuggestedActions.GetValueOrDefault(category, "Avaliar oportunidade");
}
