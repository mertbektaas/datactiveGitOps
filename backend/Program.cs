using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using backend.Data;

var builder = WebApplication.CreateBuilder(args);

// Port 8080 configuration as specified in Issue K1.1 & CONTRACT.md
builder.WebHost.UseUrls("http://0.0.0.0:8080");

// Configure PostgreSQL DbContext with Npgsql
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Host=localhost;Port=5432;Database=datactive_gitops;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Solution 2: ASP.NET Core Built-in HealthChecks Service
builder.Services.AddHealthChecks();

var app = builder.Build();

// Map /health endpoint with custom JSON output format
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            service = "datactive-backend"
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
});

app.Run();
