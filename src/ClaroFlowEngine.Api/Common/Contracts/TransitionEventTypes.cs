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
}
