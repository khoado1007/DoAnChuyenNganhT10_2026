using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Simulation;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

public sealed partial class ProfileOperationService
{
    private static readonly string[] FileChanges = ["create", "write", "rename", "move", "copy", "delete", "restore"];

    public async Task<MachineFileDetails> GetMachineFileDetailsAsync(Guid assetId, Guid sessionId, int filesPage, int historyPage, CancellationToken ct)
    {
        await ValidateSessionAsync(assetId, sessionId, ct);
        var owners = from profile in db.LocalProfiles
                     join user in db.AppUsers on profile.UserId equals user.Id
                     where profile.AssetId == assetId
                     select new { profile.Id, user.Username };
        var files = from f in db.ProfileFiles
                    join owner in owners on f.ProfileId equals owner.Id
                    orderby f.Name, owner.Username, f.Id
                    select new MachineFileInfo(f.Name, f.Extension, f.Folder, f.SizeBytes, owner.Username);
        var history = from op in db.ProfileOperations
                      join owner in owners on op.ProfileId equals owner.Id
                      where op.State == "Completed" && op.CompletedAt.HasValue && FileChanges.Contains(op.Kind)
                      orderby op.CompletedAt descending, op.Id descending
                      select new MachineFileEvent(op.FileName, op.Kind, op.CompletedAt!.Value, owner.Username);
        return new(await ReadPageAsync(files, filesPage, ct), await ReadPageAsync(history, historyPage, ct));
    }

    private static async Task<PagedResult<T>> ReadPageAsync<T>(IQueryable<T> query, int requestedPage, CancellationToken ct)
    {
        const int size = 10;
        var count = await query.CountAsync(ct);
        var page = Math.Clamp(requestedPage, 1, Math.Max(1, (int)Math.Ceiling(count / (double)size)));
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return PagedResult<T>.Create(items, page, size, count);
    }
}
