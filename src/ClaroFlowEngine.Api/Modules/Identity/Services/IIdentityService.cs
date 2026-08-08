using ClaroFlowEngine.Api.Modules.Identity.Dtos;

namespace ClaroFlowEngine.Api.Modules.Identity.Services;

public interface IIdentityService
{
    /// <summary>Resolve a identidade unificada para um par (canal, identificador), criando cliente/link se necessário.</summary>
    Task<ResolveIdentityResponse> ResolveAsync(ResolveIdentityRequest request, CancellationToken cancellationToken);

    /// <summary>Versão somente leitura: consulta um link já existente, sem criar nada.</summary>
    Task<ResolveIdentityResponse> GetAsync(string channel, string identifier, CancellationToken cancellationToken);
}
