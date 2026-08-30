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
        await SeedInvoicesAsync(db, customers, plans, cancellationToken);

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
        // billing_due_day/segment são placeholders mockados (FASE 3, item C.3) — sem correspondência real,
        // só para o card do cliente no painel não exibir "—" em campos sem dado nenhum.
        var seedCustomers = new[]
        {
            new Customer { Cpf = "11144477735", FullName = "Ana Silva", BillingDueDay = 15, Segment = "Pessoa Física" },
            new Customer { Cpf = "22255588846", FullName = "Carlos Mendes", BillingDueDay = 5, Segment = "Premium" },
            new Customer { Cpf = "33366699957", FullName = "Mariana Souza", BillingDueDay = 20, Segment = "Controle" },
        };

        var existingCustomers = await db.Customers.ToListAsync(ct);
        var existingCpfs = existingCustomers.Select(c => c.Cpf).ToHashSet();

        foreach (var customer in seedCustomers.Where(c => !existingCpfs.Contains(c.Cpf)))
        {
            db.Customers.Add(customer);
        }

        // Backfill: bancos já seedados antes do item C.3 não têm billing_due_day/segment ainda.
        foreach (var seedCustomer in seedCustomers)
        {
            var existing = existingCustomers.FirstOrDefault(c => c.Cpf == seedCustomer.Cpf);
            if (existing is not null && existing.BillingDueDay is null)
            {
                existing.BillingDueDay = seedCustomer.BillingDueDay;
                existing.Segment = seedCustomer.Segment;
            }
        }

        await db.SaveChangesAsync(ct);

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

    /// <summary>
    /// Popula 3 faturas por cliente do seed (últimos 3 meses), cada uma com itens de linha —
    /// dados usados pela intenção "contestação de cobrança indevida" (ETAPA 2 Passo C / FASE 3 Bloco A).
    /// A fatura mais recente de cada cliente ganha um item de assinatura "gancho" mais plausível
    /// (streaming/TV/SMS premium) e um adicional pequeno, para dar o que "descobrir" na demonstração.
    /// </summary>
    private static async Task SeedInvoicesAsync(
        CfeDbContext db, Dictionary<string, Customer> customers, Dictionary<string, Plan> plans, CancellationToken ct)
    {
        var seedPlanByCpf = new[]
        {
            (Cpf: "11144477735", PlanCode: "claro_15gb"),
            (Cpf: "22255588846", PlanCode: "claro_30gb"),
            (Cpf: "33366699957", PlanCode: "claro_15gb"),
        };

        // Item "gancho" mais plausível por cliente (FASE 3, item A.5) — cada um contestável como
        // "assinatura que eu não contratei", cenário real de reclamação de cobrança indevida.
        var hookItemByCpf = new Dictionary<string, (string Description, int AmountCents)>
        {
            ["11144477735"] = ("Assinatura Premiere Torcedor", 4990),
            ["22255588846"] = ("Pacote Netflix Premium", 3990),
            ["33366699957"] = ("Aluguel Decodificador Claro TV+", 2490),
        };

        var existingCustomerIdsWithInvoices = await db.Invoices
            .Select(i => i.CustomerId)
            .Distinct()
            .ToListAsync(ct);

        var firstOfThisMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        foreach (var (cpf, planCode) in seedPlanByCpf)
        {
            if (!customers.TryGetValue(cpf, out var customer)) continue;
            if (!plans.TryGetValue(planCode, out var plan)) continue;
            if (existingCustomerIdsWithInvoices.Contains(customer.Id)) continue;

            var hookItem = hookItemByCpf[cpf];

            for (var monthsAgo = 2; monthsAgo >= 0; monthsAgo--)
            {
                var referenceMonth = firstOfThisMonth.AddMonths(-monthsAgo);
                var isMostRecent = monthsAgo == 0;

                var items = new List<(string Description, string Category, int AmountCents)>
                {
                    ($"Mensalidade {plan.Name}", InvoiceItemCategory.Subscription, plan.MonthlyPriceCents),
                };

                // Fatura mais recente (issued): assinatura "gancho" + adicional pequeno, para a demo de contestação.
                if (isMostRecent)
                {
                    items.Add((hookItem.Description, InvoiceItemCategory.Subscription, hookItem.AmountCents));
                    items.Add(("SMS internacional", InvoiceItemCategory.AddOn, 350));
                }

                items.Add(("ICMS", InvoiceItemCategory.Tax, 3600));
                items.Add(("PIS/COFINS", InvoiceItemCategory.Tax, 1200));
                items.Add(("Taxa de conveniência", InvoiceItemCategory.Fee, 3210));

                var invoice = new Invoice
                {
                    CustomerId = customer.Id,
                    ReferenceMonth = referenceMonth,
                    DueDate = referenceMonth.AddDays(14),
                    TotalCents = items.Sum(i => i.AmountCents),
                    Status = isMostRecent ? InvoiceStatus.Issued : InvoiceStatus.Paid,
                };
                db.Invoices.Add(invoice);

                for (var i = 0; i < items.Count; i++)
                {
                    db.InvoiceItems.Add(new InvoiceItem
                    {
                        Invoice = invoice,
                        Description = items[i].Description,
                        Category = items[i].Category,
                        AmountCents = items[i].AmountCents,
                        Sequence = i + 1,
                    });
                }
            }
        }
    }
}
