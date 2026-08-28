using ClaroFlowEngine.Api.Common.Contracts;
using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(i => i.Status).HasMaxLength(20).HasDefaultValue(InvoiceStatus.Issued);
        builder.Property(i => i.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(i => new { i.CustomerId, i.ReferenceMonth })
            .HasDatabaseName("ix_invoices_customer_month")
            .IsDescending(false, true);

        builder.HasOne(i => i.Customer)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_invoices_status", "status IN ('issued', 'paid', 'overdue', 'contested')");
            t.HasCheckConstraint("ck_invoices_total_positive", "total_cents > 0");
        });
    }
}
