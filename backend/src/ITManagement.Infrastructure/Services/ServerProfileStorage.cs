using ITManagement.Domain.Exceptions;
using Microsoft.Extensions.Configuration;

namespace ITManagement.Infrastructure.Services;

/// <summary>Private immutable blobs. User file names are metadata and never filesystem paths.</summary>
internal sealed class ServerProfileStorage(IConfiguration configuration)
{
    public const int MaxBytes = 8 * 1024 * 1024;
    private string Root => Path.GetFullPath(configuration["Simulation:StorageRoot"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "storage", "simulation"));

    private string BlobPath(Guid device, Guid user, Guid profile, string key)
    {
        if (!Guid.TryParseExact(key, "N", out _)) throw new SimulationException(409, "STORAGE_METADATA_INVALID", "Tham chiếu file không hợp lệ.");
        var root = Root;
        if (root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(s => s.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Simulation storage must be outside wwwroot.");
        var path = Path.Combine(root, device.ToString("N"), user.ToString("N"), profile.ToString("N"), key);
        // Local administrators control the storage root; refuse symlinks/junctions rather than following them.
        for (string? current = path; current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new SimulationException(409, "STORAGE_LINK_REJECTED", "Vùng lưu trữ không được chứa liên kết thư mục/file.");
        return path;
    }

    public async Task<string> WriteAsync(Guid device, Guid user, Guid profile, byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length > MaxBytes) throw new SimulationException(413, "FILE_TOO_LARGE", "File tối đa 8 MiB.");
        var key = Guid.NewGuid().ToString("N");
        var path = BlobPath(device, user, profile, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        BlobPath(device, user, profile, key);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
        stream.Flush(flushToDisk: true);
        // SQL will publish this key only after a durable write. Failed/rolled-back keys stay unreferenced.
        return key;
    }

    public async Task<byte[]> ReadAsync(Guid device, Guid user, Guid profile, string? key, CancellationToken ct)
    {
        if (key is null) throw new SimulationException(409, "FILE_NOT_ON_SERVER", "File cũ chưa được chuyển vào vùng lưu trữ server.");
        var path = BlobPath(device, user, profile, key);
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous);
            if (stream.Length > MaxBytes) throw new SimulationException(413, "FILE_TOO_LARGE", "File tối đa 8 MiB.");
            var bytes = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(bytes, ct);
            return bytes;
        }
        catch (FileNotFoundException) { throw new SimulationException(409, "STORAGE_FILE_MISSING", "Không tìm thấy nội dung file trong kho lưu trữ. Metadata được giữ để kiểm tra."); }
        catch (DirectoryNotFoundException) { throw new SimulationException(409, "STORAGE_FILE_MISSING", "Kho lưu trữ chưa sẵn sàng. Metadata được giữ để kiểm tra."); }
    }
}
