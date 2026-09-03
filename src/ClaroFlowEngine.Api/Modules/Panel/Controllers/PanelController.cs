using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Panel.Dtos;
using ClaroFlowEngine.Api.Modules.Panel.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Panel.Controllers;

/// <summary>Dados agregados para o menu lateral do painel do atendente: jornadas ativas e métricas operacionais (FASE 3.2).</summary>
[ApiController]
[Produces("application/json")]
public class PanelController : ControllerBase
{
    private readonly IPanelService _service;

    public PanelController(IPanelService service) => _service = service;

    [HttpGet("journeys/active")]
    [EndpointSummary("Jornadas ativas")]
    [EndpointDescription("Retorna todas as jornadas com status 'open', mais recentes primeiro, para a tela \"Jornadas Ativas\" do painel do atendente. Requer header X-Channel-Token válido.")]
    [ProducesResponseType(typeof(ActiveJourneysResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetActiveJourneys(
        [FromQuery(Name = "include_escalated")] bool includeEscalated, CancellationToken cancellationToken)
    {
        var result = await _service.GetActiveJourneysAsync(includeEscalated, cancellationToken);
        return Ok(result);
    }

    [HttpGet("metrics/summary")]
    [EndpointSummary("Métricas operacionais")]
    [EndpointDescription("Retorna 4 métricas agregadas (TMA mediano, jornadas hoje, taxa de conclusão, canal mais usado) para a tela \"Métricas\" do painel do atendente. TMA, taxa de conclusão e canal mais usado consideram os últimos 30 dias; jornadas hoje considera apenas o dia corrente. Campos individuais retornam null quando não há dados suficientes no período. Requer header X-Channel-Token válido.")]
    [ProducesResponseType(typeof(MetricsSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMetricsSummary(CancellationToken cancellationToken)
    {
        var result = await _service.GetMetricsSummaryAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("journeys/{id:guid}/conclude")]
    [EndpointSummary("Concluir jornada pelo painel")]
    [EndpointDescription("Encerra uma jornada 'open' com uma categoria de desfecho padronizada, registrada pelo atendente. Requer header X-Channel-Token de painel.")]
    [ProducesResponseType(typeof(ConcludeJourneyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConcludeJourney(Guid id, [FromBody] ConcludeJourneyRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.ConcludeJourneyAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("journeys/{id:guid}/escalate")]
    [EndpointSummary("Escalar jornada para outra área")]
    [EndpointDescription("Transfere uma jornada 'open' para outra área (mockada), sem fechá-la — ela permanece registrada aguardando desfecho externo. Requer header X-Channel-Token de painel.")]
    [ProducesResponseType(typeof(EscalateJourneyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EscalateJourney(Guid id, [FromBody] EscalateJourneyRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.EscalateJourneyAsync(id, request, cancellationToken);
        return Ok(result);
    }
}
