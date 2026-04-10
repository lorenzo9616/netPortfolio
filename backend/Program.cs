using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OcrApi.Data;
using OcrApi.Middleware;
using OcrApi.Repositories;
using OcrApi.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Controllers ────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ── OpenAPI / Swagger ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title       = "OCR API",
        Version     = "v1",
        Description = "REST API for the open-source OCR system"
    });
});

// ── Database (connection string injected via env in all environments) ───────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<OcrDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Repositories ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IOcrPropertyRepository, OcrPropertyRepository>();

// ── OCR client (typed HttpClient — base address read from config) ────────────
builder.Services.AddHttpClient<IOcrClient, OcrClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:OcrServiceUrl"] ?? "http://ocr-service:8000");
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddSingleton<IFieldMatcher, FieldMatcher>();

// ── CORS (environment-aware — origins read from config) ────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:3000"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ── Rate limiting (fixed window — applied only to expensive OCR endpoint) ───
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("ocr-policy", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window      = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit   = 2;
    });
    options.RejectionStatusCode = 429;
});

// ── Health checks ───────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Database: apply pending migrations and seed on startup ─────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<OcrDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("Applying database migrations...");
    db.Database.Migrate();
    logger.LogInformation("Migrations applied. Running seed check...");
    SeedData.Initialize(db);
    logger.LogInformation("Seed check complete.");
}

// ── Middleware pipeline ─────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCR API v1"));
}

// 1. CORS — must come before auth so pre-flight requests are handled
app.UseCors("FrontendPolicy");

// 2. Rate limiting — reject overloaded requests early
app.UseRateLimiter();

// 3. API Key auth — runs after CORS, before routing/controllers
app.UseMiddleware<ApiKeyMiddleware>();

// 4. Authorization
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
