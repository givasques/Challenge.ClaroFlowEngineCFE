namespace ClaroFlowEngine.Api.Modules.Auth.Dtos;

/// <summary>Payload de login do painel do atendente (FASE 4.1).</summary>
public record LoginRequest(string Email, string Password);

/// <summary>Payload de troca de senha do usuário logado.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
