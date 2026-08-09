using ClaroFlowEngine.Api.Data.Entities;

namespace ClaroFlowEngine.Api.Common.Services;

/// <summary>
/// Regra de expiração reativa (UC08). Compartilhada entre Context e Handoff — ambos os módulos
/// tocam jornadas potencialmente inativas e precisam aplicar a mesma checagem.
/// </summary>
public interface IJourneyExpirationService
{
    /// <summary>
    /// Se a jornada está open e passou do TTL de inatividade, marca como expired e registra a transição.
    /// Retorna true se a jornada foi mutada — o chamador deve persistir (SaveChangesAsync) em seguida.
    /// </summary>
    bool TryExpireIfInactive(JourneyContext journey);
}
