using ClaroFlowEngine.Api.Common.Contracts;

namespace ClaroFlowEngine.Api.Modules.Panel;

/// <summary>
/// Rótulos amigáveis para intent, canal e etapa, espelhando os mapas usados no frontend do painel
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

    /// <summary>
    /// Etapas que o sistema grava em current_step (bot, App e Context). As etapas "awaiting_*" são estados
    /// internos do bot do WhatsApp e hoje não são persistidas; ficam mapeadas para o caso de passarem a ser.
    /// </summary>
    private static readonly Dictionary<string, string> CurrentStepLabels = new()
    {
        ["awaiting_intent"] = "Aguardando escolha do atendimento",
        ["awaiting_cpf"] = "Aguardando CPF",
        ["awaiting_name"] = "Aguardando nome",
        ["awaiting_plan_choice"] = "Aguardando escolha de plano",
        ["awaiting_invoice_choice"] = "Aguardando escolha de fatura",
        ["awaiting_dispute_reason"] = "Aguardando motivo da contestação",
        ["awaiting_problem_description"] = "Aguardando descrição do problema",
        ["identity_resolved"] = "Identidade resolvida",
        ["plan_selected"] = "Plano selecionado",
        ["invoice_selected"] = "Fatura selecionada",
        ["dispute_reason_selected"] = "Motivo informado",
        ["description_provided"] = "Descrição do problema enviada",
        ["dispute_formalized"] = "Contestação formalizada",
    };

    public static string Intent(string intent) => IntentLabels.GetValueOrDefault(intent, intent);

    public static string Channel(string channel) => ChannelLabels.GetValueOrDefault(channel, channel);

    public static string CurrentStep(string currentStep) => CurrentStepLabels.GetValueOrDefault(currentStep, currentStep);

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
