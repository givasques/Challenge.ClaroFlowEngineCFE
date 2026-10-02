namespace ClaroFlowEngine.Api.Configuration;

/// <summary>Proteção contra força bruta no login do painel (FASE 4.1).</summary>
public class PanelAuthOptions
{
    public const string SectionName = "PanelAuth";

    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int LoginRateLimitPerMinute { get; set; } = 10;
}
