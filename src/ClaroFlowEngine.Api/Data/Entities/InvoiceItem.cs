namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Item de linha de uma fatura (mensalidade, adicional, taxa, tarifa). ETAPA 2, Passo C.
/// </summary>
public class InvoiceItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int AmountCents { get; set; }
    public int Sequence { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
