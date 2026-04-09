using Microsoft.AspNetCore.Mvc;

namespace OcrApi.Middleware;

public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly RequestDelegate _next;
    private readonly IConfiguration _config;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config, ILogger<ApiKeyMiddleware> logger)
    {
        _next   = next;
        _config = config;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip health check endpoint — allows load balancers to probe without auth
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        var apiKey = _config["Auth:ApiKey"];

        // If no API key configured, skip auth (development mode)
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("API key authentication is disabled — no Auth:ApiKey configured");
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedKey)
            || extractedKey != apiKey)
        {
            _logger.LogWarning("Unauthorized API access attempt from {IP}",
                context.Connection.RemoteIpAddress);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 401,
                Title  = "Unauthorized",
                Detail = "A valid API key is required."
            });
            return;
        }

        await _next(context);
    }
}
