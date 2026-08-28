namespace ClaroFlowEngine.Api.Configuration;

/// <summary>
/// Configurações de negócio do CFE (TTLs e tokens de canal permitidos).
/// </summary>
public class CfeOptions
{
    public const string SectionName = "Cfe";

    public int HandoffTokenTtlMinutes { get; set; } = 30;
    public int JourneyInactivityTtlHours { get; set; } = 24;
    public string[] AllowedChannelTokens { get; set; } = [];

    /// <summary>Janela de deduplicação de transições `panel_accessed` por jornada (ETAPA 2, Passo B, item 5.5).</summary>
    public int PanelAccessDedupMinutes { get; set; } = 5;
}
