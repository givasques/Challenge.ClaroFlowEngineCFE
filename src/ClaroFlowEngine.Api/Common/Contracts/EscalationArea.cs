namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Áreas mockadas para onde uma jornada pode ser escalada pelo painel (FASE 3.5).</summary>
public static class EscalationArea
{
    public const string TechnicalSupport = "technical_support";
    public const string Financial = "financial";
    public const string Retention = "retention";
    public const string Sales = "sales";
    public const string Ombudsman = "ombudsman";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [TechnicalSupport] = "Suporte técnico",
        [Financial] = "Financeiro / Cobrança",
        [Retention] = "Retenção / Fidelização",
        [Sales] = "Vendas / Comercial",
        [Ombudsman] = "Ouvidoria",
    };

    public static bool IsValid(string area) => Labels.ContainsKey(area);

    public static string Label(string area) => Labels.GetValueOrDefault(area, area);
}
