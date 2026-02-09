using HM.Application.Common.DTOs.Auth;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Hm.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IFileUploadService _fileUpload;

    public AuthController(IAuthService authService, IFileUploadService fileUpload)
    {
        _authService = authService;
        _fileUpload = fileUpload;
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized();
        await _authService.ChangePasswordAsync(userId.Value, request, cancellationToken);
        return NoContent();
    }

    /// <summary>Register with form data. For driver, NationalIdFrontImage and NationalIdBackImage (files) are required.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Register([FromForm] RegisterRequest request, CancellationToken cancellationToken)
    {
        string? frontUrl = null, backUrl = null;
        if (request.UserType == UserType.Driver)
        {
            if (request.NationalIdFrontImage == null || request.NationalIdFrontImage.Length == 0)
                return BadRequest("National ID front image is required for driver registration.");
            if (request.NationalIdBackImage == null || request.NationalIdBackImage.Length == 0)
                return BadRequest("National ID back image is required for driver registration.");
            frontUrl = await _fileUpload.SaveImageAsync(request.NationalIdFrontImage, "driver-national-id", cancellationToken);
            backUrl = await _fileUpload.SaveImageAsync(request.NationalIdBackImage, "driver-national-id", cancellationToken);
        }
        var result = await _authService.RegisterAsync(request, frontUrl, backUrl, cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyOtpAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResendOtpAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Accept driver invitation. Use form data; NationalIdFrontImage and NationalIdBackImage (files) are required.</summary>
    [HttpPost("driver/accept-invitation")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AcceptDriverInvitation([FromQuery] string token, [FromForm] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (request.NationalIdFrontImage == null || request.NationalIdFrontImage.Length == 0)
            return BadRequest("National ID front image is required.");
        if (request.NationalIdBackImage == null || request.NationalIdBackImage.Length == 0)
            return BadRequest("National ID back image is required.");
        var frontUrl = await _fileUpload.SaveImageAsync(request.NationalIdFrontImage, "driver-national-id", cancellationToken);
        var backUrl = await _fileUpload.SaveImageAsync(request.NationalIdBackImage, "driver-national-id", cancellationToken);
        var result = await _authService.AcceptDriverInvitationAsync(token, request, frontUrl, backUrl, cancellationToken);
        return Ok(result);
    }
}
