using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Handoff.Dtos;
using ClaroFlowEngine.Api.Modules.Handoff.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Handoff.Controllers;

/// <summary>Geração e resolução de deep links para handoff entre canais (UC05, UC06).</summary>
[ApiController]
[Route("handoff")]
[Produces("application/json")]
public class HandoffController : ControllerBase
{
    private readonly IHandoffService _service;

    public HandoffController(IHandoffService service) => _service = service;

    /// <summary>Gera um deep link com token de validade limitada para retomar a jornada em outro canal.</summary>
    /// <response code="201">Token e deep link gerados.</response>
    /// <response code="400">Canal de destino inválido.</response>
    /// <response code="404">Jornada não encontrada.</response>
    /// <response code="409">Jornada não está aberta.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GenerateHandoffResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate([FromBody] GenerateHandoffRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.GenerateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Resolve um token de handoff e retorna o contexto completo da jornada para o canal de destino.
    /// Rota fixada em <c>/context/resolve</c> (fora do prefixo "handoff") para casar com o contrato documentado
    /// na spec técnica — a responsabilidade é do módulo Handoff, mas a URL é histórica do módulo Context.
    /// </summary>
    /// <response code="200">Token válido, contexto retornado.</response>
    /// <response code="404">Token inexistente.</response>
    /// <response code="410">Token expirado/já usado, ou jornada expirada/encerrada.</response>
    [HttpGet("/context/resolve")]
    [ProducesResponseType(typeof(ResolveTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status410Gone)]
    public async Task<IActionResult> ResolveToken(
        [FromQuery] string token, [FromQuery] string? identifier, CancellationToken cancellationToken)
    {
        var result = await _service.ResolveTokenAsync(token, identifier, cancellationToken);
        return Ok(result);
    }
}
