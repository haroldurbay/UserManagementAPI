using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace UserManagementAPI.Middleware
{
    /// <summary>
    /// Logs incoming requests and outgoing responses with status and duration for auditing.
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var sw = Stopwatch.StartNew();
            var method = context.Request.Method;
            var path = context.Request.Path;
            var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
            var traceId = context.TraceIdentifier;

            _logger.LogInformation("Request start {Method} {Path}{Query} trace={TraceId}", method, path, query, traceId);

            await _next(context);

            sw.Stop();
            var status = context.Response.StatusCode;
            _logger.LogInformation("Request end {Method} {Path}{Query} trace={TraceId} status={Status} duration_ms={DurationMs}",
                method, path, query, traceId, status, sw.ElapsedMilliseconds);
        }
    }
}
