namespace ClaroFlowEngine.Api.Configuration;

/// <summary>
/// Configurações de negócio do CFE (TTLs, limites operacionais e tokens de canal permitidos).
/// </summary>
public class CfeOptions
{
    public const string SectionName = "Cfe";

    public int HandoffTokenTtlMinutes { get; set; } = 30;
    public int JourneyInactivityTtlHours { get; set; } = 24;
    public string[] AllowedChannelTokens { get; set; } = [];

    /// <summary>Janela de deduplicação de transições `panel_accessed` por jornada (ETAPA 2, Passo B, item 5.5).</summary>
    public int PanelAccessDedupMinutes { get; set; } = 5;

    /// <summary>Tentativas de abrir o handoff com conta que não é a dona da jornada antes de revogar o token (FASE 4.3, item B.4).</summary>
    public int HandoffMaxOwnerMismatchAttempts { get; set; } = 3;

    /// <summary>Inatividade mínima para uma jornada aberta requerer atenção na Central operacional (FASE 4.2).</summary>
    public int JourneyAttentionThresholdMinutes { get; set; } = 5;

    /// <summary>Inatividade mínima para uma jornada aberta ser classificada como crítica na Central operacional (FASE 4.2).</summary>
    public int JourneyCriticalThresholdMinutes { get; set; } = 15;

    /// <summary>Fuso de Brasília usado para o contador "jornadas hoje" (FASE 4.2). Identificador IANA.</summary>
    public string BusinessTimeZoneId { get; set; } = "America/Sao_Paulo";

    public bool IsValidBusinessTimeZone()
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(BusinessTimeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
