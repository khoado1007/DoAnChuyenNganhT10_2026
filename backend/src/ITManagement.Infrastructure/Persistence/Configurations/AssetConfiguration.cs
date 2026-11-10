using ITManagement.Domain.Entities.Asset;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITManagement.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    /// <summary>Cấu hình ánh xạ và ràng buộc dữ liệu cho tài sản.</summary>
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("assets");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.AssetCode).IsRequired().HasMaxLength(50);
        builder.HasIndex(a => a.AssetCode).IsUnique();
        builder.Property(a => a.Name).IsRequired().HasMaxLength(200);
        builder.Property(a => a.SerialNumber).HasMaxLength(100);
        builder.Property(a => a.Manufacturer).HasMaxLength(100);
        builder.Property(a => a.Model).HasMaxLength(100);
        builder.Property(a => a.Location).HasMaxLength(200);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.PurchasePrice).HasColumnType("decimal(18,2)");

        builder.Property(a => a.Status).HasConversion<string>();
        builder.Property(a => a.Type).HasConversion<string>();
    }
}
