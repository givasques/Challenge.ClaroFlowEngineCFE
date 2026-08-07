using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class JourneyTransitionConfiguration : IEntityTypeConfiguration<JourneyTransition>
{
    public void Configure(EntityTypeBuilder<JourneyTransition> builder)
    {
        builder.ToTable("journey_transitions");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(t => t.Channel).HasMaxLength(20).IsRequired();
        builder.Property(t => t.EventType).HasMaxLength(50).IsRequired();

        builder.Property(t => t.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
            .Metadata.SetValueComparer(JsonDictionaryValueComparer.Instance);
        builder.Property(t => t.Metadata).HasDefaultValueSql("'{}'::jsonb");

        builder.Property(t => t.OccurredAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(t => new { t.JourneyContextId, t.OccurredAt })
            .HasDatabaseName("ix_transitions_journey_occurred");

        builder.HasOne(t => t.JourneyContext)
            .WithMany(j => j.Transitions)
            .HasForeignKey(t => t.JourneyContextId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
