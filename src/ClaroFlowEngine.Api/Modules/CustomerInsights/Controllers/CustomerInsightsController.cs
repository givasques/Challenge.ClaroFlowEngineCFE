using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Dtos;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights.Controllers;

/// <summary>Resumo do cliente com IA para o painel do atendente (FASE 4.4).</summary>
[ApiController]
[Produces("application/json")]
[Authorize]
public class CustomerInsightsController : ControllerBase
{
    private readonly ICustomerSummaryService _service;

    public CustomerInsightsController(ICustomerSummaryService service) => _service = service;

    [HttpPost("customers/{customerId:guid}/ai-summary")]
    [EndpointSummary("Resumo do cliente com IA")]
    [EndpointDescription("Gera um resumo do histórico do cliente para o atendente: resumo, pontos de atenção e sugestão de abordagem. O modelo recebe só um retrato minimizado (sem nome, CPF, telefone, conta do App ou texto livre). Se a IA falhar, demorar ou não estiver configurada, a resposta é o resumo por regras, com fallback_reason preenchido: falha da IA não é erro HTTP. Resumos da IA ficam em cache enquanto os dados não mudam; force_refresh ignora o cache. Gerações contam no limite por atendente; respostas do cache não. Requer autenticação JWT do painel (Authorization: Bearer).")]
    [ProducesResponseType(typeof(CustomerSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GenerateSummary(
        Guid customerId, [FromBody] CustomerSummaryRequest? request, CancellationToken cancellationToken)
    {
        var result = await _service.GenerateAsync(customerId, request?.ForceRefresh ?? false, cancellationToken);
        return Ok(result);
    }
}
