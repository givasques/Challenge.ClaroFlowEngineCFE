namespace ClaroFlowEngine.Api.Modules.Lgpd.Dtos;

/// <summary>
/// Corpo de POST /customers/data-export — o campo preenchido depende do canal que pede (FASE 4.3, item C.2):
/// painel (JWT) envia CustomerId; App (X-Channel-Token) envia AppAccount. Enviar os dois, ou nenhum, é inválido.
/// </summary>
public record DataExportRequest(Guid? CustomerId = null, string? AppAccount = null);

/// <summary>Portabilidade dos dados do cliente (LGPD, Art. 18, V) — conteúdo completo do arquivo exportado.</summary>
public record CustomerDataExport(
    string ExportFormatVersion,
    DateTime GeneratedAt,
    string LegalBasis,
    string Controller,
    CustomerExportDto Customer,
    List<ChannelIdentityExportDto> ChannelIdentities,
    List<JourneyExportDto> Journeys,
    List<CustomerPlanExportDto> CustomerPlans,
    List<InvoiceExportDto> Invoices,
    List<OpportunityExportDto> CommercialOpportunities);

public record CustomerExportDto(Guid Id, string FullName, string Cpf, DateTime CreatedAt);

public record ChannelIdentityExportDto(string Channel, string ChannelLabel, string Identifier, DateTime LinkedAt);

/// <summary>Sem Metadata de propósito (FASE 4.3, item C.3) — é onde moram panel_user_* e outros ids internos de atendente.</summary>
public record TransitionHistoryExportDto(string EventType, string? Description, string Channel, DateTime OccurredAt);

public record JourneyExportDto(
    Guid Id,
    string Intent,
    string IntentLabel,
    string Status,
    string StatusLabel,
    string OriginChannel,
    DateTime CreatedAt,
    DateTime? ClosedAt,
    Dictionary<string, object> Data,
    List<TransitionHistoryExportDto> History);

public record CustomerPlanExportDto(string PlanCode, string PlanName, int MonthlyPriceCents, bool Active, DateTime StartedAt);

public record InvoiceItemExportDto(string Description, string Category, int AmountCents);

public record InvoiceExportDto(
    DateOnly ReferenceMonth,
    string ReferenceLabel,
    DateOnly DueDate,
    int TotalCents,
    string Status,
    List<InvoiceItemExportDto> Items);

/// <summary>Só rótulos e data (FASE 4.3, item C.3) — oportunidade é dado inferido, não aparecem ids internos nem urgência/metadata.</summary>
public record OpportunityExportDto(string CategoryLabel, string StatusLabel, DateTime DetectedAt);
