using System.Text.RegularExpressions;
using ITManagement.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
namespace ITManagement.Infrastructure.FileStorage;

// Storage keys are private, generated identifiers. Display names never become paths.
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string root;
    /// <summary>Khởi tạo dịch vụ lưu tệp cục bộ từ cấu hình.</summary>
    public LocalFileStorageService(IConfiguration config)
    {
        root = Path.GetFullPath(config["FileStorage:RootPath"] ?? throw new InvalidOperationException("FileStorage:RootPath không được cấu hình."));
        Directory.CreateDirectory(root);
        RejectLink(root);
    }
    // Từ chối đường dẫn liên kết để ngăn truy cập ngoài vùng lưu trữ.
    private static void RejectLink(string path)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Storage không cho phép liên kết hoặc reparse point.");
        }
        catch (FileNotFoundException) { }
        catch (DirectoryNotFoundException) { }
    }
    // Giải quyết khóa lưu trữ thành đường dẫn an toàn trong thư mục gốc.
    private string Resolve(string key)
    {
        var parts = key.Replace('\\', '/').Split('/');
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "D", out _)
            || !Regex.IsMatch(parts[1], @"\A(?:[a-fA-F0-9]{32}|[a-fA-F0-9]{8}(?:-[a-fA-F0-9]{4}){3}-[a-fA-F0-9]{12})(?:\.[a-zA-Z0-9]{1,16})?\z"))
            throw new ArgumentException("Storage key không hợp lệ.");
        var directory = Path.Combine(root, parts[0]);
        var path = Path.Combine(directory, parts[1]);
        RejectLink(root); RejectLink(directory); RejectLink(path);
        return path;
    }
    /// <summary>Lưu luồng dữ liệu vào vùng lưu trữ cục bộ.</summary>
    public async Task<string> SaveAsync(Guid userId, string fileName, Stream content, CancellationToken ct = default)
        => (await SaveLimitedAsync(userId, fileName, content, long.MaxValue, ct)).StoragePath;

    /// <summary>Lưu tệp đồng thời giới hạn kích thước thực tế được ghi.</summary>
    public async Task<StoredFile> SaveLimitedAsync(Guid userId, string fileName, Stream content, long maxBytes, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxBytes);
        ct.ThrowIfCancellationRequested();
        var key = $"{userId:D}/{Guid.NewGuid():N}";
        var path = Resolve(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        path = Resolve(key);
        var created = false;
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            created = true;
            var buffer = new byte[81920];
            long total = 0;
            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(), ct);
                if (read == 0) break;
                if (read > maxBytes - total)
                    throw new ITManagement.Domain.Exceptions.QuotaExceededException(total > long.MaxValue - read ? long.MaxValue : total + read, maxBytes);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                total += read;
            }
            return new StoredFile(key, total);
        }
        catch
        {
            if (created) File.Delete(path);
            throw;
        }
    }
    /// <summary>Kiểm tra các đối tượng và metadata trong vùng lưu trữ.</summary>
    public Task<StorageInventory> InspectAsync(Guid userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        RejectLink(root);
        var directory = Path.Combine(root, userId.ToString("D"));
        RejectLink(directory);
        var keys = new List<string>();
        var skipped = 0;
        if (!Directory.Exists(directory)) return Task.FromResult(new StorageInventory(keys, skipped));
        foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var key = $"{userId:D}/{Path.GetFileName(entry)}";
                var path = Resolve(key);
                if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                { skipped++; continue; }
                keys.Add(key);
            }
            catch (ArgumentException) { skipped++; }
            catch (FileNotFoundException) { skipped++; }
        }
        return Task.FromResult(new StorageInventory(keys, skipped));
    }
    /// <summary>Mở luồng đọc của đối tượng lưu trữ.</summary>
    public Task<Stream> ReadAsync(string storagePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Stream stream = new FileStream(Resolve(storagePath), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Task.FromResult(stream);
    }
    /// <summary>Xóa đối tượng khỏi vùng lưu trữ cục bộ.</summary>
    public Task DeleteAsync(string storagePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); File.Delete(Resolve(storagePath)); return Task.CompletedTask;
    }
    /// <summary>Kiểm tra đối tượng lưu trữ có tồn tại hay không.</summary>
    public Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); return Task.FromResult(File.Exists(Resolve(storagePath)));
    }
}
