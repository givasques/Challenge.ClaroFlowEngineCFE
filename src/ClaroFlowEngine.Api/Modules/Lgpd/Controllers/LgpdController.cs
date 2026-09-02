using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Lgpd.Dtos;
using ClaroFlowEngine.Api.Modules.Lgpd.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Lgpd.Controllers;

/// <summary>Direito ao esquecimento (Art. 18 LGPD) — FASE 3.4.</summary>
[ApiController]
[Produces("application/json")]
public class LgpdController : ControllerBase
{
    private readonly ILgpdService _service;

    public LgpdController(ILgpdService service) => _service = service;

    [HttpPost("customers/{cpf}/right-to-be-forgotten")]
    [EndpointSummary("Direito ao esquecimento")]
    [EndpointDescription("Anonimiza os dados pessoais identificáveis do cliente (nome, CPF, identificadores de canal) mantendo o histórico de jornadas e transições íntegro para auditoria. Operação irreversível e idempotente (uma segunda chamada retorna 409). Requer header X-Channel-Token de App (o próprio cliente) ou Painel (atendente em nome do cliente) — outros canais são rejeitados.")]
    [ProducesResponseType(typeof(RightToBeForgottenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExerciseRightToBeForgotten(string cpf, CancellationToken cancellationToken)
    {
        var result = await _service.ExerciseRightToBeForgottenAsync(cpf, cancellationToken);
        return Ok(result);
    }
}
