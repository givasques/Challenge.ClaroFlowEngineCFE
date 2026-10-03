namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Token de handoff (deep link) usado para retomar uma jornada em outro canal.
/// Single-use, com validade de 30 minutos (configurável).
/// </summary>
public class HandoffToken
{
    public Guid Id { get; set; }
    public Guid JourneyContextId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string TargetChannel { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Tentativas de abrir este link com uma conta que não é a dona da jornada (FASE 4.3, item B.4). Reseta nunca — o token é revogado ao atingir o limite.</summary>
    public int OwnerMismatchAttempts { get; set; }

    public JourneyContext JourneyContext { get; set; } = null!;
}
