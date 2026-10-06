namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>
/// Tipos de evento registrados em journey_transitions, conforme spec-tecnica §4.2 e spec-funcional §6.6.
/// </summary>
public static class TransitionEventTypes
{
    public const string JourneyStarted = "journey_started";
    public const string JourneyReopenAttempted = "journey_reopen_attempted";
    public const string StepUpdated = "step_updated";
    public const string DeepLinkGenerated = "deep_link_generated";
    public const string JourneyResumed = "journey_resumed";
    public const string JourneyClosed = "journey_closed";
    public const string JourneyExpired = "journey_expired";
    public const string PanelAccessed = "panel_accessed";

    /// <summary>Direito ao esquecimento exercido (Art. 18 LGPD, FASE 3.4) — transição órfã, sem journey_context_id.</summary>
    public const string DataAnonymizationRequested = "data_anonymization_requested";

    /// <summary>Jornada concluída manualmente pelo atendente via painel (FASE 3.5), com categoria de desfecho.</summary>
    public const string JourneyConcludedByAgent = "journey_concluded_by_agent";

    /// <summary>Jornada escalada para outra área via painel (FASE 3.5) — jornada permanece registrada, sem fechar.</summary>
    public const string JourneyEscalated = "journey_escalated";

    /// <summary>CPF completo revelado pelo atendente no painel (FASE 4.3, item A.4) — transição órfã, sem journey_context_id.</summary>
    public const string CustomerCpfRevealed = "customer_cpf_revealed";

    /// <summary>Tentativa de abrir o link de continuação com conta que não é a dona da jornada, bloqueada (FASE 4.3, item B.4).</summary>
    public const string HandoffOwnerMismatch = "handoff_owner_mismatch";

    /// <summary>Token de handoff revogado após atingir o limite de tentativas de outra conta (FASE 4.3, item B.4).</summary>
    public const string HandoffTokenRevoked = "handoff_token_revoked";

    /// <summary>Exportação de dados solicitada (LGPD Art. 18, V — FASE 4.3, item C.3) — transição órfã, sem journey_context_id.</summary>
    public const string DataPortabilityRequested = "data_portability_requested";

    /// <summary>
    /// Eventos que contam como atividade operacional da jornada (FASE 4.2). Usados para o canal atual e para a
    /// inatividade. Eventos apenas observacionais, como <see cref="PanelAccessed"/>, ficam deliberadamente fora —
    /// abrir o painel não deve mudar o canal da jornada nem zerar o alerta de inatividade.
    /// </summary>
    public static readonly string[] OperationalActivityTypes =
    [
        JourneyStarted,
        JourneyReopenAttempted,
        StepUpdated,
        DeepLinkGenerated,
        JourneyResumed,
        JourneyClosed,
        JourneyExpired,
        JourneyConcludedByAgent,
        JourneyEscalated,
    ];
}
