using ITManagement.Domain.Enums;

namespace ITManagement.Application.Common.Interfaces;

/// <summary>
/// Abstraction để lấy thông tin user hiện tại từ HTTP context.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
