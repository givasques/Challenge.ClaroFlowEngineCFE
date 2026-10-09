using ClaroFlowEngine.Api.Common.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaroFlowEngine.Api.Modules.Auth.Controllers;

/// <summary>
/// Endpoint de validação da policy ManagerOnly (FASE 4.1, item B.3). A FASE 4.2 (dashboard do
/// gestor) usa a mesma policy de verdade — este endpoint só confirma que ela está funcionando.
/// </summary>
[ApiController]
[Route("manager")]
[Produces("application/json")]
[Authorize(Policy = "ManagerOnly")]
public class ManagerController : ControllerBase
{
    [HttpGet("ping")]
    [EndpointSummary("Ping do gestor")]
    [EndpointDescription("Confirma que o usuário logado tem o perfil 'manager'. Atendente recebe 403 forbidden_for_role.")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public IActionResult Ping() => Ok(new { status = "ok" });
}
