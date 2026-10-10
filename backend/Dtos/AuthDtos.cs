using System.ComponentModel.DataAnnotations;

namespace CampusEats.Api.Dtos;

public record RegisterDto(
    [Required, StringLength(80)] string Name,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

public record LoginDto(string Email, string Password);