using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class IdentityLinkConfiguration : IEntityTypeConfiguration<IdentityLink>
{
    public void Configure(EntityTypeBuilder<IdentityLink> builder)
    {
        builder.ToTable("identity_links");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(l => l.Channel).HasMaxLength(20).IsRequired();
        builder.Property(l => l.Identifier).HasMaxLength(100).IsRequired();
        builder.Property(l => l.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(l => new { l.Channel, l.Identifier })
            .IsUnique()
            .HasDatabaseName("ux_identity_links_channel_identifier");

        builder.HasIndex(l => l.CustomerId).HasDatabaseName("ix_identity_links_customer");

        builder.HasOne(l => l.Customer)
            .WithMany(c => c.IdentityLinks)
            .HasForeignKey(l => l.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
