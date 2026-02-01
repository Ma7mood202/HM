using HM.Application.Common.DTOs.Auth;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Domain.Enums;
using HM.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HM.Infrastructure.Services;

/// <summary>
/// Authentication and registration. Phone + Password + OTP verification.
/// OTP expires in 5 minutes. Identity username = phone number.
/// </summary>
public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan OtpExpiry = TimeSpan.FromMinutes(5);
    private static readonly Random Random = new();

    private readonly IApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenGenerator _jwt;
    private readonly IConfiguration _configuration;

    public AuthService(
        IApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        JwtTokenGenerator jwt,
        IConfiguration configuration)
    {
        _db = db;
        _userManager = userManager;
        _jwt = jwt;
        _configuration = configuration;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByNameAsync(request.PhoneNumber);
        if (existingUser != null)
            throw new InvalidOperationException("Phone number is already registered.");

        var appUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.PhoneNumber,
            NormalizedUserName = request.PhoneNumber.ToUpperInvariant(),
            Email = request.Email ?? string.Empty,
            NormalizedEmail = (request.Email ?? string.Empty).ToUpperInvariant(),
            EmailConfirmed = false,
            PhoneNumber = request.PhoneNumber,
            PhoneNumberConfirmed = false
        };

        var result = await _userManager.CreateAsync(appUser, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var domainUser = new User
        {
            Id = appUser.Id,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email ?? string.Empty,
            UserType = request.UserType,
            IsActive = true,
            IsOtpVerified = false,
            OtpPurpose = OtpPurpose.Verification,
            CreatedAt = DateTime.Now
        };
        SetOtp(domainUser, OtpPurpose.Verification);
        _db.Users.Add(domainUser);

        if (request.UserType == UserType.Merchant)
        {
            _db.MerchantProfiles.Add(new MerchantProfile
            {
                Id = Guid.NewGuid(),
                UserId = domainUser.Id,
                CompanyName = request.CompanyName,
                IsVerified = false,
                CreatedAt = DateTime.Now
            });
        }
        else if (request.UserType == UserType.TruckAccount)
        {
            _db.TruckAccounts.Add(new TruckAccount
            {
                Id = Guid.NewGuid(),
                UserId = domainUser.Id,
                DisplayName = request.DisplayName ?? request.FullName,
                IsAvailable = true,
                CreatedAt = DateTime.Now
            });
        }
        else if (request.UserType == UserType.Driver)
        {
            _db.DriverProfiles.Add(new DriverProfile
            {
                Id = Guid.NewGuid(),
                UserId = domainUser.Id,
                FullName = request.FullName,
                IsVerified = false,
                CreatedAt = DateTime.Now
            });
        }

        var roleName = request.UserType.ToString();
        await _userManager.AddToRoleAsync(appUser, roleName);

        await _db.SaveChangesAsync(cancellationToken);

        return new RegisterResponse
        {
            Success = true,
            Message = "Registration successful. Please verify your phone with the OTP sent.",
            RequiresOtpVerification = true,
            UserId = domainUser.Id
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var appUser = await _userManager.FindByNameAsync(request.PhoneNumber);
        if (appUser == null)
            throw new UnauthorizedAccessException("Invalid phone number or password.");

        var valid = await _userManager.CheckPasswordAsync(appUser, request.Password);
        if (!valid)
            throw new UnauthorizedAccessException("Invalid phone number or password.");

        var user = await _db.Users.FindAsync([appUser.Id], cancellationToken);
        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("User not found or inactive.");

        if (!user.IsOtpVerified)
        {
            SetOtp(user, OtpPurpose.Verification);
            await _db.SaveChangesAsync(cancellationToken);
            return new AuthResponse
            {
                UserId = user.Id,
                UserType = user.UserType,
                RequiresOtpVerification = true
            };
        }

        var (token, expiration) = _jwt.Generate(user.Id, user.PhoneNumber, user.Email, user.UserType);
        return new AuthResponse
        {
            UserId = user.Id,
            UserType = user.UserType,
            Token = token,
            Expiration = expiration,
            RequiresOtpVerification = false
        };
    }

    public async Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        var appUser = await _userManager.FindByNameAsync(request.PhoneNumber);
        if (appUser == null)
            throw new UnauthorizedAccessException("Invalid phone number.");

        var user = await _db.Users.FindAsync([appUser.Id], cancellationToken);
        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("User not found or inactive.");

        if (user.OtpPurpose != OtpPurpose.Verification)
            throw new InvalidOperationException("No pending OTP verification for this account.");

        if (string.IsNullOrEmpty(user.OtpCode) || user.OtpExpiresAt == null || user.OtpExpiresAt < DateTime.Now)
            throw new InvalidOperationException("OTP has expired. Please request a new one.");

        if (user.OtpCode != request.OtpCode)
            throw new InvalidOperationException("Invalid OTP code.");

        user.IsOtpVerified = true;
        user.OtpCode = null;
        user.OtpExpiresAt = null;
        user.OtpPurpose = OtpPurpose.None;
        await _db.SaveChangesAsync(cancellationToken);

        var (token, expiration) = _jwt.Generate(user.Id, user.PhoneNumber, user.Email, user.UserType);
        return new AuthResponse
        {
            UserId = user.Id,
            UserType = user.UserType,
            Token = token,
            Expiration = expiration,
            RequiresOtpVerification = false
        };
    }

    public async Task<MessageResponse> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken = default)
    {
        var appUser = await _userManager.FindByNameAsync(request.PhoneNumber);
        if (appUser == null)
            throw new InvalidOperationException("No account found with this phone number.");

        var user = await _db.Users.FindAsync([appUser.Id], cancellationToken);
        if (user == null || !user.IsActive)
            throw new InvalidOperationException("User not found or inactive.");

        if (user.IsOtpVerified)
            throw new InvalidOperationException("Account is already verified. No OTP required.");

        SetOtp(user, OtpPurpose.Verification);
        await _db.SaveChangesAsync(cancellationToken);

        return new MessageResponse
        {
            Success = true,
            Message = "OTP has been resent. Please check your phone."
        };
    }

    public async Task<MessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var appUser = await _userManager.FindByNameAsync(request.PhoneNumber);
        if (appUser == null)
            throw new InvalidOperationException("No account found with this phone number.");

        var user = await _db.Users.FindAsync([appUser.Id], cancellationToken);
        if (user == null || !user.IsActive)
            throw new InvalidOperationException("User not found or inactive.");

        SetOtp(user, OtpPurpose.PasswordReset);
        await _db.SaveChangesAsync(cancellationToken);

        return new MessageResponse
        {
            Success = true,
            Message = "If an account exists for this phone number, a password reset code has been sent."
        };
    }

    public async Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var appUser = await _userManager.FindByNameAsync(request.PhoneNumber);
        if (appUser == null)
            throw new UnauthorizedAccessException("Invalid phone number.");

        var user = await _db.Users.FindAsync([appUser.Id], cancellationToken);
        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("User not found or inactive.");

        if (user.OtpPurpose != OtpPurpose.PasswordReset)
            throw new InvalidOperationException("No pending password reset OTP. Please request a new one.");

        if (string.IsNullOrEmpty(user.OtpCode) || user.OtpExpiresAt == null || user.OtpExpiresAt < DateTime.Now)
            throw new InvalidOperationException("OTP has expired. Please request a new one.");

        if (user.OtpCode != request.OtpCode)
            throw new InvalidOperationException("Invalid OTP code.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(appUser);
        var result = await _userManager.ResetPasswordAsync(appUser, token, request.NewPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        user.OtpCode = null;
        user.OtpExpiresAt = null;
        user.OtpPurpose = OtpPurpose.None;
        await _db.SaveChangesAsync(cancellationToken);

        return new MessageResponse
        {
            Success = true,
            Message = "Your password has been reset successfully. You can now log in with your new password."
        };
    }

    public async Task<AuthResponse> AcceptDriverInvitationAsync(string token, RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var invitation = await _db.DriverInvitations
            .FirstOrDefaultAsync(i => i.Token == token && !i.IsUsed && i.ExpiresAt > DateTime.Now, cancellationToken);
        if (invitation == null)
            throw new InvalidOperationException("Invalid or expired invitation token.");

        var existingUser = await _userManager.FindByNameAsync(request.PhoneNumber);
        if (existingUser != null)
            throw new InvalidOperationException("Phone number is already registered.");

        var appUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = request.PhoneNumber,
            NormalizedUserName = request.PhoneNumber.ToUpperInvariant(),
            Email = request.Email ?? string.Empty,
            NormalizedEmail = (request.Email ?? string.Empty).ToUpperInvariant(),
            EmailConfirmed = false,
            PhoneNumber = request.PhoneNumber,
            PhoneNumberConfirmed = false
        };

        var result = await _userManager.CreateAsync(appUser, request.Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        var domainUser = new User
        {
            Id = appUser.Id,
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Email = request.Email ?? string.Empty,
            UserType = UserType.Driver,
            IsActive = true,
            IsOtpVerified = false,
            OtpPurpose = OtpPurpose.Verification,
            CreatedAt = DateTime.Now
        };
        SetOtp(domainUser, OtpPurpose.Verification);
        _db.Users.Add(domainUser);

        var driverProfile = new DriverProfile
        {
            Id = Guid.NewGuid(),
            UserId = domainUser.Id,
            FullName = request.FullName,
            IsVerified = false,
            CreatedAt = DateTime.Now
        };
        _db.DriverProfiles.Add(driverProfile);

        await _userManager.AddToRoleAsync(appUser, UserType.Driver.ToString());

        invitation.IsUsed = true;

        var shipment = await _db.Shipments.FindAsync([invitation.ShipmentId], cancellationToken);
        if (shipment != null)
        {
            shipment.DriverProfileId = driverProfile.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            UserId = domainUser.Id,
            UserType = UserType.Driver,
            RequiresOtpVerification = true
        };
    }

    private void SetOtp(User user, OtpPurpose purpose)
    {
        user.OtpCode = GenerateOtp();
        user.OtpExpiresAt = DateTime.Now.Add(OtpExpiry);
        user.OtpPurpose = purpose;
    }

    private string GenerateOtp()
    {
        var staticOtp = _configuration["Auth:StaticOtpForTesting"];
        if (!string.IsNullOrEmpty(staticOtp) && staticOtp.Length == 6)
            return staticOtp;
        return Random.Next(100000, 999999).ToString();
    }
}
