using System.Text.Json.Serialization;
using Cleanuparr.Api.Middleware;
using Cleanuparr.Infrastructure.Health;
using Cleanuparr.Infrastructure.Hubs;
using Cleanuparr.Infrastructure.Logging;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi.Models;
using System.Text;

namespace Cleanuparr.Api.DependencyInjection;

public static class ApiDI
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        
        // Add API-specific services
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            });
        services.AddEndpointsApiExplorer();
        
        // Add SignalR for real-time updates
        services
            .AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        
        // Add health status broadcaster
        services.AddHostedService<HealthStatusBroadcaster>();
        
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Cleanuparr API",
                Version = "v1",
                Description = "API for managing media downloads and cleanups",
                Contact = new OpenApiContact
                {
                    Name = "Cleanuparr Team"
                }
            });
        });

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
            OnPrepareResponse = ctx =>
            {
                // Cache static assets for 30 days
                // if (ctx.File.Name.EndsWith(".js") || ctx.File.Name.EndsWith(".css"))
                // {
                //     ctx.Context.Response.Headers.CacheControl = "public,max-age=2592000";
                // }
            }
        });
        
        // Add the global exception handling middleware first
        app.UseMiddleware<ExceptionMiddleware>();
        
        app.UseCors("Any");
        app.UseRouting();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("v1/swagger.json", "Cleanuparr API v1");
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "Cleanuparr API Documentation";
            });
        }

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
        });
        
        // Map SignalR hubs
        app.MapHub<HealthStatusHub>("/api/hubs/health");
        app.MapHub<AppHub>("/api/hubs/app");
        
        app.MapGet("/manifest.webmanifest", (HttpContext context) =>
        {
            var basePath = context.Request.PathBase.HasValue
                ? context.Request.PathBase.Value
                : "/";

            var manifest = new
            {
                name = "Cleanuparr",
                short_name = "Cleanuparr",
                start_url = basePath,
                display = "standalone",
                background_color = "#ffffff",
                theme_color = "#ffffff",
                icons = new[]
                {
                    new {
                        src = "assets/icons/icon-192x192.png",
                        sizes = "192x192",
                        type = "image/png"
                    },
                    new {
                        src = "assets/icons/icon-512x512.png",
                        sizes = "512x512",
                        type = "image/png"
                    }
                }
            };

            return Results.Json(manifest, contentType: "application/manifest+json");
        });

        return app;
    }
}
