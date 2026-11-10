using ITManagement.Domain.Entities.User;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của hệ thống. Kế thừa IdentityDbContext để tích hợp ASP.NET Identity.
/// Toàn bộ entity configuration nằm trong thư mục Configurations/.
/// </summary>
public class AppDbContext : IdentityDbContext<IdentityUser>
{
    public DbSet<Domain.Entities.Simulation.LocalProfile> LocalProfiles => Set<Domain.Entities.Simulation.LocalProfile>();
    public DbSet<Domain.Entities.Simulation.DeviceAccessGrant> DeviceAccessGrants => Set<Domain.Entities.Simulation.DeviceAccessGrant>();
    public DbSet<Domain.Entities.Simulation.DeviceSession> DeviceSessions => Set<Domain.Entities.Simulation.DeviceSession>();
    public DbSet<Domain.Entities.Simulation.ProfileFile> ProfileFiles => Set<Domain.Entities.Simulation.ProfileFile>();
    public DbSet<Domain.Entities.Simulation.ProfileOperation> ProfileOperations => Set<Domain.Entities.Simulation.ProfileOperation>();
    public DbSet<Domain.Entities.Simulation.AppAuditEvent> AppAuditEvents => Set<Domain.Entities.Simulation.AppAuditEvent>();
    /// <summary>Khởi tạo ngữ cảnh dữ liệu với các tùy chọn Entity Framework được cung cấp.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Domain.Entities.Agents.RegisteredAgent> Agents => Set<Domain.Entities.Agents.RegisteredAgent>();
    public DbSet<Domain.Entities.Agents.EnrollmentToken> EnrollmentTokens => Set<Domain.Entities.Agents.EnrollmentToken>();

    // Domain entities
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Domain.Entities.Asset.Asset> Assets => Set<Domain.Entities.Asset.Asset>();
    public DbSet<Domain.Entities.Asset.AssetAssignment> AssetAssignments => Set<Domain.Entities.Asset.AssetAssignment>();
    public DbSet<Domain.Entities.Storage.StorageQuota> StorageQuotas => Set<Domain.Entities.Storage.StorageQuota>();
    public DbSet<Domain.Entities.Storage.FileMetadata> FileMetadata => Set<Domain.Entities.Storage.FileMetadata>();
    public DbSet<Domain.Entities.Ticket.Ticket> Tickets => Set<Domain.Entities.Ticket.Ticket>();
    public DbSet<Domain.Entities.Ticket.TicketHistory> TicketHistories => Set<Domain.Entities.Ticket.TicketHistory>();
    public DbSet<Domain.Entities.Ticket.TicketComment> TicketComments => Set<Domain.Entities.Ticket.TicketComment>();

    // Áp dụng cấu hình ánh xạ cho mô hình dữ liệu.
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Global monotonic sequence: year remains a display prefix, not a reset boundary.
        builder.HasSequence<int>("ticket_number_seq");

        // Áp dụng toàn bộ IEntityTypeConfiguration trong assembly
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Soft delete global query filter
        builder.Entity<AppUser>().HasQueryFilter(u => !u.IsDeleted);
        builder.Entity<Domain.Entities.Asset.Asset>().HasQueryFilter(a => !a.IsDeleted);
        builder.Entity<Domain.Entities.Ticket.Ticket>().HasQueryFilter(t => !t.IsDeleted);
    }
}
