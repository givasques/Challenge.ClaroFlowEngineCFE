using ClaroFlowEngine.Api.Modules.Context.Dtos;

namespace ClaroFlowEngine.Api.Modules.Context.Services;

public interface IContextService
{
    /// <summary>WasCreated indica se uma jornada nova foi criada (201) ou se uma já ativa foi retornada (200, idempotência).</summary>
    Task<(JourneySummaryResponse Response, bool WasCreated)> OpenAsync(OpenJourneyRequest request, CancellationToken cancellationToken);

    Task<JourneySummaryResponse> UpdateAsync(Guid journeyId, PatchContextRequest request, CancellationToken cancellationToken);

    Task<JourneyDetailResponse> GetByIdAsync(Guid journeyId, CancellationToken cancellationToken);

    Task<ActiveJourneyResponse> GetActiveByCustomerAsync(Guid customerId, bool includeHistory, CancellationToken cancellationToken);

    Task<TransitionsResponse> GetTransitionsAsync(Guid journeyId, CancellationToken cancellationToken);

    Task<JourneySummaryResponse> CloseAsync(Guid journeyId, CloseJourneyRequest request, CancellationToken cancellationToken);
}
