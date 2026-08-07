using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class CustomerPlanConfiguration : IEntityTypeConfiguration<CustomerPlan>
{
    public void Configure(EntityTypeBuilder<CustomerPlan> builder)
    {
        builder.ToTable("customer_plans");
        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(cp => cp.Active).HasDefaultValue(true);
        builder.Property(cp => cp.StartedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(cp => new { cp.CustomerId, cp.Active })
            .HasDatabaseName("ix_customer_plans_customer");

        builder.HasOne(cp => cp.Customer)
            .WithMany(c => c.CustomerPlans)
            .HasForeignKey(cp => cp.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cp => cp.Plan)
            .WithMany(p => p.CustomerPlans)
            .HasForeignKey(cp => cp.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
