namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Registro de auditoria de cada evento relevante ocorrido em uma jornada.
/// É o histórico consultado pelo painel do atendente.
/// </summary>
public class JourneyTransition
{
    public Guid Id { get; set; }
    public Guid JourneyContextId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public DateTime OccurredAt { get; set; }

    public JourneyContext JourneyContext { get; set; } = null!;
}
