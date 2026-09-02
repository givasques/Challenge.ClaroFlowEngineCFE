namespace ClaroFlowEngine.Api.Common.Services;

/// <summary>
/// Registro centralizado de transições de jornada. Usado por qualquer módulo que
/// altere o estado de uma jornada (Context, Handoff), garantindo rastreabilidade consistente.
/// </summary>
public interface ITransitionRecorder
{
    /// <summary>
    /// Adiciona a transição ao change tracker do DbContext, sem persistir imediatamente —
    /// o chamador decide quando salvar (geralmente junto com a mudança de estado que originou o evento,
    /// dentro da mesma transação).
    /// </summary>
    /// <param name="journeyContextId">
    /// Null para transições de auditoria que não pertencem a uma jornada específica
    /// (ex: direito ao esquecimento, FASE 3.4) — o cliente pode não ter nenhuma jornada aberta.
    /// </param>
    void Record(Guid? journeyContextId, string channel, string eventType, string? description = null, object? metadata = null);
}
