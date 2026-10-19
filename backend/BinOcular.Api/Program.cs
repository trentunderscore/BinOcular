
using BinOcular.Api.Analysis;
using BinOcular.Api.Data;
using BinOcular.Api.Services;
using BinOcular.Api.Tools;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

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

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=binocular.db"));

builder.Services.AddScoped<AnalysisFileStorage>();
builder.Services.AddTransient<ToolRunner>();
builder.Services.AddTransient<IAnalysisTool, DIETool>();

var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
