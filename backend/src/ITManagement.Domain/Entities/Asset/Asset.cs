using ITManagement.Domain.Common;
using ITManagement.Domain.Enums;

namespace ITManagement.Domain.Entities.Asset;

/// <summary>
/// Thiết bị CNTT (máy tính, laptop, printer...) trong hệ thống ITAM.
/// </summary>
public class Asset : BaseEntity
{
    public string AssetCode { get; set; } = string.Empty;   // Mã tài sản, unique
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; }
    public AssetStatus Status { get; set; } = AssetStatus.Available;
    public string SerialNumber { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime? PurchaseDate { get; set; }
    public decimal? PurchasePrice { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // Navigation
    public ICollection<AssetAssignment> Assignments { get; set; } = [];
}
