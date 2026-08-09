using ClaroFlowEngine.Api.Modules.Handoff.Dtos;

namespace ClaroFlowEngine.Api.Modules.Handoff.Services;

public interface IHandoffService
{
    Task<GenerateHandoffResponse> GenerateAsync(GenerateHandoffRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Resolve um token de handoff. <paramref name="identifier"/> é opcional: quando informado pelo canal de
    /// destino (ex: login do App), é usado para vincular a identidade a esse canal (UC06, passo 5).
    /// </summary>
    Task<ResolveTokenResponse> ResolveTokenAsync(string token, string? identifier, CancellationToken cancellationToken);

    Task<PlansResponse> GetActivePlansAsync(CancellationToken cancellationToken);
}
