namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Categorias padronizadas de desfecho ao concluir uma jornada pelo painel (FASE 3.5).</summary>
public static class ResolutionCategory
{
    public const string ResolvedAnswered = "resolved_answered";
    public const string ResolvedActionTaken = "resolved_action_taken";
    public const string ResolvedGuidanceGiven = "resolved_guidance_given";
    public const string CustomerGaveUp = "customer_gave_up";
    public const string UnableToResolve = "unable_to_resolve";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [ResolvedAnswered] = "Resolvido — dúvida esclarecida",
        [ResolvedActionTaken] = "Resolvido — ação executada em outro sistema",
        [ResolvedGuidanceGiven] = "Resolvido — orientação dada, cliente executará depois",
        [CustomerGaveUp] = "Cliente desistiu",
        [UnableToResolve] = "Não foi possível resolver — requer follow-up",
    };

    public static bool IsValid(string category) => Labels.ContainsKey(category);

    public static string Label(string category) => Labels.GetValueOrDefault(category, category);
}
