using ITManagement.Domain.Common;
using ITManagement.Domain.Enums;

namespace ITManagement.Domain.Entities.Asset;

/// <summary>
/// Máy đầu cuối mô phỏng trong hệ thống.
/// Nhân viên chọn một máy này rồi đăng nhập → truy cập dữ liệu cá nhân.
/// Máy mô phỏng ≠ máy vật lý; đây là endpoint ảo trong ứng dụng web.
/// </summary>
public class SimulatedDevice : BaseEntity
{
    public string DeviceName { get; set; } = string.Empty;   // VD: "Máy 1", "Máy 2"
    public string DisplayLabel { get; set; } = string.Empty;  // Hiển thị ở portal nhân viên
    public string Location { get; set; } = string.Empty;      // Phòng ban / vị trí
    public bool IsActive { get; set; } = true;                // false → không thể bắt đầu phiên mới

    /// <summary>
    /// Tài sản vật lý (Asset) tương ứng, nếu có.
    /// Một SimulatedDevice có thể map 1-1 với một Asset thực.
    /// </summary>
    public Guid? AssetId { get; set; }
    public Asset? Asset { get; set; }
}

