using ITManagement.Domain.Entities.Ticket;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITManagement.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    /// <summary>Cấu hình ánh xạ và ràng buộc dữ liệu cho phiếu hỗ trợ.</summary>
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("tickets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.TicketNumber).IsRequired().HasMaxLength(30);
        builder.HasIndex(t => t.TicketNumber).IsUnique();
        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);
        builder.Property(t => t.Resolution).HasMaxLength(4000);

        builder.Property(t => t.Status).HasConversion<string>();
        builder.Property(t => t.Priority).HasConversion<string>();
        builder.Property(t => t.Category).HasConversion<string>();

        builder.HasOne(t => t.CreatedBy)
               .WithMany(u => u.CreatedTickets)
               .HasForeignKey(t => t.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.AssignedTo)
               .WithMany(u => u.AssignedTickets)
               .HasForeignKey(t => t.AssignedToUserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
