using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Context.Dtos;
using ClaroFlowEngine.Api.Modules.Context.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Context.Controllers;

/// <summary>Ciclo de vida da jornada: abertura, atualização, consulta, histórico e encerramento (UC01, UC04-UC09).</summary>
[ApiController]
[Route("context")]
[Produces("application/json")]
public class ContextController : ControllerBase
{
    private readonly IContextService _service;

    public ContextController(IContextService service) => _service = service;

    /// <summary>Abre uma nova jornada, ou retorna a já ativa para o mesmo cliente/intenção (idempotência).</summary>
    /// <response code="201">Jornada criada.</response>
    /// <response code="200">Jornada já ativa retornada sem duplicar.</response>
    /// <response code="400">Payload inválido.</response>
    /// <response code="404">Cliente não encontrado.</response>
    [HttpPost("open")]
    [ProducesResponseType(typeof(JourneySummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(JourneySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Open([FromBody] OpenJourneyRequest request, CancellationToken cancellationToken)
    {
        var (response, wasCreated) = await _service.OpenAsync(request, cancellationToken);
        return wasCreated
            ? CreatedAtAction(nameof(GetById), new { id = response.Id }, response)
            : Ok(response);
    }

    /// <summary>Atualiza a etapa atual e/ou os dados coletados de uma jornada aberta.</summary>
    /// <response code="200">Jornada atualizada.</response>
    /// <response code="400">Payload inválido.</response>
    /// <response code="404">Jornada não encontrada.</response>
    /// <response code="409">Jornada não está aberta.</response>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(JourneySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Patch(Guid id, [FromBody] PatchContextRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retorna o estado atual de uma jornada, incluindo dados resumidos do cliente.</summary>
    /// <response code="200">Jornada encontrada.</response>
    /// <response code="404">Jornada não encontrada.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JourneyDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retorna a jornada aberta do cliente (se houver) e, opcionalmente, o histórico de jornadas fechadas.</summary>
    /// <response code="200">Cliente encontrado (com ou sem jornada ativa).</response>
    /// <response code="404">Cliente não encontrado, ou sem jornada ativa e sem histórico solicitado.</response>
    [HttpGet("customer/{customerId:guid}")]
    [ProducesResponseType(typeof(ActiveJourneyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActiveByCustomer(
        Guid customerId,
        [FromQuery(Name = "include_history")] bool includeHistory,
        [FromQuery(Name = "history_limit")] int? historyLimit,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetActiveByCustomerAsync(customerId, includeHistory, historyLimit ?? 5, cancellationToken);
        return Ok(result);
    }

    /// <summary>Retorna o histórico completo de transições da jornada, mais recente primeiro.</summary>
    /// <response code="200">Histórico retornado (pode ser vazio).</response>
    /// <response code="404">Jornada não encontrada.</response>
    [HttpGet("{id:guid}/transitions")]
    [ProducesResponseType(typeof(TransitionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransitions(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetTransitionsAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Encerra uma jornada aberta com o desfecho informado (concluded/abandoned).</summary>
    /// <response code="200">Jornada encerrada.</response>
    /// <response code="400">Payload inválido.</response>
    /// <response code="404">Jornada não encontrada.</response>
    /// <response code="409">Jornada já não está aberta.</response>
    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(typeof(JourneySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseJourneyRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CloseAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
