using ClaroFlowEngine.Api.Modules.Panel.Services;

namespace ClaroFlowEngine.Api.Modules.Panel;

public static class PanelModuleExtensions
{
    public static IServiceCollection AddPanelModule(this IServiceCollection services)
    {
        services.AddScoped<IPanelService, PanelService>();
        return services;
    }
}
