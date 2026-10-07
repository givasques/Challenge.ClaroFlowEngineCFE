using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
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
        await SeedPanelUsersAsync(db, cancellationToken);
        await SeedOperationalCenterScenarioAsync(db, cancellationToken);
        await SeedRichHistoryScenarioAsync(db, cancellationToken);

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
        // Vincula o CPF de cada cliente ao canal "cpf", um telefone fictício ao canal "whatsapp"
        // (cliente que já iniciou contato por telefone antes do protótipo) e uma conta do App
        // (FASE 4.3, item B.2) — simula que a Claro já sabe qual conta do App Minha Claro é de qual
        // cliente, premissa de que o CFE confia na autenticação do App numa implantação real.
        var seedLinks = new[]
        {
            (Cpf: "11144477735", Channel: Channels.Cpf, Identifier: "11144477735"),
            (Cpf: "11144477735", Channel: Channels.Whatsapp, Identifier: "5511999990001"),
            (Cpf: "11144477735", Channel: Channels.App, Identifier: "ana.silva"),
            (Cpf: "22255588846", Channel: Channels.Cpf, Identifier: "22255588846"),
            (Cpf: "22255588846", Channel: Channels.Whatsapp, Identifier: "5511999990002"),
            (Cpf: "22255588846", Channel: Channels.App, Identifier: "carlos.mendes"),
            (Cpf: "33366699957", Channel: Channels.Cpf, Identifier: "33366699957"),
            (Cpf: "33366699957", Channel: Channels.Whatsapp, Identifier: "5511999990003"),
            (Cpf: "33366699957", Channel: Channels.App, Identifier: "mariana.souza"),
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

    /// <summary>
    /// Usuários de demonstração do painel (FASE 4.1, item D.1) — credenciais públicas e fracas de
    /// propósito, só por ser ambiente acadêmico (ver README). Senhas passam pelo mesmo
    /// <see cref="PasswordHasher{TUser}"/> usado em produção (Modules/Auth/Services/PasswordHashingService),
    /// instanciado direto aqui (não via DI) porque Data/ não deve depender de Modules/* (Parte I §2 do padrão).
    /// </summary>
    private static async Task SeedPanelUsersAsync(CfeDbContext db, CancellationToken ct)
    {
        var hasher = new PasswordHasher<PanelUser>();
        var seedUsers = new[]
        {
            (FullName: "Júlia Souza", Email: "julia.souza@cfe.demo", Role: PanelRole.Attendant, Password: "Atendente@2026"),
            (FullName: "Ricardo Almeida", Email: "ricardo.almeida@cfe.demo", Role: PanelRole.Manager, Password: "Gestor@2026"),
        };

        var existingEmails = await db.PanelUsers.Select(u => u.Email).ToListAsync(ct);

        foreach (var seedUser in seedUsers.Where(u => !existingEmails.Contains(u.Email)))
        {
            var user = new PanelUser
            {
                FullName = seedUser.FullName,
                Email = seedUser.Email,
                Role = seedUser.Role,
            };
            user.PasswordHash = hasher.HashPassword(user, seedUser.Password);
            db.PanelUsers.Add(user);
        }
    }

    /// <summary>
    /// Cenário da Central operacional (FASE 4.2, item C.2): clientes de demonstração com jornadas abertas em
    /// vários níveis de inatividade, para a Visão geral e os alertas aparecerem num banco recém-criado.
    /// Horários relativos ao momento do seed. Idempotente: se os clientes de demonstração já existem, não faz nada
    /// (para atualizar os horários, recrie o banco). Não toca nos clientes e contas do App já existentes.
    /// </summary>
    private static async Task SeedOperationalCenterScenarioAsync(CfeDbContext db, CancellationToken ct)
    {
        var existingCpfs = await db.Customers.Select(c => c.Cpf).ToListAsync(ct);
        if (existingCpfs.Contains(OperationalDemoScenarios[0].Cpf))
        {
            await BackfillScenarioDisputeReasonsAsync(db, ct);
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var scenario in OperationalDemoScenarios)
        {
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Cpf = scenario.Cpf,
                FullName = scenario.FullName,
                Segment = "Pessoa Física",
                BillingDueDay = 10,
            };
            db.Customers.Add(customer);
            db.IdentityLinks.Add(new IdentityLink { CustomerId = customer.Id, Channel = Channels.Cpf, Identifier = scenario.Cpf });
            db.IdentityLinks.Add(new IdentityLink { CustomerId = customer.Id, Channel = Channels.Whatsapp, Identifier = scenario.Phone });

            var journey = new JourneyContext
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                OriginChannel = scenario.OriginChannel,
                Intent = scenario.Intent,
                CurrentStep = scenario.CurrentStep,
                Status = JourneyStatus.Open,
                CreatedAt = now.AddMinutes(-scenario.OpenedMinutesAgo),
                UpdatedAt = now.AddMinutes(-scenario.InactiveMinutes),
            };
            if (scenario.DisputeReason is not null)
                journey.Payload["dispute_reason"] = scenario.DisputeReason;
            db.JourneyContexts.Add(journey);

            db.JourneyTransitions.Add(new JourneyTransition
            {
                Id = Guid.NewGuid(),
                JourneyContextId = journey.Id,
                Channel = scenario.OriginChannel,
                EventType = TransitionEventTypes.JourneyStarted,
                Description = "Jornada iniciada.",
                Metadata = new Dictionary<string, object> { ["intent"] = scenario.Intent },
                OccurredAt = journey.CreatedAt,
            });
            db.JourneyTransitions.Add(new JourneyTransition
            {
                Id = Guid.NewGuid(),
                JourneyContextId = journey.Id,
                Channel = scenario.CurrentChannel,
                EventType = TransitionEventTypes.StepUpdated,
                Description = "Etapa atualizada.",
                Metadata = new Dictionary<string, object> { ["current_step"] = scenario.CurrentStep },
                OccurredAt = journey.UpdatedAt,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Clientes fictícios com CPFs de teste (dígitos verificadores válidos). Nenhum deles tem conta do App vinculada,
    /// então não interfere nos roteiros de handoff da 4.3.
    /// </summary>
    private static readonly OperationalDemoScenario[] OperationalDemoScenarios =
    [
        // Atenção: ~8 min sem atividade.
        new("52601815906", "Lucia Ferreira", "5511988880001", Channels.Whatsapp, "change_plan", "plan_selected", Channels.Whatsapp, 8, 20),
        new("08301661305", "Rafael Costa", "5511988880002", Channels.App, "dispute_charge", "dispute_reason_selected", Channels.App, 8, 30, "higher_than_expected"),
        // Crítico: ~25 min sem atividade.
        new("18609139034", "Beatriz Lima", "5511988880003", Channels.Call, "dispute_charge", "identity_resolved", Channels.Call, 25, 40),
        // Crítico, com a jornada passada do WhatsApp para o App (canal atual diferente do de origem).
        new("99603082430", "Eduardo Nunes", "5511988880004", Channels.Whatsapp, "change_plan", "plan_selected", Channels.App, 25, 60),
        // Normal: atividade recente, só para a fila mostrar todas as prioridades.
        new("62819482112", "Helena Martins", "5511988880005", Channels.App, "change_plan", "identity_resolved", Channels.App, 1, 5),
    ];

    /// <summary>
    /// Cliente de demonstração com histórico variado nos últimos 30 dias (FASE 4.4, D.1): plano abandonado no App,
    /// contestação escalada para o Financeiro, contestação concluída, troca concluída e oportunidade crítica aberta.
    /// Idempotente pelo CPF. Não toca em Ana, Carlos, Mariana, nas contas do App nem no cenário da Central.
    /// </summary>
    private static async Task SeedRichHistoryScenarioAsync(CfeDbContext db, CancellationToken ct)
    {
        const string rodrigoCpf = "49100528102";
        var existingCpfs = await db.Customers.Select(c => c.Cpf).ToListAsync(ct);
        if (existingCpfs.Contains(rodrigoCpf)) return;

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Cpf = rodrigoCpf,
            FullName = "Rodrigo Alves",
            Segment = "Pessoa Física",
            BillingDueDay = 12,
        };
        db.Customers.Add(customer);
        db.IdentityLinks.Add(new IdentityLink { CustomerId = customer.Id, Channel = Channels.Cpf, Identifier = rodrigoCpf });
        db.IdentityLinks.Add(new IdentityLink { CustomerId = customer.Id, Channel = Channels.Whatsapp, Identifier = "5511988880010" });

        var plan15 = await db.Plans.FirstAsync(p => p.Code == "claro_15gb", ct);
        db.CustomerPlans.Add(new CustomerPlan { Id = Guid.NewGuid(), CustomerId = customer.Id, PlanId = plan15.Id, Active = true, StartedAt = now.AddDays(-60) });

        var firstOfMonth = new DateOnly(now.Year, now.Month, 1);
        var invoiceHigher = NewInvoice(customer.Id, firstOfMonth.AddMonths(-1), 8990, now);
        var invoiceDuplicate = NewInvoice(customer.Id, firstOfMonth.AddMonths(-2), 5990, now);
        db.Invoices.AddRange(invoiceHigher, invoiceDuplicate);

        // Plano abandonado no App, há 21 dias.
        var abandoned = NewJourney(customer.Id, Channels.App, "change_plan", "plan_selected", JourneyStatus.Abandoned, now.AddDays(-21));
        abandoned.Payload["selected_plan_code"] = "claro_30gb";
        abandoned.ClosedAt = abandoned.UpdatedAt;

        // Contestação escalada para o Financeiro, há 10 dias, sem desfecho.
        var escalated = NewJourney(customer.Id, Channels.Whatsapp, "dispute_charge", "dispute_formalized", JourneyStatus.Escalated, now.AddDays(-10));
        escalated.Payload["invoice_id"] = invoiceHigher.Id.ToString();
        escalated.Payload["dispute_reason"] = "higher_than_expected";
        escalated.Payload["escalation_area"] = "financial";
        escalated.EscalatedAt = now.AddDays(-9);
        escalated.UpdatedAt = now.AddDays(-9);

        // Contestação concluída pela central telefônica, há 25 dias.
        var concluded = NewJourney(customer.Id, Channels.Call, "dispute_charge", "description_provided", JourneyStatus.Concluded, now.AddDays(-25));
        concluded.Payload["invoice_id"] = invoiceDuplicate.Id.ToString();
        concluded.Payload["dispute_reason"] = "duplicate_charge";
        concluded.Payload["resolution_category"] = "resolved_action_taken";
        concluded.ClosedAt = now.AddDays(-24);
        concluded.UpdatedAt = now.AddDays(-24);

        // Troca de plano concluída pelo WhatsApp, há 14 dias.
        var planChanged = NewJourney(customer.Id, Channels.Whatsapp, "change_plan", "plan_selected", JourneyStatus.Concluded, now.AddDays(-14));
        planChanged.Payload["selected_plan_code"] = "claro_60gb";
        planChanged.Payload["resolution_category"] = "resolved_answered";
        planChanged.ClosedAt = now.AddDays(-13);
        planChanged.UpdatedAt = now.AddDays(-13);

        db.JourneyContexts.AddRange(abandoned, escalated, concluded, planChanged);
        AddJourneyEvents(db, abandoned, Channels.App, TransitionEventTypes.JourneyClosed, Channels.App);
        AddJourneyEvents(db, escalated, Channels.Whatsapp, TransitionEventTypes.JourneyEscalated, Channels.Panel);
        AddJourneyEvents(db, concluded, Channels.Call, TransitionEventTypes.JourneyConcludedByAgent, Channels.Panel);
        AddJourneyEvents(db, planChanged, Channels.Whatsapp, TransitionEventTypes.JourneyClosed, Channels.Whatsapp);

        db.Opportunities.Add(new Opportunity
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Category = OpportunityCategory.AbandonedPlanChange,
            Urgency = OpportunityUrgency.Critical,
            Status = OpportunityStatus.New,
            TriggeringJourneyId = abandoned.Id,
            Metadata = new Dictionary<string, object>(),
            DetectedAt = now.AddDays(-2),
            ValidUntil = now.AddDays(28),
        });

        await db.SaveChangesAsync(ct);
    }

    private static Invoice NewInvoice(Guid customerId, DateOnly referenceMonth, int totalCents, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        ReferenceMonth = referenceMonth,
        DueDate = referenceMonth.AddDays(14),
        TotalCents = totalCents,
        Status = InvoiceStatus.Paid,
        CreatedAt = now,
    };

    private static JourneyContext NewJourney(Guid customerId, string channel, string intent, string currentStep, string status, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        CustomerId = customerId,
        OriginChannel = channel,
        Intent = intent,
        CurrentStep = currentStep,
        Status = status,
        CreatedAt = createdAt,
        UpdatedAt = createdAt.AddHours(1),
    };

    private static void AddJourneyEvents(CfeDbContext db, JourneyContext journey, string originChannel, string closingEvent, string closingChannel)
    {
        db.JourneyTransitions.Add(new JourneyTransition
        {
            Id = Guid.NewGuid(),
            JourneyContextId = journey.Id,
            Channel = originChannel,
            EventType = TransitionEventTypes.JourneyStarted,
            Description = "Jornada iniciada.",
            Metadata = new Dictionary<string, object> { ["intent"] = journey.Intent },
            OccurredAt = journey.CreatedAt,
        });
        db.JourneyTransitions.Add(new JourneyTransition
        {
            Id = Guid.NewGuid(),
            JourneyContextId = journey.Id,
            Channel = originChannel,
            EventType = TransitionEventTypes.StepUpdated,
            Description = "Etapa atualizada.",
            Metadata = new Dictionary<string, object> { ["current_step"] = journey.CurrentStep },
            OccurredAt = journey.CreatedAt.AddHours(1),
        });
        db.JourneyTransitions.Add(new JourneyTransition
        {
            Id = Guid.NewGuid(),
            JourneyContextId = journey.Id,
            Channel = closingChannel,
            EventType = closingEvent,
            Description = "Evento de encerramento da jornada.",
            Metadata = new Dictionary<string, object>(),
            OccurredAt = journey.UpdatedAt,
        });
    }

    private sealed record OperationalDemoScenario(
        string Cpf,
        string FullName,
        string Phone,
        string OriginChannel,
        string Intent,
        string CurrentStep,
        string CurrentChannel,
        int InactiveMinutes,
        int OpenedMinutesAgo,
        string? DisputeReason = null);

    /// <summary>
    /// Bases já semeadas antes do motivo existir: preenche o motivo nas jornadas de contestação abertas que não o têm.
    /// Só mexe no que faltou; não altera motivo que já está gravado.
    /// </summary>
    private static async Task BackfillScenarioDisputeReasonsAsync(CfeDbContext db, CancellationToken ct)
    {
        foreach (var scenario in OperationalDemoScenarios.Where(s => s.DisputeReason is not null))
        {
            var journeys = await db.JourneyContexts
                .Where(j => j.Customer.Cpf == scenario.Cpf && j.Intent == scenario.Intent && j.Status == JourneyStatus.Open)
                .ToListAsync(ct);
            foreach (var journey in journeys.Where(j => !j.Payload.ContainsKey("dispute_reason")))
                journey.Payload["dispute_reason"] = scenario.DisputeReason!;
        }

        await db.SaveChangesAsync(ct);
    }
}
