namespace ClaroFlowEngine.Api.Common.Services;

public static class CommonServiceExtensions
{
    public static IServiceCollection AddCommonServices(this IServiceCollection services)
    {
        services.AddScoped<ITransitionRecorder, TransitionRecorder>();
        return services;
    }
}
