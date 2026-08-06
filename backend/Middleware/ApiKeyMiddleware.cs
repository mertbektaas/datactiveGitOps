namespace backend.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private const string API_KEY_HEADER = "X-API-KEY";
    private const string DEFAULT_API_KEY = "datactive-gitops-secret-key-2026";

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();

        // Bypass security check for health endpoint
        if (path != null && (path == "/health" || path.StartsWith("/health/")))
        {
            await _next(context);
            return;
        }

        var requireApiKey = _configuration.GetValue<bool>("RequireApiKey", false);
        if (!requireApiKey)
        {
            await _next(context);
            return;
        }

        var configuredApiKey = _configuration.GetValue<string>("ApiKey") 
            ?? Environment.GetEnvironmentVariable("API_KEY") 
            ?? DEFAULT_API_KEY;

        if (!context.Request.Headers.TryGetValue(API_KEY_HEADER, out var extractedApiKey))
        {
            _logger.LogWarning("Unauthorized API request to '{Path}' without X-API-KEY header.", path);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Yetkisiz Erişim: X-API-KEY başlığı eksik.\"}");
            return;
        }

        if (!string.Equals(configuredApiKey, extractedApiKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid API Key supplied for request to '{Path}'.", path);
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Yetkisiz Erişim: Geçersiz X-API-KEY.\"}");
            return;
        }

        await _next(context);
    }
}
