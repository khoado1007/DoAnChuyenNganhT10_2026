using System.ComponentModel.DataAnnotations;
using ITManagement.Domain.Enums;

namespace ITManagement.Application.Contracts.Users;

public sealed record CreateUserRequest(
    [Required, StringLength(100)] string Username,
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(200)] string FullName,
    [Required, StringLength(256, MinimumLength = 8)] string Password,
    [StringLength(100)] string Department,
    [EnumDataType(typeof(UserRole))] UserRole Role);
