using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Data.Seed;

/// <summary>
/// Popula o banco com dados mockados (planos e clientes de teste) na inicialização.
/// Idempotente: só insere o que ainda não existe.
/// </summary>
public static class DatabaseSeeder
{
    public static async Task SeedAsync(CfeDbContext db, CancellationToken cancellationToken = default)
    {
        var plans = await SeedPlansAsync(db, cancellationToken);
        var customers = await SeedCustomersAsync(db, cancellationToken);
        await SeedIdentityLinksAsync(db, customers, cancellationToken);
        await SeedCustomerPlansAsync(db, customers, plans, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, Plan>> SeedPlansAsync(CfeDbContext db, CancellationToken ct)
    {
        var seedPlans = new[]
        {
            new Plan { Code = "claro_15gb", Name = "Claro 15GB", DataGb = 15, MonthlyPriceCents = 4990, Active = true },
            new Plan { Code = "claro_30gb", Name = "Claro 30GB", DataGb = 30, MonthlyPriceCents = 5990, Active = true },
            new Plan { Code = "claro_60gb", Name = "Claro 60GB", DataGb = 60, MonthlyPriceCents = 8990, Active = true },
            new Plan { Code = "claro_100gb", Name = "Claro 100GB", DataGb = 100, MonthlyPriceCents = 11990, Active = true },
        };

        var existingCodes = await db.Plans.Select(p => p.Code).ToListAsync(ct);

        foreach (var plan in seedPlans.Where(p => !existingCodes.Contains(p.Code)))
        {
            db.Plans.Add(plan);
        }

        if (seedPlans.Any(p => !existingCodes.Contains(p.Code)))
        {
            await db.SaveChangesAsync(ct);
        }

        return await db.Plans.ToDictionaryAsync(p => p.Code, ct);
    }

    private static async Task<Dictionary<string, Customer>> SeedCustomersAsync(CfeDbContext db, CancellationToken ct)
    {
        var seedCustomers = new[]
        {
            new Customer { Cpf = "11144477735", FullName = "Ana Silva" },
            new Customer { Cpf = "22255588846", FullName = "Carlos Mendes" },
            new Customer { Cpf = "33366699957", FullName = "Mariana Souza" },
        };

        var existingCpfs = await db.Customers.Select(c => c.Cpf).ToListAsync(ct);

        foreach (var customer in seedCustomers.Where(c => !existingCpfs.Contains(c.Cpf)))
        {
            db.Customers.Add(customer);
        }

        if (seedCustomers.Any(c => !existingCpfs.Contains(c.Cpf)))
        {
            await db.SaveChangesAsync(ct);
        }

        return await db.Customers.ToDictionaryAsync(c => c.Cpf, ct);
    }

    private static async Task SeedIdentityLinksAsync(
        CfeDbContext db, Dictionary<string, Customer> customers, CancellationToken ct)
    {
        // Vincula o CPF de cada cliente ao canal "cpf" e um telefone fictício ao canal "whatsapp",
        // simulando um cliente que já iniciou contato por telefone antes do protótipo.
        var seedLinks = new[]
        {
            (Cpf: "11144477735", Channel: Channels.Cpf, Identifier: "11144477735"),
            (Cpf: "11144477735", Channel: Channels.Whatsapp, Identifier: "5511999990001"),
            (Cpf: "22255588846", Channel: Channels.Cpf, Identifier: "22255588846"),
            (Cpf: "22255588846", Channel: Channels.Whatsapp, Identifier: "5511999990002"),
            (Cpf: "33366699957", Channel: Channels.Cpf, Identifier: "33366699957"),
            (Cpf: "33366699957", Channel: Channels.Whatsapp, Identifier: "5511999990003"),
        };

        var existingLinks = await db.IdentityLinks
            .Select(l => new { l.Channel, l.Identifier })
            .ToListAsync(ct);

        foreach (var link in seedLinks)
        {
            if (!customers.TryGetValue(link.Cpf, out var customer)) continue;
            if (existingLinks.Any(l => l.Channel == link.Channel && l.Identifier == link.Identifier)) continue;

            db.IdentityLinks.Add(new IdentityLink
            {
                CustomerId = customer.Id,
                Channel = link.Channel,
                Identifier = link.Identifier,
            });
        }
    }

    private static async Task SeedCustomerPlansAsync(
        CfeDbContext db, Dictionary<string, Customer> customers, Dictionary<string, Plan> plans, CancellationToken ct)
    {
        var seedActivePlans = new[]
        {
            (Cpf: "11144477735", PlanCode: "claro_15gb"),
            (Cpf: "22255588846", PlanCode: "claro_30gb"),
            (Cpf: "33366699957", PlanCode: "claro_15gb"),
        };

        var existingActivePlanCustomerIds = await db.CustomerPlans
            .Where(cp => cp.Active)
            .Select(cp => cp.CustomerId)
            .ToListAsync(ct);

        foreach (var (cpf, planCode) in seedActivePlans)
        {
            if (!customers.TryGetValue(cpf, out var customer)) continue;
            if (!plans.TryGetValue(planCode, out var plan)) continue;
            if (existingActivePlanCustomerIds.Contains(customer.Id)) continue;

            db.CustomerPlans.Add(new CustomerPlan
            {
                CustomerId = customer.Id,
                PlanId = plan.Id,
                Active = true,
            });
        }
    }
}
