using ITManagement.Domain.Entities.Ticket;
using ITManagement.Domain.Enums;

namespace ITManagement.Domain.Interfaces.Repositories;

public interface ITicketRepository
{
    /// <summary>Tìm phiếu hỗ trợ theo mã định danh.</summary>
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Tìm phiếu hỗ trợ theo số phiếu.</summary>
    Task<Ticket?> GetByNumberAsync(string ticketNumber, CancellationToken ct = default);
    /// <summary>Liệt kê phiếu do người dùng tạo.</summary>
    Task<IEnumerable<Ticket>> GetByCreatorAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Liệt kê phiếu được giao cho người xử lý.</summary>
    Task<IEnumerable<Ticket>> GetByAssigneeAsync(Guid technicianId, CancellationToken ct = default);
    /// <summary>Liệt kê phiếu theo trạng thái.</summary>
    Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status, CancellationToken ct = default);
    /// <summary>Truy vấn danh sách phiếu theo điều kiện lọc.</summary>
    Task<IEnumerable<Ticket>> GetAllAsync(CancellationToken ct = default);
    /// <summary>Thêm phiếu hỗ trợ mới.</summary>
    Task AddAsync(Ticket ticket, CancellationToken ct = default);
    /// <summary>Cập nhật phiếu hỗ trợ hiện có.</summary>
    Task UpdateAsync(Ticket ticket, CancellationToken ct = default);
    /// <summary>Lấy số thứ tự tiếp theo để tạo số phiếu duy nhất.</summary>
    Task<int> GetNextSequenceAsync(int year, CancellationToken ct = default);
}
