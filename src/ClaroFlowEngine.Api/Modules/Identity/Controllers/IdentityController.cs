using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Identity.Dtos;
using ClaroFlowEngine.Api.Modules.Identity.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Identity.Controllers;

/// <summary>Resolução de identidade unificada entre canais (UC02/UC03).</summary>
[ApiController]
[Route("identity")]
[Produces("application/json")]
public class IdentityController : ControllerBase
{
    private readonly IIdentityService _service;

    public IdentityController(IIdentityService service) => _service = service;

    /// <summary>
    /// Resolve a identidade unificada para um par (canal, identificador).
    /// Cria o cliente e/ou o vínculo de identidade quando necessário.
    /// </summary>
    /// <response code="200">Identidade resolvida (encontrada ou criada) com sucesso.</response>
    /// <response code="400">Canal ou identificador em formato inválido.</response>
    /// <response code="404">CPF não cadastrado e sem dica de nome para criação.</response>
    [HttpPost("resolve")]
    [ProducesResponseType(typeof(ResolveIdentityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve([FromBody] ResolveIdentityRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.ResolveAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Consulta somente leitura de uma identidade já vinculada (não cria nada).</summary>
    /// <response code="200">Identidade encontrada.</response>
    /// <response code="400">Canal ou identificador em formato inválido.</response>
    /// <response code="404">Identidade não encontrada para o par informado.</response>
    [HttpGet("resolve")]
    [ProducesResponseType(typeof(ResolveIdentityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResolve(
        [FromQuery] string channel, [FromQuery] string identifier, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(channel, identifier, cancellationToken);
        return Ok(result);
    }
}
