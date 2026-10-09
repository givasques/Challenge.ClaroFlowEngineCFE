namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Motivos válidos para revelar o CPF completo de um cliente no painel (FASE 4.3, item A.4).</summary>
public static class CpfRevealReason
{
    public const string IdentityConfirmation = "identity_confirmation";
    public const string CustomerRequest = "customer_request";
    public const string EscalationRequirement = "escalation_requirement";
    public const string Other = "other";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [IdentityConfirmation] = "Confirmação de identidade com o cliente",
        [CustomerRequest] = "Solicitação do próprio cliente",
        [EscalationRequirement] = "Exigência da área para escalação",
        [Other] = "Outro",
    };

    public static bool IsValid(string reason) => Labels.ContainsKey(reason);

    public static string Label(string reason) => Labels.GetValueOrDefault(reason, reason);
}
