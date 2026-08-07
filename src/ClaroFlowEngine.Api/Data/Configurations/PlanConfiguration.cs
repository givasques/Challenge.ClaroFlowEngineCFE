using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique().HasDatabaseName("ux_plans_code");

        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Active).HasDefaultValue(true);

        builder.ToTable(t => t.HasCheckConstraint("ck_plans_price_positive", "monthly_price_cents > 0"));
    }
}
