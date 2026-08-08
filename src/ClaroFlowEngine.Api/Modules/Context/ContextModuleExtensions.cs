using ClaroFlowEngine.Api.Modules.Context.Services;

namespace ClaroFlowEngine.Api.Modules.Context;

public static class ContextModuleExtensions
{
    public static IServiceCollection AddContextModule(this IServiceCollection services)
    {
        services.AddScoped<IContextService, ContextService>();
        return services;
    }
}
