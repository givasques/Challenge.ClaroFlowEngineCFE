using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Data;
using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaroFlowEngine.Api.Tests.Support;

/// <summary>Dados de teste: clientes e jornadas criados por cada teste, com CPFs próprios.</summary>
public static class TestData
{
    public static async Task<Guid> CreateCustomerAsync(TestHost host, string cpf, string fullName)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        var customer = new Customer { Id = Guid.NewGuid(), Cpf = cpf, FullName = fullName, Segment = "Teste", BillingDueDay = 10 };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer.Id;
    }

    public static async Task AddJourneyAsync(TestHost host, Guid customerId, string channel, int minutesAgo)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        db.JourneyContexts.Add(new JourneyContext
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OriginChannel = channel,
            Intent = "change_plan",
            CurrentStep = "plan_selected",
            Status = JourneyStatus.Open,
            CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo - 10),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
        });
        await db.SaveChangesAsync();
    }

    public static async Task<int> CountAiSummariesAsync(TestHost host, Guid customerId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CfeDbContext>();
        return await db.CustomerAiSummaries.CountAsync(s => s.CustomerId == customerId);
    }
}
