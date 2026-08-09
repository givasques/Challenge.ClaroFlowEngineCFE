using ClaroFlowEngine.Api.Modules.Handoff.Dtos;
using ClaroFlowEngine.Api.Modules.Handoff.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Handoff.Controllers;

/// <summary>Listagem de planos ativos — útil para o chat simulado mostrar as opções (spec-tecnica §5.5).</summary>
[ApiController]
[Route("plans")]
[Produces("application/json")]
public class PlansController : ControllerBase
{
    private readonly IHandoffService _service;

    public PlansController(IHandoffService service) => _service = service;

    /// <response code="200">Lista de planos ativos.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PlansResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivePlans(CancellationToken cancellationToken)
    {
        var result = await _service.GetActivePlansAsync(cancellationToken);
        return Ok(result);
    }
}
