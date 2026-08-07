namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Contexto de uma jornada do cliente: intenção, etapa atual e dados coletados.
/// É o núcleo do CFE — persiste o estado da conversa entre canais.
/// </summary>
public class JourneyContext
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string OriginChannel { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public ICollection<JourneyTransition> Transitions { get; set; } = new List<JourneyTransition>();
    public ICollection<HandoffToken> HandoffTokens { get; set; } = new List<HandoffToken>();
}
