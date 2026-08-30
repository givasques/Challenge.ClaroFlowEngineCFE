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
    public async Task<IActionResult> GetActiveJourneys(CancellationToken cancellationToken)
    {
        var result = await _service.GetActiveJourneysAsync(cancellationToken);
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
}
