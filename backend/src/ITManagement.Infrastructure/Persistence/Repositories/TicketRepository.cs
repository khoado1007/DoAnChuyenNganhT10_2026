using ITManagement.Domain.Entities.Ticket;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Persistence.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly AppDbContext _db;

    /// <summary>Khởi tạo repository phiếu hỗ trợ với ngữ cảnh dữ liệu.</summary>
    public TicketRepository(AppDbContext db) => _db = db;

    /// <summary>Tìm phiếu hỗ trợ theo mã định danh.</summary>
    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Tickets
              .Include(t => t.History)
              .Include(t => t.Comments)
              .Include(t => t.CreatedBy)
              .Include(t => t.AssignedTo)
              .FirstOrDefaultAsync(t => t.Id == id, ct);

    /// <summary>Tìm phiếu hỗ trợ theo số phiếu.</summary>
    public Task<Ticket?> GetByNumberAsync(string ticketNumber, CancellationToken ct = default)
        => _db.Tickets.FirstOrDefaultAsync(t => t.TicketNumber == ticketNumber, ct);

    /// <summary>Liệt kê phiếu do người dùng tạo.</summary>
    public async Task<IEnumerable<Ticket>> GetByCreatorAsync(Guid userId, CancellationToken ct = default)
        => await _db.Tickets.Where(t => t.CreatedByUserId == userId).ToListAsync(ct);

    /// <summary>Liệt kê phiếu được giao cho người xử lý.</summary>
    public async Task<IEnumerable<Ticket>> GetByAssigneeAsync(Guid technicianId, CancellationToken ct = default)
        => await _db.Tickets.Where(t => t.AssignedToUserId == technicianId).ToListAsync(ct);

    /// <summary>Liệt kê phiếu theo trạng thái.</summary>
    public async Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status, CancellationToken ct = default)
        => await _db.Tickets.Where(t => t.Status == status).ToListAsync(ct);

    /// <summary>Truy vấn danh sách phiếu theo bộ lọc và phân trang.</summary>
    public async Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default)
        => await _db.Tickets
                    .Include(t => t.CreatedBy)
                    .Include(t => t.AssignedTo)
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync(ct);

    /// <summary>Thêm phiếu hỗ trợ vào ngữ cảnh dữ liệu.</summary>
    public async Task AddAsync(Ticket ticket, CancellationToken ct = default)
        => await _db.Tickets.AddAsync(ticket, ct);

    /// <summary>Đánh dấu phiếu hỗ trợ cần cập nhật.</summary>
    public Task UpdateAsync(Ticket ticket, CancellationToken ct = default)
    {
        _db.Tickets.Update(ticket);
        return Task.CompletedTask;
    }

    /// <summary>Lấy số thứ tự tiếp theo để tạo mã phiếu duy nhất.</summary>
    public async Task<int> GetNextSequenceAsync(int year, CancellationToken ct = default)
        // SQL Server cấp sequence nguyên tử; materialize trước để EF không bọc NEXT VALUE FOR trong subquery.
        => (await _db.Database.SqlQueryRaw<int>("SELECT NEXT VALUE FOR [ticket_number_seq] AS [Value]")
            .ToListAsync(ct)).Single();
}
