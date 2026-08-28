namespace ClaroFlowEngine.Api.Common.Contracts;

/// <summary>Estados válidos de uma fatura (ETAPA 2, Passo C).</summary>
public static class InvoiceStatus
{
    public const string Issued = "issued";
    public const string Paid = "paid";
    public const string Overdue = "overdue";
    public const string Contested = "contested";
}
