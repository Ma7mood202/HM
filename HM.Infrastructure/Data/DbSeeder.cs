using HM.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HM.Infrastructure.Data;

/// <summary>
/// Seeds required lookup data. No test/demo data unless clearly marked.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("HM.Infrastructure.Data.DbSeeder");

        var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
        var config      = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (roleManager is null) return;

        var roleNames = new[] { "Merchant", "TruckAccount", "Driver",
                                "Admin", "SuperAdmin", "Support", "ReadOnly" };
        foreach (var name in roleNames)
        {
            if (await roleManager.RoleExistsAsync(name)) continue;
            await roleManager.CreateAsync(new IdentityRole<Guid>(name));
            logger.LogInformation("Created role: {Role}", name);
        }

        if (userManager is null) return;

        var seedEmail = config["AdminPanel:SeedAdmin:Email"];
        var seedPwd   = config["AdminPanel:SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPwd))
        {
            logger.LogWarning("AdminPanel:SeedAdmin not configured; skipping admin seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(seedEmail);
        if (existing is not null) return;

        var admin = new ApplicationUser
        {
            Id             = Guid.NewGuid(),
            UserName       = seedEmail,
            Email          = seedEmail,
            EmailConfirmed = true
        };
        var create = await userManager.CreateAsync(admin, seedPwd);
        if (!create.Succeeded)
        {
            logger.LogError("Failed to seed SuperAdmin: {Errors}",
                string.Join("; ", create.Errors.Select(e => e.Description)));
            return;
        }
        await userManager.AddToRolesAsync(admin, new[] { "Admin", "SuperAdmin" });
        logger.LogInformation("Seeded SuperAdmin: {Email}", seedEmail);
    }
}
