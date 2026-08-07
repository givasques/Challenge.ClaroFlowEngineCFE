using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaroFlowEngine.Api.Data;

public class CfeDbContext : DbContext
{
    public CfeDbContext(DbContextOptions<CfeDbContext> options) : base(options) { }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<IdentityLink> IdentityLinks => Set<IdentityLink>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<CustomerPlan> CustomerPlans => Set<CustomerPlan>();
    public DbSet<JourneyContext> JourneyContexts => Set<JourneyContext>();
    public DbSet<JourneyTransition> JourneyTransitions => Set<JourneyTransition>();
    public DbSet<HandoffToken> HandoffTokens => Set<HandoffToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Necessária para gen_random_uuid() usado como default das chaves primárias.
        modelBuilder.HasPostgresExtension("pgcrypto");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CfeDbContext).Assembly);
    }
}
