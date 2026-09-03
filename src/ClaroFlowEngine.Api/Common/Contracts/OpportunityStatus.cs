namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Ciclo de vida de uma oportunidade (FASE 3.6): new → contacted → converted | not_relevant.</summary>
public static class OpportunityStatus
{
    public const string New = "new";
    public const string Contacted = "contacted";
    public const string Converted = "converted";
    public const string NotRelevant = "not_relevant";
}
