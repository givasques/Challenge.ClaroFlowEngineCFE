using ClaroFlowEngine.Api.Modules.Panel.Dtos;

namespace ClaroFlowEngine.Api.Modules.Panel.Services;

public interface IPanelService
{
    Task<ActiveJourneysResponse> GetActiveJourneysAsync(bool includeEscalated, CancellationToken cancellationToken);
    Task<ActiveJourneysResponse> SearchActiveJourneysAsync(string query, CancellationToken cancellationToken);
    Task<ActiveAlertsResponse> GetActiveAlertsAsync(CancellationToken cancellationToken);
    Task<MetricsSummaryResponse> GetMetricsSummaryAsync(CancellationToken cancellationToken);
    Task<ConcludeJourneyResponse> ConcludeJourneyAsync(Guid journeyId, ConcludeJourneyRequest request, CancellationToken cancellationToken);
    Task<EscalateJourneyResponse> EscalateJourneyAsync(Guid journeyId, EscalateJourneyRequest request, CancellationToken cancellationToken);
}
