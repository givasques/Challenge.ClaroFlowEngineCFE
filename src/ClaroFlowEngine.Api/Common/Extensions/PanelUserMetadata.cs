using ClaroFlowEngine.Api.Common.Services;
using System.Text.Json;

namespace ClaroFlowEngine.Api.Common.Extensions;

/// <summary>
/// Acrescenta quem (usuário do painel) realizou uma ação ao metadata de uma transição (FASE 4.1,
/// item B.4). Vive em Common porque é usada por mais de um módulo (Context, Panel, Lgpd).
/// </summary>
public static class PanelUserMetadata
{
    /// <summary>
    /// Mescla <paramref name="baseMetadata"/> (objeto anônimo ou dicionário, mesmo formato aceito por
    /// <c>ITransitionRecorder.Record</c>) com panel_user_id/name/role, só quando há um usuário do
    /// painel autenticado na requisição atual (chamadas de WhatsApp/App nunca têm um).
    /// </summary>
    public static Dictionary<string, object> Merge(object? baseMetadata, ICurrentPanelUserAccessor currentPanelUser)
    {
        var dict = baseMetadata switch
        {
            null => new Dictionary<string, object>(),
            Dictionary<string, object> d => new Dictionary<string, object>(d),
            _ => JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(baseMetadata)) ?? new(),
        };

        if (currentPanelUser.UserId is Guid userId)
        {
            dict["panel_user_id"] = userId;
            dict["panel_user_name"] = currentPanelUser.FullName ?? "";
            dict["panel_user_role"] = currentPanelUser.Role ?? "";
        }

        return dict;
    }
}
