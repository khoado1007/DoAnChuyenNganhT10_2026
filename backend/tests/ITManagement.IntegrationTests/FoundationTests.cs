using ITManagement.Infrastructure.Persistence;
using ITManagement.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ITManagement.IntegrationTests;

public class FoundationTests
{
    // Tạo DbContext trỏ đến cơ sở dữ liệu không thể truy cập để kiểm thử lỗi kết nối.
    private static AppDbContext UnreachableDatabase() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unreachable;Username=postgres;Password=postgres;Timeout=1")
            .Options);

    /// <summary>Kiểm thử lỗi cơ sở dữ liệu được truyền lên khi bắt đầu giao dịch thất bại.</summary>
    [Fact]
    public async Task Begin_transaction_propagates_database_failure()
    {
        await using var db = UnreachableDatabase();
        var work = new UnitOfWork(db, new AssetRepository(db), new UserRepository(db),
            new TicketRepository(db), new StorageRepository(db));
        await Assert.ThrowsAnyAsync<Exception>(() => work.BeginTransactionAsync());
    }

    /// <summary>Kiểm thử truy vấn danh sách tài sản tôn trọng yêu cầu hủy trước khi trả dữ liệu.</summary>
    [Fact]
    public async Task Asset_list_observes_cancellation_before_returning_data()
    {
        await using var db = UnreachableDatabase();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new AssetRepository(db).GetAllAsync(cancellation.Token));
    }
}
