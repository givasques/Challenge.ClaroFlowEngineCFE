namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Níveis operacionais de inatividade de uma jornada aberta (FASE 4.2, Central operacional).</summary>
public static class JourneyAlertLevel
{
    public const string Normal = "normal";
    public const string Warning = "warning";
    public const string Critical = "critical";

    /// <summary>Ordem usada pela Central operacional: crítico antes de atenção e normal.</summary>
    public static int RankOf(string level) => level switch
    {
        Critical => 0,
        Warning => 1,
        Normal => 2,
        _ => int.MaxValue,
    };
}
