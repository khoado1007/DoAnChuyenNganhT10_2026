using System.ComponentModel.DataAnnotations;

namespace ITManagement.Application.Contracts.Auth;

public sealed class ChangePasswordRequest
{
    [Required, StringLength(256)] public string CurrentPassword { get; init; } = "";
    [Required, StringLength(256, MinimumLength = 8)] public string NewPassword { get; init; } = "";
    [Required, StringLength(256), Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    public string ConfirmPassword { get; init; } = "";
}
