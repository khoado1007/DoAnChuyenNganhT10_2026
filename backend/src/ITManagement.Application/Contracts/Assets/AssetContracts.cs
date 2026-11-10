using System.ComponentModel.DataAnnotations;
using ITManagement.Domain.Enums;
namespace ITManagement.Application.Contracts.Assets;

public sealed record CreateAssetRequest(
    [Required, StringLength(50)] string AssetCode,
    [Required, StringLength(200)] string Name,
    [EnumDataType(typeof(AssetType))] AssetType Type = AssetType.Desktop,
    [StringLength(100)] string SerialNumber = "",
    [StringLength(100)] string Manufacturer = "",
    [StringLength(100)] string Model = "",
    [StringLength(200)] string Location = "",
    [StringLength(1000)] string Notes = "");
public sealed record UpdateAssetRequest(
    [Required, StringLength(200)] string Name,
    [EnumDataType(typeof(AssetType))] AssetType Type,
    [EnumDataType(typeof(AssetStatus))] AssetStatus Status,
    [StringLength(100)] string SerialNumber = "",
    [StringLength(100)] string Manufacturer = "",
    [StringLength(100)] string Model = "",
    [StringLength(200)] string Location = "",
    [StringLength(1000)] string Notes = "");
public sealed record AssetQuery(
    [Range(1, 100000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(100)] string? Search = null,
    [EnumDataType(typeof(AssetStatus))] AssetStatus? Status = null);
public sealed record AssetLookupResponse(Guid Id, string AssetCode, string Name, string Status);
public sealed record AssetResponse(Guid Id, string AssetCode, string Name, AssetType Type,
    AssetStatus Status, string SerialNumber, string Manufacturer, string Model, string Location,
    string Notes, DateTime CreatedAt, DateTime? UpdatedAt);
