namespace ClaroFlowEngine.Api.Modules.Auth.Dtos;

/// <summary>Dados públicos do usuário do painel, nunca incluindo o hash da senha.</summary>
public record PanelUserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string RoleLabel,
    DateTime? LastLoginAt);

/// <summary>Resposta de login bem-sucedido.</summary>
public record LoginResponse(string AccessToken, DateTime ExpiresAt, PanelUserDto User);
