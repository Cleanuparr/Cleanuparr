using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Cleanuparr.Api.Json;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Hubs;
using Microsoft.AspNetCore.Http.Json;
using System.Text;
using Cleanuparr.Api.Middleware;
using Microsoft.Extensions.Options;

namespace Cleanuparr.Api.DependencyInjection;

public static class ApiDI
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            options.SerializerOptions.TypeInfoResolver = new SensitiveDataResolver(
                options.SerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());
        });

        // Make JsonSerializerOptions available for injection
        services.AddSingleton(sp =>
            sp.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions);

        // Add API-specific services
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.TypeInfoResolver = new SensitiveDataResolver(
                    options.JsonSerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());
            });
        services.AddEndpointsApiExplorer();

        // Add SignalR for real-time updates
        services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.PayloadSerializerOptions.TypeInfoResolver = new SensitiveDataResolver(
                    options.PayloadSerializerOptions.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver());
            });
        
        // Add health status broadcaster
        services.AddHostedService<HealthStatusBroadcaster>();

        return services;
    }

    public static WebApplication ConfigureApi(this WebApplication app)
    {
        ILogger<Program> logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        // Enable compression
        app.UseResponseCompression();
        
        // Serve static files with caching
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = _ => {}
        });
        
        // Add the global exception handling middleware first
        app.UseMiddleware<ExceptionMiddleware>();

        // Block non-auth requests until setup is complete
        app.UseMiddleware<SetupGuardMiddleware>();

        app.UseCors("Any");
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        
        // Custom SPA fallback to inject base path
        app.MapFallback(async context =>
        {
            var basePath = app.Configuration.GetValue<string>("BASE_PATH") ?? "/";
            
            // Normalize the base path (remove trailing slash if not root)
            if (basePath != "/" && basePath.EndsWith("/"))
            {
                basePath = basePath.TrimEnd('/');
            }
            
            var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
            var indexPath = Path.Combine(webRoot, "index.html");
            
            if (!File.Exists(indexPath))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("index.html not found");
                return;
            }
            
            var indexContent = await File.ReadAllTextAsync(indexPath);
            
            // Inject the base path into the HTML
            var scriptInjection = $@"
    <script>
      window['_server_base_path'] = '{basePath}';
    </script>";
            
            // Insert the script right before the existing script tag
            indexContent = indexContent.Replace(
                "  <script>",
                scriptInjection + "\n  <script>"
            );
            
            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync(indexContent, Encoding.UTF8);
        }).AllowAnonymous();
        
        // Map SignalR hubs
        app.MapHub<HealthStatusHub>("/api/hubs/health").RequireAuthorization();
        app.MapHub<AppHub>("/api/hubs/app").RequireAuthorization();
        
        app.MapGet("/manifest.webmanifest", (HttpContext context) =>
        {
            var basePath = context.Request.PathBase.HasValue
                ? context.Request.PathBase.Value
                : "/";

            var manifest = new
            {
                name = "Cleanuparr",
                short_name = "Cleanuparr",
                description = "Automated cleanup for *arr applications and download clients",
                start_url = basePath,
                display = "standalone",
                background_color = "#0e0a1a",
                theme_color = "#1a1135",
                icons = new[]
                {
                    new {
                        src = "icons/128.png",
                        sizes = "128x128",
                        type = "image/png"
                    },
                    new {
                        src = "icons/icon-192x192.png",
                        sizes = "192x192",
                        type = "image/png"
                    },
                    new {
                        src = "icons/icon-512x512.png",
                        sizes = "512x512",
                        type = "image/png"
                    }
                }
            };

            return Results.Json(manifest, contentType: "application/manifest+json");
        }).AllowAnonymous();

        return app;
    }
}
