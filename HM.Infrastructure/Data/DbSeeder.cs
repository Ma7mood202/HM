using Microsoft.AspNetCore.Identity;
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
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("HM.Infrastructure.Data.DbSeeder");

        var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole<Guid>>>();
        if (roleManager != null)
        {
            var roleNames = new[] { "Merchant", "TruckAccount", "Driver" };
            foreach (var name in roleNames)
            {
                if (await roleManager.RoleExistsAsync(name))
                    continue;
                await roleManager.CreateAsync(new IdentityRole<Guid>(name));
                logger.LogInformation("Created role: {Role}", name);
            }
        }
    }
}
