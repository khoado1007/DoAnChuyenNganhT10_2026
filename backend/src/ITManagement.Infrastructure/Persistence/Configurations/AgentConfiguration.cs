using ITManagement.Domain.Entities.Agents;
using ITManagement.Domain.Entities.Asset;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ITManagement.Infrastructure.Persistence.Configurations;

public sealed class RegisteredAgentConfiguration : IEntityTypeConfiguration<RegisteredAgent>
{
    /// <summary>Cấu hình ánh xạ và ràng buộc dữ liệu cho agent.</summary>
    public void Configure(EntityTypeBuilder<RegisteredAgent> b)
    {
        b.ToTable("agents");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.AssetId).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        b.HasIndex(x => x.InstanceId).IsUnique();
        b.HasIndex(x => x.CredentialHash).IsUnique();
        b.Property(x => x.CredentialHash).HasMaxLength(64);
        b.Property(x => x.Mode).HasMaxLength(9);
        b.Property(x => x.Hostname).HasMaxLength(200);
        b.Property(x => x.Os).HasMaxLength(200);
        b.Property(x => x.Cpu).HasMaxLength(200);
        b.Property(x => x.AgentVersion).HasMaxLength(40);
        b.Property(x => x.RevokedReason).HasMaxLength(200);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class EnrollmentTokenConfiguration : IEntityTypeConfiguration<EnrollmentToken>
{
    /// <summary>Cấu hình ánh xạ và ràng buộc dữ liệu cho agent.</summary>
    public void Configure(EntityTypeBuilder<EnrollmentToken> b)
    {
        b.ToTable("agent_enrollment_tokens");
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.Property(x => x.Mode).HasMaxLength(9);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}
