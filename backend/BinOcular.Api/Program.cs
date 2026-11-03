using BinOcular.Api.Data;
using BinOcular.Api.Extensions;
using BinOcular.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure 100 MB max request size for demo uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104_857_600; // 100 MB
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 104_857_600; // 100 MB
});

builder.Services.AddOpenApi();
builder.Services.AddControllers();

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            "logs/backend-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7);
});

// Database - configurable connection string with SQLite fallback
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=binocular.db";
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// Storage & Worker Queue
builder.Services.AddSingleton<AnalysisQueue>();
builder.Services.AddHostedService<AnalysisWorker>();
builder.Services.AddScoped<FileHashService>();
builder.Services.AddScoped<AnalysisFileStorage>();

// Analysis Pipeline Tools & Formatters
builder.Services.AddAnalysisPipeline();

// CORS for Frontend (Vite & production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseSerilogRequestLogging();

// Automatic DB Migration on startup for zero-friction setup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database migration warning; ensuring schema is created");
        db.Database.EnsureCreated();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Serve static frontend assets if published to wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowFrontend");

// Health check endpoint for container monitors & free cloud platforms
app.MapGet("/healthz", () => Results.Ok(new
{
    status = "healthy",
    service = "BinOcular.Api",
    version = "1.0.0",
    activeTools = 15,
    timestamp = DateTime.UtcNow
}));

app.MapControllers();

// SPA routing fallback to index.html when serving compiled frontend
app.MapFallbackToFile("index.html");

app.Run();
