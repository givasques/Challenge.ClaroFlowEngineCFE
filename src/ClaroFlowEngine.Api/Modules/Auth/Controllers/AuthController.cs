using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Modules.Auth.Dtos;
using ClaroFlowEngine.Api.Modules.Auth.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace ClaroFlowEngine.Api.Modules.Auth.Controllers;

/// <summary>Login, sessão e troca de senha dos usuários do painel do atendente (FASE 4.1).</summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _service;

    public AuthController(IAuthService service) => _service = service;

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [EndpointSummary("Login do painel")]
    [EndpointDescription("Autentica um usuário do painel (atendente ou gestor) por e-mail e senha, retornando um JWT. Bloqueia a conta por tempo limitado após tentativas seguidas erradas, e limita a taxa de tentativas por IP.")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    [EndpointSummary("Usuário logado")]
    [EndpointDescription("Retorna os dados do usuário do painel autenticado pelo JWT enviado. Usado pelo frontend para validar a sessão ao carregar a página.")]
    [ProducesResponseType(typeof(PanelUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.GetCurrentUserAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    [EndpointSummary("Trocar senha")]
    [EndpointDescription("Troca a senha do usuário logado, exigindo a senha atual. Nova senha deve ter no mínimo 8 caracteres, com letras e números, e ser diferente da atual.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _service.ChangePasswordAsync(userId, request, cancellationToken);
        return NoContent();
    }
}
