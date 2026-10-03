namespace ClaroFlowEngine.Api.Modules.Lgpd.Dtos;

/// <summary>Corpo de POST /customers/{customerId}/reveal-cpf — motivo obrigatório (FASE 4.3, item A.4).</summary>
public record CpfRevealRequest(string Reason);

/// <summary>Resposta de POST /customers/{customerId}/reveal-cpf — CPF completo, válido só para esta chamada.</summary>
public record CpfRevealResponse(string Cpf, DateTime RevealedAt);
