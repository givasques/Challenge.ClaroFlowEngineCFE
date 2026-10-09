namespace ClaroFlowEngine.Api.Data.Entities;

/// <summary>
/// Usuário do painel do atendente (FASE 4.1) — não confundir com <see cref="Customer"/>, que é o cliente Claro.
/// </summary>
public class PanelUser
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
