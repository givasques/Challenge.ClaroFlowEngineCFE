using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Lgpd.Dtos;
using ClaroFlowEngine.Api.Modules.Lgpd.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Lgpd.Controllers;

/// <summary>Direito ao esquecimento (Art. 18 LGPD, FASE 3.4) e revelação auditada de CPF (FASE 4.3).</summary>
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

    [HttpPost("customers/{customerId:guid}/reveal-cpf")]
    [Authorize]
    [EndpointSummary("Revelar CPF completo")]
    [EndpointDescription("Revela o CPF completo do cliente para o atendente, com motivo obrigatório. A consulta fica registrada (transição customer_cpf_revealed) com o usuário do painel e o motivo — o CPF nunca entra no registro de auditoria. Requer autenticação JWT do painel (Authorization: Bearer), qualquer perfil.")]
    [ProducesResponseType(typeof(CpfRevealResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevealCpf(Guid customerId, [FromBody] CpfRevealRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.RevealCpfAsync(customerId, request.Reason, cancellationToken);
        return Ok(result);
    }

    [HttpPost("customers/data-export")]
    [EndpointSummary("Exportar dados do cliente (portabilidade)")]
    [EndpointDescription("Gera um arquivo JSON com todos os dados pessoais e o histórico de atendimento do cliente, conforme o direito à portabilidade (LGPD, Art. 18, V). Painel (JWT) envia customer_id; App (X-Channel-Token) envia app_account e só consegue exportar os próprios dados. WhatsApp não tem acesso a este recurso.")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ExportData([FromBody] DataExportRequest request, CancellationToken cancellationToken)
    {
        var (jsonBytes, fileName) = await _service.ExportDataAsync(request, cancellationToken);
        return File(jsonBytes, "application/json", fileName);
    }
}
