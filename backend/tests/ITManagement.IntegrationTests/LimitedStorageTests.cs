using ITManagement.Application.Common.Interfaces;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.FileStorage;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace ITManagement.IntegrationTests;
public class LimitedStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "itam-limited-" + Guid.NewGuid().ToString("N"));
    // Tạo dịch vụ lưu trữ cục bộ dùng thư mục riêng của ca kiểm thử.
    private IFileStorageService Storage() => new LocalFileStorageService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:RootPath"] = root }).Build());
    /// <summary>Kiểm thử lưu đúng kích thước thực tế mà không cần biết trước độ dài luồng.</summary>
    [Theory]
    [InlineData(0, 0)] [InlineData(4, 4)] [InlineData(3, 4)]
    public async Task Saves_exact_actual_size_without_needing_stream_length(int size, int limit)
    {
        var storage = Storage(); using var input = new NonSeekable(size);
        var result = await storage.SaveLimitedAsync(Guid.NewGuid(), "../name.txt", input, limit);
        Assert.Equal(size, result.SizeBytes);
        await using var read = await storage.ReadAsync(result.StoragePath); Assert.Equal(size, read.Length);
    }
    /// <summary>Kiểm thử tệp quá giới hạn hoặc luồng lỗi không để lại blob ghi dở.</summary>
    [Fact]
    public async Task Oversize_and_failed_stream_leave_no_partial_blob()
    {
        var storage = Storage(); var owner = Guid.NewGuid();
        using var large = new NonSeekable(90000);
        await Assert.ThrowsAsync<QuotaExceededException>(() => storage.SaveLimitedAsync(owner, "a", large, 81920));
        using var broken = new NonSeekable(90000, fail: true);
        await Assert.ThrowsAsync<IOException>(() => storage.SaveLimitedAsync(owner, "a", broken, 100000));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }
    /// <summary>Kiểm thử hủy thao tác hoặc giới hạn âm không để lại blob ghi dở.</summary>
    [Fact]
    public async Task Cancellation_and_negative_limit_leave_no_partial_blob()
    {
        var storage = Storage(); using var input = new NonSeekable(10); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveLimitedAsync(Guid.NewGuid(), "a", input, 20, cancel.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => storage.SaveLimitedAsync(Guid.NewGuid(), "a", input, -1));
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }
    /// <summary>Kiểm thử hủy sau khi ghi một phần sẽ xóa blob dở dang.</summary>
    [Fact]
    public async Task Cancellation_after_partial_write_removes_blob()
    {
        var storage = Storage();
        using var cancel = new CancellationTokenSource();
        using var input = new NonSeekable(90000, cancelAfterFirstRead: cancel);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.SaveLimitedAsync(Guid.NewGuid(), "a", input, 100000, cancel.Token));
        Assert.True(input.PartialWriteObserved);
        Assert.Empty(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
    }
    // Khởi tạo luồng kiểm thử không hỗ trợ tua và có thể mô phỏng lỗi hoặc hủy.
    private sealed class NonSeekable(int size, bool fail = false, CancellationTokenSource? cancelAfterFirstRead = null) : Stream
    {
        private readonly int initialSize = size;
        private int remaining = size;
        public bool PartialWriteObserved { get; private set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        /// <summary>Đọc dữ liệu từ luồng kiểm thử.</summary>
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        /// <summary>Đọc dữ liệu từ luồng.</summary>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (cancelAfterFirstRead is not null && remaining < initialSize)
            {
                PartialWriteObserved = true;
                cancelAfterFirstRead.Cancel();
            }
            ct.ThrowIfCancellationRequested();
            if (fail && remaining < initialSize) throw new IOException("Test read failure");
            var count = Math.Min(buffer.Length, remaining); buffer.Span[..count].Clear(); remaining -= count; return ValueTask.FromResult(count);
        }
        /// <summary>Xả dữ liệu đang chờ ghi của luồng kiểm thử.</summary>
        public override void Flush() => throw new NotSupportedException(); public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <summary>Đặt độ dài luồng kiểm thử.</summary>
        public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    /// <summary>Giải phóng tài nguyên của luồng kiểm thử.</summary>
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
