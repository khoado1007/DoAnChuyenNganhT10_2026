namespace ITManagement.Domain.Interfaces.Repositories;

/// <summary>
/// Unit of Work pattern — commit toàn bộ changes trong một transaction.
/// </summary>
public interface IUnitOfWork
{
    IAssetRepository Assets { get; }
    IUserRepository Users { get; }
    ITicketRepository Tickets { get; }
    IStorageRepository Storage { get; }

    /// <summary>Lưu các thay đổi đang chờ vào cơ sở dữ liệu.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    /// <summary>Bắt đầu giao dịch cơ sở dữ liệu.</summary>
    Task BeginTransactionAsync(CancellationToken ct = default);
    /// <summary>Xác nhận giao dịch hiện tại.</summary>
    Task CommitTransactionAsync(CancellationToken ct = default);
    /// <summary>Hoàn tác giao dịch hiện tại.</summary>
    Task RollbackTransactionAsync(CancellationToken ct = default);
}
