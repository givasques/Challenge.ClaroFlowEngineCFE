namespace ClaroFlowEngine.Api.Common.Services;

/// <summary>
/// Usuário do painel autenticado na requisição atual, resolvido a partir das claims do JWT
/// (FASE 4.1). Null quando o chamador não é um usuário do painel (WhatsApp/App com X-Channel-Token).
/// Mesmo espírito do <see cref="ICurrentChannelAccessor"/> — permite que outros módulos (Panel,
/// Opportunities, Lgpd) saibam quem é o usuário sem depender de Modules/Auth.
/// </summary>
public interface ICurrentPanelUserAccessor
{
    Guid? UserId { get; }
    string? FullName { get; }
    string? Role { get; }
}
