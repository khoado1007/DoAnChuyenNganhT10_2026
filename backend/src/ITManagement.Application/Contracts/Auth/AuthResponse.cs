using ITManagement.Application.Contracts.Users;

namespace ITManagement.Application.Contracts.Auth;

public sealed record AuthResponse(string AccessToken, UserResponse User);
