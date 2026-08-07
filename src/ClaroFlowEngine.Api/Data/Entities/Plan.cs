namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Plano de dados disponível para contratação/troca.
/// </summary>
public class Plan
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DataGb { get; set; }
    public int MonthlyPriceCents { get; set; }
    public bool Active { get; set; }

    public ICollection<CustomerPlan> CustomerPlans { get; set; } = new List<CustomerPlan>();
}
