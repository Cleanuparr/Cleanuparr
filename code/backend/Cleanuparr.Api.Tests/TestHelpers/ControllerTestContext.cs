using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cleanuparr.Api.Tests.TestHelpers;

/// <summary>
/// Attaches a minimal MVC <see cref="ControllerContext"/> (with a real <see cref="ProblemDetailsFactory"/>
/// and <see cref="HttpContext"/>) to a directly-instantiated controller so that
/// <c>this.ProblemResult(...)</c> can build problem-details responses in unit tests.
/// </summary>
public static class ControllerTestContext
{
    private static readonly IServiceProvider Services = BuildServices();

    private static IServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddControllers();
        services.AddProblemDetails();
        return services.BuildServiceProvider();
    }

    public static void Attach(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = Services },
        };
        controller.ProblemDetailsFactory = Services.GetRequiredService<ProblemDetailsFactory>();
    }
}
