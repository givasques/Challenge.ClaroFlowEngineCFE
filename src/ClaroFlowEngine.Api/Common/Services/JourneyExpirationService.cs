using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Configuration;
using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Common.Services;

public class JourneyExpirationService : IJourneyExpirationService
{
    private readonly ITransitionRecorder _transitionRecorder;
    private readonly CfeOptions _cfeOptions;
    private readonly ILogger<JourneyExpirationService> _logger;

    public JourneyExpirationService(
        ITransitionRecorder transitionRecorder, IOptions<CfeOptions> cfeOptions, ILogger<JourneyExpirationService> logger)
    {
        _transitionRecorder = transitionRecorder;
        _cfeOptions = cfeOptions.Value;
        _logger = logger;
    }

    public bool TryExpireIfInactive(JourneyContext journey)
    {
        if (journey.Status != JourneyStatus.Open) return false;

        var inactivity = DateTime.UtcNow - journey.UpdatedAt;
        var ttl = TimeSpan.FromHours(_cfeOptions.JourneyInactivityTtlHours);
        if (inactivity <= ttl) return false;

        journey.Status = JourneyStatus.Expired;
        journey.ClosedAt = DateTime.UtcNow;

        _transitionRecorder.Record(journey.Id, Channels.System, TransitionEventTypes.JourneyExpired,
            "Jornada expirada por inatividade.",
            new { hours_inactive = Math.Round(inactivity.TotalHours, 1) });

        _logger.LogInformation("Journey {JourneyId} expired due to inactivity", journey.Id);

        return true;
    }
}
