namespace ClaroFlowEngine.Api.Modules.Invoices.Dtos;

/// <summary>Resposta de GET /invoices/customer/{customerId} — últimas faturas do cliente, mais recente primeiro.</summary>
public record InvoiceListResponse(Guid CustomerId, List<InvoiceSummaryDto> Invoices);

public record InvoiceSummaryDto(
    Guid Id,
    DateOnly ReferenceMonth,
    string ReferenceLabel,
    DateOnly DueDate,
    int TotalCents,
    string Status);

/// <summary>Resposta de GET /invoices/{invoiceId} — inclui os itens de linha.</summary>
public record InvoiceDetailResponse(
    Guid Id,
    Guid CustomerId,
    DateOnly ReferenceMonth,
    string ReferenceLabel,
    DateOnly DueDate,
    int TotalCents,
    string Status,
    List<InvoiceItemDto> Items);

public record InvoiceItemDto(Guid Id, int Sequence, string Description, string Category, int AmountCents);
