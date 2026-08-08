using ClaroFlowEngine.Api.Modules.Identity.Services;

namespace ClaroFlowEngine.Api.Modules.Identity;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services)
    {
        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}
