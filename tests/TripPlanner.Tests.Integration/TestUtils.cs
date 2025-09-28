using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TripPlanner.Tests.Integration;

public static class TestUtils
{
    public static async Task CleanupWwwrootAsync(WebApplicationFactory<Program> factory)
    {
        try
        {
            using var scope = factory.Services.CreateScope();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var root = env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var uploads = Path.Combine(root, "uploads");
            if (Directory.Exists(uploads))
            {
                // Attempt multiple times in case any file handles are still closing
                for (var i = 0; i < 3; i++)
                {
                    try
                    {
                        Directory.Delete(uploads, recursive: true);
                        break;
                    }
                    catch
                    {
                        await Task.Delay(50);
                    }
                }
            }
        }
        catch
        {
            // Ignore cleanup failures; tests should not fail because of best-effort cleanup
        }
    }
}
