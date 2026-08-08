namespace ClaroFlowEngine.Api.Modules.Context.Dtos;

/// <summary>Representação enxuta da jornada — usada em open/patch/close, conforme os exemplos da spec técnica.</summary>
public record JourneySummaryResponse(
    Guid Id,
    Guid CustomerId,
    string OriginChannel,
    string Intent,
    string CurrentStep,
    Dictionary<string, object> Payload,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt);

/// <summary>Representação com o cliente embutido — usada em GET /context/{id} e GET /context/customer/{id}.</summary>
public record JourneyDetailResponse(
    Guid Id,
    Guid CustomerId,
    CustomerSummaryDto Customer,
    string OriginChannel,
    string Intent,
    string CurrentStep,
    Dictionary<string, object> Payload,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ClosedAt);

/// <summary>Cópia local e enxuta do resumo de cliente — evita acoplar o módulo Context ao Identity.</summary>
public record CustomerSummaryDto(Guid Id, string FullName, string Cpf);

/// <summary>Resposta de GET /context/customer/{customerId}: jornada ativa (se houver) + histórico opcional.</summary>
public record ActiveJourneyResponse(
    JourneyDetailResponse? Journey,
    List<JourneySummaryResponse>? RecentJourneys);

public record TransitionDto(
    Guid Id,
    string Channel,
    string EventType,
    string? Description,
    Dictionary<string, object> Metadata,
    DateTime OccurredAt);

public record TransitionsResponse(
    Guid JourneyContextId,
    List<TransitionDto> Transitions);
