using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OcrApi.Data;
using Xunit;

namespace OcrApi.Tests;

public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        // Auth:JwtSecret is read during WebApplication builder phase (before Build()),
        // so it must be present via environment variables — not via ConfigureAppConfiguration
        // which fires after the builder phase in minimal-API style programs.
        Environment.SetEnvironmentVariable("Auth__JwtSecret",      "test-secret-key-that-is-long-enough-32chars");
        Environment.SetEnvironmentVariable("Auth__Username",        "testuser");
        Environment.SetEnvironmentVariable("Auth__Password",        "testpass");
        Environment.SetEnvironmentVariable("Auth__JwtExpiryHours",  "1");

        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:Username"]       = "testuser",
                    ["Auth:Password"]       = "testpass",
                    ["Auth:JwtSecret"]      = "test-secret-key-that-is-long-enough-32chars",
                    ["Auth:JwtExpiryHours"] = "1",
                });
            });

            builder.ConfigureServices(services =>
            {
                // Remove the real Npgsql DbContext registration
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<OcrDbContext>));
                if (descriptor != null)
                    services.Remove(descriptor);

                // Replace with InMemory
                services.AddDbContext<OcrDbContext>(options =>
                    options.UseInMemoryDatabase("AuthTests_" + Guid.NewGuid()));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Login_WithValidCredentials_Returns200AndToken()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "testpass"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("token"));
        Assert.NotEmpty(body["token"]);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = "testuser",
            password = "wrongpass"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithInvalidUsername_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = "wronguser",
            password = "testpass"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_Returns200()
    {
        var response = await _client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
