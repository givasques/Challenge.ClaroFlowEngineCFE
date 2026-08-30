using ClaroFlowEngine.Api.Modules.Panel.Dtos;

namespace ClaroFlowEngine.Api.Modules.Panel.Services;

public interface IPanelService
{
    Task<ActiveJourneysResponse> GetActiveJourneysAsync(CancellationToken cancellationToken);
    Task<MetricsSummaryResponse> GetMetricsSummaryAsync(CancellationToken cancellationToken);
}
