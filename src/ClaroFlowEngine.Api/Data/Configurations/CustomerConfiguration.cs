using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.Cpf).HasMaxLength(11).IsRequired();
        builder.HasIndex(c => c.Cpf).IsUnique().HasDatabaseName("ux_customers_cpf");

        builder.Property(c => c.FullName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CreatedAt).HasDefaultValueSql("NOW()");

        builder.Property(c => c.Segment).HasMaxLength(50);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_customers_cpf_format", "cpf ~ '^\\d{11}$'");
            t.HasCheckConstraint("ck_customers_billing_due_day", "billing_due_day IS NULL OR billing_due_day BETWEEN 1 AND 28");
        });
    }
}
