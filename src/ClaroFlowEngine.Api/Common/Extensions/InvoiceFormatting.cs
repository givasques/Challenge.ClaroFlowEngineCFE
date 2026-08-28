namespace ClaroFlowEngine.Api.Common.Extensions;

/// <summary>Formatação de referência de fatura ("Outubro/2026"), usada pelos módulos Invoices e Handoff.</summary>
public static class InvoiceFormatting
{
    private static readonly string[] MonthNamesPtBr =
    [
        "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho",
        "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro",
    ];

    public static string ReferenceLabel(DateOnly referenceMonth) => $"{MonthNamesPtBr[referenceMonth.Month - 1]}/{referenceMonth.Year}";
}
