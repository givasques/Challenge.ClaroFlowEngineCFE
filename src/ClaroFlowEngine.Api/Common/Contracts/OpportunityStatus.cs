namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Ciclo de vida de uma oportunidade (FASE 3.6): new → contacted → converted | not_relevant.</summary>
public static class OpportunityStatus
{
    public const string New = "new";
    public const string Contacted = "contacted";
    public const string Converted = "converted";
    public const string NotRelevant = "not_relevant";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [New] = "Nova",
        [Contacted] = "Abordada",
        [Converted] = "Convertida",
        [NotRelevant] = "Não relevante",
    };

    /// <summary>Rótulo em português (FASE 4.3, item C.3 — exportação de dados) — espelha os textos já usados no painel.</summary>
    public static string Label(string status) => Labels.GetValueOrDefault(status, status);
}
