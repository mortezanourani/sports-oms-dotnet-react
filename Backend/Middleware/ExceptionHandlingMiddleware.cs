using System.Net;
using System.Text.Json;

namespace Backend.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionHandlingMiddleware(
            RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // Let the request proceed through the rest of the pipeline
                // (routing, authentication, the controller action itself)
                // normally. This middleware only does something when
                // something downstream throws.
                await _next(context);
            }
            catch (Exception ex)
            {
                // Always log the FULL exception details server-side,
                // regardless of environment — this is what you'll actually
                // look at to diagnose a production bug. What differs between
                // environments is only what gets SENT BACK to the caller.
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

                // In development, it's genuinely useful to see the real
                // exception message and type while you're actively building
                // and debugging. In production, exposing that same detail to
                // an API caller is a real information-disclosure risk — it
                // can reveal internal class names, file paths, or database
                // schema details to anyone who triggers an error, including
                // an attacker deliberately probing for weaknesses.
                object responseBody = _env.IsDevelopment()
                    ? new { detail = "خطای غیرمنتظره‌ای رخ داد", exception = ex.Message, stackTrace = ex.StackTrace }
                    : new { detail = "خطای غیرمنتظره‌ای رخ داد. لطفاً بعداً دوباره تلاش کنید." };

                var json = JsonSerializer.Serialize(responseBody);
                await context.Response.WriteAsync(json);
            }
        }
    }
}
