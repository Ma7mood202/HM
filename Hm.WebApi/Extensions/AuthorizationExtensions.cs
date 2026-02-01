using HM.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Hm.WebApi.Extensions;

/// <summary>
/// Configures role-based authorization using UserType enum names.
/// </summary>
public static class AuthorizationExtensions
{
    public static IServiceCollection AddJwtAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy("Merchant", policy => policy.RequireRole(nameof(UserType.Merchant)))
            .AddPolicy("TruckAccount", policy => policy.RequireRole(nameof(UserType.TruckAccount)))
            .AddPolicy("Driver", policy => policy.RequireRole(nameof(UserType.Driver)));

        return services;
    }
}
