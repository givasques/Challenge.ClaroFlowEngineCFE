using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("opportunities");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(o => o.Category).HasMaxLength(50).IsRequired();
        builder.Property(o => o.Urgency).HasMaxLength(20).IsRequired();
        builder.Property(o => o.Status).HasMaxLength(20).IsRequired().HasDefaultValue("new");
        builder.Property(o => o.ContactedBy).HasMaxLength(100);
        builder.Property(o => o.ResolutionNotes).HasMaxLength(500);

        builder.Property(o => o.Metadata)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
            .Metadata.SetValueComparer(JsonDictionaryValueComparer.Instance);
        builder.Property(o => o.Metadata).HasDefaultValueSql("'{}'::jsonb");

        builder.Property(o => o.DetectedAt).HasDefaultValueSql("NOW()");

        builder.HasIndex(o => new { o.Status, o.Urgency })
            .HasDatabaseName("idx_opp_status_urgency")
            .HasFilter("status IN ('new', 'contacted')");
        builder.HasIndex(o => o.CustomerId).HasDatabaseName("idx_opp_customer");
        builder.HasIndex(o => o.ValidUntil)
            .HasDatabaseName("idx_opp_valid_until")
            .HasFilter("status IN ('new', 'contacted')");

        builder.HasOne(o => o.Customer)
            .WithMany()
            .HasForeignKey(o => o.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.TriggeringJourney)
            .WithMany()
            .HasForeignKey(o => o.TriggeringJourneyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_opportunities_urgency", "urgency IN ('critical', 'high', 'medium', 'low')");
            t.HasCheckConstraint("ck_opportunities_status", "status IN ('new', 'contacted', 'converted', 'not_relevant')");
        });
    }
}
