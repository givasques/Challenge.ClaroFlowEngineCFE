using ClaroFlowEngine.Api.Modules.CustomerInsights.Providers;
using ClaroFlowEngine.Api.Modules.CustomerInsights.Services;

namespace ClaroFlowEngine.Api.Modules.CustomerInsights;

public static class CustomerInsightsModuleExtensions
{
    public static IServiceCollection AddCustomerInsightsModule(this IServiceCollection services)
    {
        services.AddScoped<ICustomerHistoryCollector, CustomerHistoryCollector>();
        services.AddScoped<ICustomerSummaryProvider, RuleBasedSummaryProvider>();
        services.AddScoped<ICustomerSummaryProvider, OpenAiCompatibleSummaryProvider>();
        services.AddHttpClient(OpenAiCompatibleSummaryProvider.HttpClientName);
        services.AddScoped<ICustomerSummaryService, CustomerSummaryService>();
        services.AddSingleton<IAiSummaryRateLimiter, AiSummaryRateLimiter>();
        return services;
    }
}
