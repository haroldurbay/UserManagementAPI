using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UserManagementAPI.Middleware
{
    /// <summary>
    /// Middleware that enforces a bearer token for incoming requests and returns 401 for invalid tokens.
    /// </summary>
    public class ApiTokenAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiTokenAuthMiddleware> _logger;
        private readonly string? _validToken;

        public ApiTokenAuthMiddleware(RequestDelegate next, ILogger<ApiTokenAuthMiddleware> logger, IConfiguration config)
        {
            _next = next;
            _logger = logger;
            _validToken = config["Auth:ApiToken"];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Skip auth for swagger and health endpoints to keep tooling reachable.
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.StartsWith("/swagger") || path.Equals("/health", System.StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            if (string.IsNullOrWhiteSpace(_validToken))
            {
                _logger.LogWarning("Auth token not configured; denying request to {Path}", path);
                await WriteUnauthorized(context);
                return;
            }

            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
            {
                await WriteUnauthorized(context);
                return;
            }

            var header = authHeader.FirstOrDefault();
            const string bearerPrefix = "Bearer ";
            if (header is null || !header.StartsWith(bearerPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                await WriteUnauthorized(context);
                return;
            }

            var token = header[bearerPrefix.Length..].Trim();
            if (!string.Equals(token, _validToken, System.StringComparison.Ordinal))
            {
                await WriteUnauthorized(context);
                return;
            }

            await _next(context);
        }

        private static async Task WriteUnauthorized(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            context.Response.Headers["WWW-Authenticate"] = "Bearer";
            var payload = new { error = "Unauthorized." };
            await context.Response.WriteAsJsonAsync(payload);
        }
    }
}
