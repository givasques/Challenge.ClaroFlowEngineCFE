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

/// <summary>
/// Cópia local e enxuta do resumo de cliente — evita acoplar o módulo Context ao Identity.
/// Phone, CurrentPlan e os campos agregados (CustomerSince, PreferredChannel, JourneyCounts) são
/// enriquecidos sob demanda (usados pelo Painel do Atendente — Fase 8 e ETAPA 2, Passo B);
/// nulos/zerados quando não há dado correspondente.
/// </summary>
public record CustomerSummaryDto(
    Guid Id,
    string FullName,
    string Cpf,
    string? Phone,
    PlanInfoDto? CurrentPlan,
    DateTime CustomerSince,
    string? PreferredChannel,
    JourneyCountsDto JourneyCounts);

public record PlanInfoDto(string Code, string Name, int MonthlyPriceCents);

/// <summary>Contagem de jornadas do cliente por desfecho (ETAPA 2, Passo B, item 5.2).</summary>
public record JourneyCountsDto(int Total, int Concluded, int Abandoned, int Expired);

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
