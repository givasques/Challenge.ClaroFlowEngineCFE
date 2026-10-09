using ClaroFlowEngine.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaroFlowEngine.Api.Data.Configurations;

public class PanelUserConfiguration : IEntityTypeConfiguration<PanelUser>
{
    public void Configure(EntityTypeBuilder<PanelUser> builder)
    {
        builder.ToTable("panel_users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(u => u.FullName).HasMaxLength(150).IsRequired();

        // Sempre salvo em minúsculas pelo service antes de persistir (A.6) — índice único garante
        // que isso é respeitado mesmo se outro código escrever direto na tabela.
        builder.Property(u => u.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("ux_panel_users_email");

        builder.Property(u => u.PasswordHash).IsRequired();
        builder.Property(u => u.Role).HasMaxLength(20).IsRequired();
        builder.Property(u => u.IsActive).HasDefaultValue(true);
        builder.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);
        builder.Property(u => u.CreatedAt).HasDefaultValueSql("NOW()");
        builder.Property(u => u.UpdatedAt).HasDefaultValueSql("NOW()");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_panel_users_role", "role IN ('attendant', 'manager')");
            t.HasCheckConstraint("ck_panel_users_failed_attempts", "failed_login_attempts >= 0");
        });
    }
}
