using ITManagement.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ITManagement.Infrastructure.Identity;

public class JwtService : IJwtService
{
    private readonly IConfiguration _config;

    /// <summary>Khởi tạo dịch vụ phát hành và kiểm tra token.</summary>
    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>Tạo access token JWT cho người dùng đã xác thực.</summary>
    public string GenerateAccessToken(Guid userId, string username, string role, string email, int securityVersion = 0)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim("security_version", securityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Tạo refresh token ngẫu nhiên để gia hạn phiên.</summary>
    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>Kiểm tra token JWT và trích xuất mã người dùng, tên đăng nhập cùng vai trò.</summary>
    public (Guid userId, string username, string role) ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!);

        handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = _config["Jwt:Audience"],
            ValidateLifetime = true
        }, out var validatedToken);

        var jwt = (JwtSecurityToken)validatedToken;
        var userId = Guid.Parse(jwt.Subject);
        var username = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value;
        var role = jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value;

        return (userId, username, role);
    }
}
