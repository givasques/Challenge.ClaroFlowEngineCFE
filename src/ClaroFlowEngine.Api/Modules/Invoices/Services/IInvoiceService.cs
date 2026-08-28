using ClaroFlowEngine.Api.Modules.Invoices.Dtos;

namespace ClaroFlowEngine.Api.Modules.Invoices.Services;

public interface IInvoiceService
{
    Task<InvoiceListResponse> GetByCustomerAsync(Guid customerId, int limit, CancellationToken cancellationToken);

    Task<InvoiceDetailResponse> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken);
}
