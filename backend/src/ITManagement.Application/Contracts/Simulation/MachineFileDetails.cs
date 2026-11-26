using ITManagement.Application.Common.Models;

namespace ITManagement.Application.Contracts.Simulation;

public sealed record MachineFileInfo(string Name, string Extension, string Folder, long SizeBytes, string Owner);
public sealed record MachineFileEvent(string? FileName, string Kind, DateTime CompletedAt, string Owner);
public sealed record MachineFileDetails(PagedResult<MachineFileInfo> Files, PagedResult<MachineFileEvent> History);
