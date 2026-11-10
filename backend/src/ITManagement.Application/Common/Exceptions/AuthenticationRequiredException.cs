namespace ITManagement.Application.Common.Exceptions;

/// <summary>Khởi tạo ngoại lệ cho yêu cầu thiếu thông tin xác thực.</summary>
public sealed class AuthenticationRequiredException(string message) : Exception(message);
