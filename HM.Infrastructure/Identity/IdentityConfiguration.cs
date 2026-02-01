using Microsoft.AspNetCore.Identity;

namespace HM.Infrastructure.Identity;

/// <summary>
/// Configures ASP.NET Core Identity options (password, lockout, user).
/// JWT token generation is handled in the WebApi layer.
/// </summary>
public static class IdentityConfiguration
{
    public static void ConfigureIdentityOptions(IdentityOptions options)
    {
        // Password
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 8;

        // Lockout
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

        // User - email optional, phone is primary identifier
        options.User.RequireUniqueEmail = false;
    }
}
