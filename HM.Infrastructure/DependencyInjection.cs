using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Infrastructure.Data;
using HM.Infrastructure.Identity;
using HM.Infrastructure.Mappings;
using HM.Infrastructure.Options;
using HM.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HM.Infrastructure;

/// <summary>
/// Dependency injection for Infrastructure: EF Core, Identity, services, AutoMapper.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            IdentityConfiguration.ConfigureIdentityOptions(options))
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddAutoMapper(typeof(MappingProfile).Assembly);

        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMerchantService, MerchantService>();
        services.AddScoped<ITruckService, TruckService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<ICurrentProfileAccessor, CurrentProfileAccessor>();
        services.AddScoped<INotificationService, NotificationService>();

        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));

        return services;
    }
}
