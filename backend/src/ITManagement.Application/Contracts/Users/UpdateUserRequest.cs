using System.ComponentModel.DataAnnotations;
using ITManagement.Domain.Enums;
namespace ITManagement.Application.Contracts.Users;

public sealed record UpdateUserRequest(
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(200)] string FullName,
    [StringLength(100)] string Department,
    [EnumDataType(typeof(UserRole))] UserRole Role,
    [EnumDataType(typeof(UserStatus))] UserStatus Status);

public sealed record UserQuery(
    [Range(1, 100000)] int Page = 1,
    [Range(1, 100)] int PageSize = 20,
    [StringLength(100)] string? Search = null);
