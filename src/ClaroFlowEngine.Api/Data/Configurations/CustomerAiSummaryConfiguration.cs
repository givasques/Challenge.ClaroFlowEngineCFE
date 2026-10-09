using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ClaroFlowEngine.Api.Data.Entities;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class CustomerAiSummaryConfiguration : IEntityTypeConfiguration<CustomerAiSummary>
{
    public void Configure(EntityTypeBuilder<CustomerAiSummary> builder)
    {
        builder.ToTable("customer_ai_summaries");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.InputFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(s => s.Source).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Model).HasMaxLength(100);

        builder.Property(s => s.Content)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new())
            .Metadata.SetValueComparer(JsonDictionaryValueComparer.Instance);
        builder.Property(s => s.Content).HasDefaultValueSql("'{}'::jsonb");

        builder.HasOne(s => s.Customer)
            .WithMany()
            .HasForeignKey(s => s.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.CustomerId).HasDatabaseName("ix_customer_ai_summaries_customer_id");
        builder.HasIndex(s => new { s.CustomerId, s.InputFingerprint })
            .HasDatabaseName("ix_customer_ai_summaries_customer_fingerprint");

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_customer_ai_summaries_source", "source IN ('ai', 'rules')"));
    }
}
