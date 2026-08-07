using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class HandoffTokenConfiguration : IEntityTypeConfiguration<HandoffToken>
{
    public void Configure(EntityTypeBuilder<HandoffToken> builder)
    {
        builder.ToTable("handoff_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Token).HasMaxLength(100).IsRequired();
        builder.HasIndex(t => t.Token).IsUnique().HasDatabaseName("ix_handoff_tokens_token");

        builder.Property(t => t.TargetChannel).HasMaxLength(20).IsRequired();
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("NOW()");

        builder.HasOne(t => t.JourneyContext)
            .WithMany(j => j.HandoffTokens)
            .HasForeignKey(t => t.JourneyContextId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
