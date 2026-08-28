using ClaroFlowEngine.Api.Modules.Invoices.Services;

namespace ClaroFlowEngine.Api.Modules.Invoices;

public static class InvoicesModuleExtensions
{
    public static IServiceCollection AddInvoicesModule(this IServiceCollection services)
    {
        services.AddScoped<IInvoiceService, InvoiceService>();
        return services;
    }
}
