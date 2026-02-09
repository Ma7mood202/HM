using HM.Infrastructure;
using HM.Infrastructure.Data;
using HM.Infrastructure.Options;
using Hm.WebApi.Extensions;
using Hm.WebApi.Middlewares;
using Hm.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace Hm.WebApi;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var configuration = builder.Configuration;

        builder.Services.AddInfrastructure(configuration);
        // Resolve Firebase credentials path relative to app content root (e.g. Secrets/firebase-service-account.json)
        builder.Services.Configure<FirebaseOptions>(options =>
        {
            if (!string.IsNullOrEmpty(options.CredentialsPath) && !Path.IsPathRooted(options.CredentialsPath))
                options.CredentialsPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.CredentialsPath));
        });
        builder.Services.AddScoped<IFileUploadService, FileUploadService>();
        builder.Services.AddJwtAuthentication(configuration);
        builder.Services.AddJwtAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "HM API",
                Version = "v1",
                Description = "HM Web API"
            });
            options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                In = Microsoft.OpenApi.Models.ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
                Name = "Authorization",
                Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey
            });
            options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
            {
                {
                    new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                    {
                        Reference = new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(scope.ServiceProvider);
        }

        app.UseMiddleware<ExceptionHandlingMiddleware>();
        var enableSwagger = app.Configuration.GetValue<bool>("Swagger:Enabled") || app.Environment.IsDevelopment();
        if (enableSwagger)
        {
            app.UseSwagger();
            app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "HM API v1"));
        }
        app.UseHttpsRedirection();
        var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsPath),
            RequestPath = "/uploads"
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
}
