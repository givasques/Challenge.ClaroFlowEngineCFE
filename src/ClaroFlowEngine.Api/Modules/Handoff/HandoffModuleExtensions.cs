using ClaroFlowEngine.Api.Modules.Handoff.Services;

namespace ClaroFlowEngine.Api.Modules.Handoff;

public static class HandoffModuleExtensions
{
    public static IServiceCollection AddHandoffModule(this IServiceCollection services)
    {
        services.AddScoped<IHandoffService, HandoffService>();
        return services;
    }
}
