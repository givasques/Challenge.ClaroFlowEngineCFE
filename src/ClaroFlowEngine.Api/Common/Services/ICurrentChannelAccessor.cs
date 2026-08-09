namespace ClaroFlowEngine.Api.Common.Services;

/// <summary>
/// Canal do chamador atual, resolvido pelo <c>ChannelAuthMiddleware</c> a partir do header
/// <c>X-Channel-Token</c>. Scoped por request — permite que services (Context, Handoff) saibam
/// qual canal está de fato fazendo a chamada, sem precisar desse dado no corpo de cada requisição.
/// </summary>
public interface ICurrentChannelAccessor
{
    string? Channel { get; set; }
}
