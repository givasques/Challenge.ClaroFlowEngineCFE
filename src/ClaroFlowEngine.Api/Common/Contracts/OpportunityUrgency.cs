namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Nível de urgência de uma oportunidade (FASE 3.6) — determina a ordenação da listagem.</summary>
public static class OpportunityUrgency
{
    public const string Critical = "critical";
    public const string High = "high";
    public const string Medium = "medium";
    public const string Low = "low";

    // Ordem de prioridade pra ORDER BY (crítica primeiro).
    private static readonly Dictionary<string, int> Rank = new()
    {
        [Critical] = 0,
        [High] = 1,
        [Medium] = 2,
        [Low] = 3,
    };

    private static readonly Dictionary<string, string> Labels = new()
    {
        [Critical] = "Crítica",
        [High] = "Alta",
        [Medium] = "Média",
        [Low] = "Baixa",
    };

    public static bool IsValid(string urgency) => Rank.ContainsKey(urgency);
    public static int RankOf(string urgency) => Rank.GetValueOrDefault(urgency, int.MaxValue);
    public static string Label(string urgency) => Labels.GetValueOrDefault(urgency, urgency);
}
