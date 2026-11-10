namespace ITManagement.Application.Common.Interfaces;

/// <summary>
/// JWT token generation service.
/// </summary>
public interface IJwtService
{
    /// <summary>Tạo access token cho người dùng đã xác thực.</summary>
    string GenerateAccessToken(Guid userId, string username, string role, string email, int securityVersion = 0);
    /// <summary>Tạo refresh token ngẫu nhiên để gia hạn phiên.</summary>
    string GenerateRefreshToken();
    (Guid userId, string username, string role) ValidateToken(string token);
}
