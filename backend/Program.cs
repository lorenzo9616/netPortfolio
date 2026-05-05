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
builder.Services.AddHttpClient("anthropic", client =>
{
    client.BaseAddress = new Uri("https://api.anthropic.com");
    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
});
builder.Services.AddScoped<IDocumentClassifierService, DocumentClassifierService>();

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
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OcrApi.Data.OcrDbContext>("postgres");

var app = builder.Build();

// ── Database schema — apply pending migrations on every startup ─────────────
// docker-compose depends_on: condition: service_healthy guarantees Postgres is
// accepting connections before this process starts.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OcrDbContext>();
    db.Database.Migrate();
}

// ── Middleware pipeline ─────────────────────────────────────────────────────
// Swagger enabled in all environments so demo / Docker deployments can explore the API.
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OCR API v1"));

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
