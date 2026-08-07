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

    public ICollection<IdentityLink> IdentityLinks { get; set; } = new List<IdentityLink>();
    public ICollection<CustomerPlan> CustomerPlans { get; set; } = new List<CustomerPlan>();
    public ICollection<JourneyContext> JourneyContexts { get; set; } = new List<JourneyContext>();
}
