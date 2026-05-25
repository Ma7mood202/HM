using HM.AdminPanel.Authorization;
using HM.AdminPanel.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HM.AdminPanel.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminAuth(this IServiceCollection services)
    {
        services.AddAuthentication(o =>
            {
                o.DefaultScheme             = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultChallengeScheme    = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultSignInScheme       = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultSignOutScheme      = CookieAuthenticationDefaults.AuthenticationScheme;
                o.DefaultForbidScheme       = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(o =>
            {
                o.LoginPath        = "/Account/Login";
                o.LogoutPath       = "/Account/Logout";
                o.AccessDeniedPath = "/Account/AccessDenied";
                o.ExpireTimeSpan   = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                o.Cookie.Name       = "HM.Admin";
                o.Cookie.HttpOnly   = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.Cookie.SameSite   = SameSiteMode.Lax;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPolicies.RequireAdmin,
                p => p.RequireRole(AdminRoles.SuperAdmin, AdminRoles.Support, AdminRoles.ReadOnly));
            options.AddPolicy(AdminPolicies.RequireSuperAdmin,
                p => p.RequireRole(AdminRoles.SuperAdmin));
            options.AddPolicy(AdminPolicies.RequireWriteAccess,
                p => p.RequireRole(AdminRoles.SuperAdmin, AdminRoles.Support));

            options.FallbackPolicy = options.GetPolicy(AdminPolicies.RequireAdmin);
        });

        return services;
    }

    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<LoginThrottleService>();
        services.AddScoped<IAdminAuditLogger, AdminAuditLogger>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<AuditActionFilter>();
        return services;
    }
}
