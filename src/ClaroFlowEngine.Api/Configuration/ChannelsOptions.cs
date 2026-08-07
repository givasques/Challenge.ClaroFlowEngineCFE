namespace ClaroFlowEngine.Api.Configuration;

/// <summary>
/// URLs base dos canais simulados, usadas para montar deep links e configurar CORS.
/// </summary>
public class ChannelsOptions
{
    public const string SectionName = "Channels";

    public string WhatsappSimBaseUrl { get; set; } = string.Empty;
    public string AppSimBaseUrl { get; set; } = string.Empty;
    public string AttendantPanelBaseUrl { get; set; } = string.Empty;
}
