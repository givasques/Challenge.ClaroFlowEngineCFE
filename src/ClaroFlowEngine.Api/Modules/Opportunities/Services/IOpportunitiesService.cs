using ClaroFlowEngine.Api.Modules.Opportunities.Dtos;

namespace ClaroFlowEngine.Api.Modules.Opportunities.Services;

public interface IOpportunitiesService
{
    Task<DetectOpportunitiesResponse> DetectAsync(CancellationToken cancellationToken);

    Task<OpportunitiesListResponse> ListAsync(
        string? statusFilter, string? category, string? urgency, int limit, int offset, CancellationToken cancellationToken);

    Task<OpportunityDto> MarkAsContactedAsync(Guid id, string? notes, CancellationToken cancellationToken);
    Task<OpportunityDto> MarkAsConvertedAsync(Guid id, string? notes, CancellationToken cancellationToken);
    Task<OpportunityDto> MarkAsNotRelevantAsync(Guid id, string? notes, CancellationToken cancellationToken);
}
