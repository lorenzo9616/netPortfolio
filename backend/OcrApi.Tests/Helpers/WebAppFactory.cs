using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using OcrApi.Data;

namespace OcrApi.Tests.Helpers;

/// <summary>
/// WebApplicationFactory that replaces PostgreSQL with in-memory DB for tests.
/// Also injects test-safe JWT config values.
/// </summary>
public class OcrWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Auth:JwtSecret is read during WebApplication builder phase,
        // so we also set environment variables as in AuthControllerTests.
        Environment.SetEnvironmentVariable("Auth__JwtSecret",      "test-secret-key-that-is-long-enough-32chars");
        Environment.SetEnvironmentVariable("Auth__Username",        "testuser");
        Environment.SetEnvironmentVariable("Auth__Password",        "testpass");
        Environment.SetEnvironmentVariable("Auth__JwtExpiryHours",  "1");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Username"]       = "testuser",
                ["Auth:Password"]       = "testpass",
                ["Auth:JwtSecret"]      = "test-secret-key-that-is-long-enough-32chars",
                ["Auth:JwtExpiryHours"] = "1",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<OcrDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<OcrDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));
        });
    }

    /// <summary>Returns an HttpClient with a valid JWT bearer token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var token = body!["token"];
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
