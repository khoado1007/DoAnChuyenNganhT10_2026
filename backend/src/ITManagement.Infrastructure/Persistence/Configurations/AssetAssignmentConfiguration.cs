using ITManagement.Domain.Entities.Asset;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ITManagement.Infrastructure.Persistence.Configurations;
public sealed class AssetAssignmentConfiguration : IEntityTypeConfiguration<AssetAssignment>
{
    /// <summary>Cấu hình ánh xạ và ràng buộc cho lượt giao tài sản.</summary>
    public void Configure(EntityTypeBuilder<AssetAssignment> builder)
    {
        builder.HasIndex(a => a.AssetId).IsUnique().HasFilter("\"Status\" = 1");
        builder.HasOne(a => a.Asset).WithMany(a => a.Assignments).HasForeignKey(a => a.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.User).WithMany(u => u.AssetAssignments).HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
