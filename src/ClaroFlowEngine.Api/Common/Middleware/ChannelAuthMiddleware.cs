using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Common.Errors;
using ClaroFlowEngine.Api.Common.Services;
using ClaroFlowEngine.Api.Configuration;
using Microsoft.Extensions.Options;

namespace ClaroFlowEngine.Api.Common.Middleware;

// MOCK — em produção, cada canal teria um JWT/serviço de identidade próprio.
// Este middleware simula a intenção arquitetural (autenticação por canal) sem custo de setup de auth real.
public class ChannelAuthMiddleware
{
    // fake-panel-token removido (FASE 4.1, item B.1) — painel só autentica via JWT a partir desta fase.
    private static readonly Dictionary<string, string> TokenChannelMap = new()
    {
        ["fake-whatsapp-token"] = Channels.Whatsapp,
        ["fake-app-token"] = Channels.App,
    };

    private readonly RequestDelegate _next;
    private readonly HashSet<string> _allowedTokens;

    public ChannelAuthMiddleware(RequestDelegate next, IOptions<CfeOptions> cfeOptions)
    {
        _next = next;
        _allowedTokens = new HashSet<string>(cfeOptions.Value.AllowedChannelTokens);
    }

    public async Task InvokeAsync(HttpContext context, ICurrentChannelAccessor currentChannel)
    {
        if (IsPublicRoute(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // JWT válido (painel, FASE 4.1 — Bloco B.1) dispensa X-Channel-Token: UseAuthentication() já
        // rodou antes deste middleware, então context.User já reflete o token, se houver um válido.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            currentChannel.Channel = Channels.Panel;
            await _next(context);
            return;
        }

        var token = context.Request.Headers["X-Channel-Token"].FirstOrDefault();
        if (string.IsNullOrEmpty(token) || !_allowedTokens.Contains(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(
                new ApiError("invalid_channel_token", "Token de canal ausente ou não autorizado."));
            return;
        }

        currentChannel.Channel = TokenChannelMap.GetValueOrDefault(token);

        await _next(context);
    }

    private static bool IsPublicRoute(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/swagger") ||
        path.StartsWithSegments("/plans") ||
        // Canais simulados (HTML/CSS/JS) servidos pela própria API no modo "full" (ver docker-compose.full.yml).
        // Arquivos estáticos não fazem sentido exigir X-Channel-Token — são as próprias páginas dos canais.
        path.StartsWithSegments("/channels") ||
        // Módulo de autenticação do painel (FASE 4.1) — proteção é inteiramente via [Authorize]/JWT,
        // não via X-Channel-Token; sem isso, uma chamada sem token nenhum seria rejeitada por este
        // middleware com invalid_channel_token em vez do invalid_or_expired_session esperado (A.5).
        path.StartsWithSegments("/auth");
}

public static class ChannelAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseChannelAuth(this IApplicationBuilder app)
        => app.UseMiddleware<ChannelAuthMiddleware>();
}
