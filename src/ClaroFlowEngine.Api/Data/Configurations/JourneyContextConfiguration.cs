using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class JourneyContextConfiguration : IEntityTypeConfiguration<JourneyContext>
{
    public void Configure(EntityTypeBuilder<JourneyContext> builder)
    {
        builder.ToTable("journey_contexts");
        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(j => j.OriginChannel).HasMaxLength(20).IsRequired();
        builder.Property(j => j.Intent).HasMaxLength(50).IsRequired();
        builder.Property(j => j.CurrentStep).HasMaxLength(50).IsRequired();
        builder.Property(j => j.Status).HasMaxLength(20).IsRequired();

        builder.Property(j => j.Payload)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
            .Metadata.SetValueComparer(JsonDictionaryValueComparer.Instance);
        builder.Property(j => j.Payload).HasDefaultValueSql("'{}'::jsonb");

        builder.Property(j => j.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(j => j.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(j => new { j.CustomerId, j.Status }).HasDatabaseName("ix_journey_customer_status");
        builder.HasIndex(j => j.UpdatedAt)
            .HasDatabaseName("ix_journey_open_updated")
            .HasFilter("status = 'open'");

        builder.HasOne(j => j.Customer)
            .WithMany(c => c.JourneyContexts)
            .HasForeignKey(j => j.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint("ck_journey_status",
            "status IN ('open', 'concluded', 'expired', 'abandoned')"));
    }
}
