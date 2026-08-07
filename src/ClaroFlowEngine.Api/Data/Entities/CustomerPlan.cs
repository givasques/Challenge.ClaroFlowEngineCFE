namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Associação entre cliente e plano contratado (histórico de planos).
/// </summary>
public class CustomerPlan
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid PlanId { get; set; }
    public bool Active { get; set; }
    public DateTime StartedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
}
