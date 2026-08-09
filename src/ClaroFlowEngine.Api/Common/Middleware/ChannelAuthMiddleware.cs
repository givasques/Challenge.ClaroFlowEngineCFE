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
    // Mapeamento token -> canal. Fica aqui (não em appsettings) porque AllowedChannelTokens é documentado
    // na spec técnica como uma allowlist simples; esta é uma extensão interna para resolver ICurrentChannelAccessor.
    private static readonly Dictionary<string, string> TokenChannelMap = new()
    {
        ["fake-whatsapp-token"] = Channels.Whatsapp,
        ["fake-app-token"] = Channels.App,
        ["fake-panel-token"] = Channels.Panel,
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
        path.StartsWithSegments("/plans");
}

public static class ChannelAuthMiddlewareExtensions
{
    public static IApplicationBuilder UseChannelAuth(this IApplicationBuilder app)
        => app.UseMiddleware<ChannelAuthMiddleware>();
}
