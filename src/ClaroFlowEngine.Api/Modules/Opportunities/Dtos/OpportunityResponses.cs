namespace ClaroFlowEngine.Api.Modules.Opportunities.Dtos;

/// <summary>Resposta de POST /opportunities/detect.</summary>
public record DetectOpportunitiesResponse(DateTime DetectedAt, Dictionary<string, int> OpportunitiesCreated, int Total);

/// <summary>Resposta de GET /opportunities.</summary>
public record OpportunitiesListResponse(int Total, List<OpportunityDto> Opportunities);

public record OpportunityDto(
    Guid Id,
    OpportunityCustomerDto Customer,
    string Category,
    string CategoryLabel,
    string Urgency,
    string UrgencyLabel,
    string Status,
    DateTime DetectedAt,
    DateTime ValidUntil,
    int DaysRemaining,
    OpportunityTriggeringJourneyDto? TriggeringJourney,
    Dictionary<string, object> Metadata,
    string SuggestedAction,
    DateTime? ContactedAt,
    string? ContactedBy,
    DateTime? ResolvedAt,
    string? ResolutionNotes);

public record OpportunityCustomerDto(Guid Id, string FullName, string Cpf, string? Phone);

public record OpportunityTriggeringJourneyDto(Guid Id, string Intent, string? AbandonedAtStep);

/// <summary>Body opcional de POST /opportunities/{id}/mark-as-contacted|converted|not-relevant.</summary>
public record OpportunityActionRequest(string? Notes);
