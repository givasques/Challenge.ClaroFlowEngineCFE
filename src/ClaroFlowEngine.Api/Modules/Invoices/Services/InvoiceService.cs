using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Extensions;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Modules.Invoices.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Modules.Invoices.Services;

public class InvoiceService : IInvoiceService
{
    private readonly CfeDbContext _db;

    public InvoiceService(CfeDbContext db) => _db = db;

    public async Task<InvoiceListResponse> GetByCustomerAsync(Guid customerId, int limit, CancellationToken cancellationToken)
    {
        var customerExists = await _db.Customers.AsNoTracking().AnyAsync(c => c.Id == customerId, cancellationToken);
        if (!customerExists)
            throw new NotFoundException("customer_not_found", $"Cliente {customerId} não encontrado.");

        var invoices = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.CustomerId == customerId)
            .OrderByDescending(i => i.ReferenceMonth)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var summaries = invoices
            .Select(i => new InvoiceSummaryDto(
                i.Id, i.ReferenceMonth, InvoiceFormatting.ReferenceLabel(i.ReferenceMonth), i.DueDate, i.TotalCents, i.Status))
            .ToList();

        return new InvoiceListResponse(customerId, summaries);
    }

    public async Task<InvoiceDetailResponse> GetByIdAsync(Guid invoiceId, CancellationToken cancellationToken)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == invoiceId, cancellationToken)
            ?? throw new NotFoundException("invoice_not_found", $"Fatura {invoiceId} não encontrada.");

        var items = invoice.Items
            .OrderBy(it => it.Sequence)
            .Select(it => new InvoiceItemDto(it.Id, it.Sequence, it.Description, it.Category, it.AmountCents))
            .ToList();

        return new InvoiceDetailResponse(
            invoice.Id, invoice.CustomerId, invoice.ReferenceMonth, InvoiceFormatting.ReferenceLabel(invoice.ReferenceMonth),
            invoice.DueDate, invoice.TotalCents, invoice.Status, items);
    }
}
