using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Opportunities.Dtos;
using ClaroFlowEngine.Api.Modules.Opportunities.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Opportunities.Controllers;

/// <summary>Painel de Oportunidades (FASE 3.6) — detecção e ciclo de vida de leads comerciais gerados a partir de jornadas.</summary>
[ApiController]
[Route("opportunities")]
[Produces("application/json")]
public class OpportunitiesController : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private readonly IOpportunitiesService _service;

    public OpportunitiesController(IOpportunitiesService service) => _service = service;

    [HttpPost("detect")]
    [EndpointSummary("Detectar oportunidades")]
    [EndpointDescription("Executa as 4 regras de detecção (troca de plano abandonada, contestação abandonada, cliente engajado, cliente inativo) e persiste as oportunidades novas. Não duplica oportunidades já ativas do mesmo cliente/categoria. Requer header X-Channel-Token de painel.")]
    [ProducesResponseType(typeof(DetectOpportunitiesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Detect(CancellationToken cancellationToken)
    {
        var result = await _service.DetectAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    [EndpointSummary("Listar oportunidades")]
    [EndpointDescription("Lista oportunidades ordenadas por urgência (crítica primeiro) e depois por data de detecção. Por padrão retorna só status 'new' e 'contacted'; oportunidades expiradas (valid_until no passado) nunca aparecem. Requer header X-Channel-Token de painel.")]
    [ProducesResponseType(typeof(OpportunitiesListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? urgency,
        [FromQuery] int limit,
        [FromQuery] int offset,
        CancellationToken cancellationToken)
    {
        var clampedLimit = limit <= 0 ? DefaultLimit : Math.Min(limit, MaxLimit);
        var clampedOffset = Math.Max(0, offset);

        var result = await _service.ListAsync(status, category, urgency, clampedLimit, clampedOffset, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-as-contacted")]
    [EndpointSummary("Marcar oportunidade como abordada")]
    [EndpointDescription("Transição 'new' → 'contacted'. Requer header X-Channel-Token de painel.")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkAsContacted(Guid id, [FromBody] OpportunityActionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _service.MarkAsContactedAsync(id, request?.Notes, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-as-converted")]
    [EndpointSummary("Marcar oportunidade como convertida")]
    [EndpointDescription("Transição 'contacted' → 'converted'. Requer header X-Channel-Token de painel.")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkAsConverted(Guid id, [FromBody] OpportunityActionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _service.MarkAsConvertedAsync(id, request?.Notes, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/mark-as-not-relevant")]
    [EndpointSummary("Marcar oportunidade como não relevante")]
    [EndpointDescription("Transição 'contacted' → 'not_relevant'. Requer header X-Channel-Token de painel.")]
    [ProducesResponseType(typeof(OpportunityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkAsNotRelevant(Guid id, [FromBody] OpportunityActionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _service.MarkAsNotRelevantAsync(id, request?.Notes, cancellationToken);
        return Ok(result);
    }
}
