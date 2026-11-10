using System.ComponentModel.DataAnnotations;

namespace ITManagement.Application.Contracts.Auth;

public sealed record LoginRequest(
    [Required, StringLength(100)] string Username,
    [Required, StringLength(256)] string Password);
