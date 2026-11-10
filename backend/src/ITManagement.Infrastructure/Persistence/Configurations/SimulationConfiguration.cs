using ITManagement.Domain.Entities.Asset;
using ITManagement.Domain.Entities.Simulation;
using ITManagement.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITManagement.Infrastructure.Persistence.Configurations;

public sealed class SimulationConfiguration : IEntityTypeConfiguration<LocalProfile>,
    IEntityTypeConfiguration<DeviceAccessGrant>, IEntityTypeConfiguration<DeviceSession>,
    IEntityTypeConfiguration<ProfileFile>, IEntityTypeConfiguration<ProfileOperation>, IEntityTypeConfiguration<AppAuditEvent>
{
    public void Configure(EntityTypeBuilder<LocalProfile> b)
    {
        b.ToTable("local_profiles", t => t.HasCheckConstraint("CK_Profile_Counts", "\"MaxFiles\" >= 0 AND \"UsedFiles\" >= 0"));
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.AssetId }).IsUnique();
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<DeviceAccessGrant> b)
    {
        b.ToTable("device_access_grants"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.UserId, x.AssetId }).IsUnique().HasFilter("\"RevokedAt\" IS NULL");
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<DeviceSession> b)
    {
        b.ToTable("device_sessions"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.AssetId).IsUnique().HasFilter("\"EndedAt\" IS NULL");
        b.HasOne<LocalProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<ProfileFile> b)
    {
        b.ToTable("profile_files"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(512); b.Property(x => x.Folder).HasMaxLength(16);
        b.Property(x => x.Extension).HasMaxLength(32);
        b.HasIndex(x => new { x.ProfileId, x.Folder, x.Name }).IsUnique();
        b.HasOne<LocalProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<ProfileOperation> b)
    {
        b.ToTable("profile_operations"); b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ProfileId, x.RequestId }).IsUnique();
        b.HasIndex(x => x.ProfileId).IsUnique().HasFilter("\"CompletedAt\" IS NULL");
        b.Property(x => x.State).HasMaxLength(24); b.Property(x => x.Kind).HasMaxLength(24);
        b.Property(x => x.RequestHash).HasMaxLength(64); b.Property(x => x.ErrorCode).HasMaxLength(64);
        b.HasOne<LocalProfile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<AppAuditEvent> b)
    {
        b.ToTable("app_audit_events"); b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(100); b.HasIndex(x => x.CreatedAt);
    }
}
