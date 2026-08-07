namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Vínculo entre um identificador de canal (telefone, login, CPF) e o cliente unificado.
/// </summary>
public class IdentityLink
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Identifier { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}
