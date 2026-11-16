using ITManagement.Domain.Entities.Asset;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITManagement.Infrastructure.Persistence.Configurations;

public sealed class MachineSpecificationConfiguration : IEntityTypeConfiguration<MachineSpecification>
{
    public void Configure(EntityTypeBuilder<MachineSpecification> builder)
    {
        builder.ToTable("machine_specifications");
        builder.HasKey(x => x.AssetId);
        builder.HasOne<Asset>().WithOne().HasForeignKey<MachineSpecification>(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.OperatingSystem).HasMaxLength(200);
        builder.Property(x => x.Processor).HasMaxLength(200);
        builder.Property(x => x.MemoryGb).HasPrecision(14, 2);
        builder.Property(x => x.DiskTotalGb).HasPrecision(14, 2);
        builder.Property(x => x.DiskFreeGb).HasPrecision(14, 2);
        builder.Property(x => x.CollectorVersion).HasMaxLength(100);
        builder.Property(x => x.PrivateIp).HasMaxLength(45);
        builder.Property(x => x.PublicIp).HasMaxLength(45);
        builder.Property(x => x.SoftwareJson).IsRequired();
    }
}
