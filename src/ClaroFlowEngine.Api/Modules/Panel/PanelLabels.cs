using ClaroFlowEngine.Api.Common.Contracts;

namespace ClaroFlowEngine.Api.Modules.Panel;

/// <summary>
/// Rótulos amigáveis para intent/canal, espelhando os mapas já usados no frontend do painel
/// (INTENT_LABELS/CHANNEL_LABELS em channels/attendant-panel/app.js) — resolvidos aqui para que
/// /journeys/active já entregue o texto pronto, sem o frontend precisar duplicar esse mapeamento.
/// </summary>
public static class PanelLabels
{
    private static readonly Dictionary<string, string> IntentLabels = new()
    {
        ["change_plan"] = "Troca de plano",
        ["dispute_charge"] = "Contestação de cobrança",
    };

    private static readonly Dictionary<string, string> ChannelLabels = new()
    {
        [Channels.Whatsapp] = "WhatsApp",
        [Channels.App] = "App Minha Claro",
        [Channels.Panel] = "Painel do Atendente",
        [Channels.Call] = "Central telefônica",
    };

    public static string Intent(string intent) => IntentLabels.GetValueOrDefault(intent, intent);

    public static string Channel(string channel) => ChannelLabels.GetValueOrDefault(channel, channel);

    /// <summary>Formata segundos como "4m 12s" (menos de 1h) ou "2h 34m" (1h ou mais).</summary>
    public static string TmaLabel(long? seconds)
    {
        if (seconds is null) return "—";

        var duration = TimeSpan.FromSeconds(seconds.Value);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
            : $"{duration.Minutes}m {duration.Seconds}s";
    }
}
