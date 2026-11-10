using ITManagement.Domain.Entities.User;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITManagement.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    /// <summary>Cấu hình ánh xạ và ràng buộc dữ liệu cho người dùng.</summary>
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.HasIndex(u => u.Username).IsUnique();
        builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
        builder.HasIndex(u => u.Email).IsUnique();
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Department).HasMaxLength(100);
        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.IdentityUserId).IsRequired().HasMaxLength(450);

        builder.Property(u => u.Role).HasConversion<string>();
        builder.Property(u => u.Status).HasConversion<string>();

        // Một user có một StorageQuota
        builder.HasOne(u => u.StorageQuota)
               .WithOne(q => q.User)
               .HasForeignKey<Domain.Entities.Storage.StorageQuota>(q => q.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
