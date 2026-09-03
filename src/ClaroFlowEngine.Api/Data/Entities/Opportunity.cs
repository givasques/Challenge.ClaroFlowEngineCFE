namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Oportunidade comercial detectada a partir de dados históricos de jornadas (FASE 3.6).
/// Não é uma jornada — é um insight derivado, com ciclo de vida próprio (new → contacted → converted/not_relevant).
/// </summary>
public class Opportunity
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    /// <summary>Jornada que originou a detecção. Pode ser null se a jornada de referência for removida no futuro.</summary>
    public Guid? TriggeringJourneyId { get; set; }

    /// <summary>Dados contextuais específicos da categoria (plano de interesse, motivo de contestação, etc.).</summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    public DateTime DetectedAt { get; set; }
    public DateTime ValidUntil { get; set; }
    public DateTime? ContactedAt { get; set; }
    public string? ContactedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }

    public Customer Customer { get; set; } = null!;
    public JourneyContext? TriggeringJourney { get; set; }
}
