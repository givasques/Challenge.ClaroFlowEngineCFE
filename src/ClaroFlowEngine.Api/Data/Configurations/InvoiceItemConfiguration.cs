using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class InvoiceItemConfiguration : IEntityTypeConfiguration<InvoiceItem>
{
    public void Configure(EntityTypeBuilder<InvoiceItem> builder)
    {
        builder.ToTable("invoice_items");
        builder.HasKey(ii => ii.Id);
        builder.Property(ii => ii.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(ii => ii.Description).HasMaxLength(200).IsRequired();
        builder.Property(ii => ii.Category).HasMaxLength(50).IsRequired();

        builder.HasIndex(ii => new { ii.InvoiceId, ii.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_invoice_items_invoice_sequence");

        builder.HasOne(ii => ii.Invoice)
            .WithMany(i => i.Items)
            .HasForeignKey(ii => ii.InvoiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
