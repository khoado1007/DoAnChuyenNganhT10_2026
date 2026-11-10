namespace ITManagement.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Technician = 2,
    Employee = 3
}

public enum UserStatus
{
    Active = 1,
    Locked = 2,
    Inactive = 3
}

public enum AssetStatus
{
    Available = 1,    // Sẵn sàng cấp phát
    Assigned = 2,     // Đang được cấp phát
    UnderMaintenance = 3, // Đang bảo trì
    Retired = 4,      // Đã nghỉ hưu
    Disabled = 5      // Bị vô hiệu hóa
}

public enum AssetType
{
    Desktop = 1,
    Laptop = 2,
    Server = 3,
    Printer = 4,
    NetworkDevice = 5,
    Other = 6
}

public enum AssignmentStatus
{
    Active = 1,    // Đang sử dụng
    Returned = 2,  // Đã trả lại
    Revoked = 3    // Bị thu hồi
}

public enum TicketStatus
{
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5,
    Cancelled = 6
}

public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum TicketCategory
{
    Hardware = 1,
    Software = 2,
    Network = 3,
    Account = 4,
    Storage = 5,
    Other = 6
}
