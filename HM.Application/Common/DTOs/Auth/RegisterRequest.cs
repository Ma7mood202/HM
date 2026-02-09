using HM.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Request model for user registration (form-data). For driver, NationalIdFrontImage and NationalIdBackImage are required.
/// </summary>
public class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserType UserType { get; set; }
    public IFormFile? NationalIdFrontImage { get; set; }
    public IFormFile? NationalIdBackImage { get; set; }
}
