namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Fatura mensal de um cliente. Usada pela intenção "contestação de cobrança indevida" (ETAPA 2, Passo C).
/// </summary>
public class Invoice
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public DateOnly ReferenceMonth { get; set; }
    public DateOnly DueDate { get; set; }
    public int TotalCents { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}
