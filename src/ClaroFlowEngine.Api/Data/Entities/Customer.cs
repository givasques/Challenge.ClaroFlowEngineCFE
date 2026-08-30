namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Cliente Claro, identificado de forma única pelo CPF.
/// </summary>
public class Customer
{
    public Guid Id { get; set; }
    public string Cpf { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>Dia do mês (1-28) de vencimento da fatura. Placeholder mockado (FASE 3, item C.3).</summary>
    public int? BillingDueDay { get; set; }

    /// <summary>Segmento comercial do cliente (ex: "Pessoa Física", "Premium"). Placeholder mockado (FASE 3, item C.3).</summary>
    public string? Segment { get; set; }

    public ICollection<IdentityLink> IdentityLinks { get; set; } = new List<IdentityLink>();
    public ICollection<CustomerPlan> CustomerPlans { get; set; } = new List<CustomerPlan>();
    public ICollection<JourneyContext> JourneyContexts { get; set; } = new List<JourneyContext>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
