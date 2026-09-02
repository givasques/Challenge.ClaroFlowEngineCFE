using ClaroFlowEngine.Api.Modules.Lgpd.Services;

namespace ClaroFlowEngine.Api.Modules.Lgpd;

public static class LgpdModuleExtensions
{
    public static IServiceCollection AddLgpdModule(this IServiceCollection services)
    {
        services.AddScoped<ILgpdService, LgpdService>();
        return services;
    }
}
