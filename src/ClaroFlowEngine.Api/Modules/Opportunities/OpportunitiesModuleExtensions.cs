using ClaroFlowEngine.Api.Modules.Opportunities.Services;

namespace ClaroFlowEngine.Api.Modules.Opportunities;

public static class OpportunitiesModuleExtensions
{
    public static IServiceCollection AddOpportunitiesModule(this IServiceCollection services)
    {
        services.AddScoped<IOpportunityDetectorService, OpportunityDetectorService>();
        services.AddScoped<IOpportunitiesService, OpportunitiesService>();
        return services;
    }
}
