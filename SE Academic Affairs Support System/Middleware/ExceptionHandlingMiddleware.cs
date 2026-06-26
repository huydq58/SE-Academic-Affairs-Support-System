using System.Text.Json;

namespace SE_Academic_Affairs_Support_System.Middleware
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;

        public ExceptionHandlingMiddleware(RequestDelegate next,
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                    context.Request.Method, context.Request.Path);

                if (context.Response.HasStarted)
                {
                    _logger.LogWarning("Cannot write error response: response has already started for {Path}.",
                        context.Request.Path);
                    throw;
                }

                // HTMX-boosted navigation sends HX-Request: true — treat as regular HTML, not JSON API
                bool isHtmx = context.Request.Headers["HX-Request"] == "true";

                // True AJAX JSON clients: XMLHttpRequest header OR Accept: application/json without text/html
                bool isAjaxJson = !isHtmx && IsAjaxJsonRequest(context);

                if (isAjaxJson)
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/json; charset=utf-8";

                    var message = _env.IsDevelopment()
                        ? $"Lỗi server: [{ex.GetType().Name}] {ex.Message}"
                        : "Có lỗi xảy ra, vui lòng thử lại sau.";

                    await context.Response.WriteAsync(
                        JsonSerializer.Serialize(new { success = false, message }));
                    return;
                }

                if (_env.IsDevelopment())
                {
                    // Re-throw so DeveloperExceptionPage (outer in pipeline) can display full details
                    throw;
                }

                // Production: redirect to friendly Vietnamese error page
                context.Response.Clear();
                context.Response.Redirect("/Home/Error");
            }
        }

        private static bool IsAjaxJsonRequest(HttpContext context)
        {
            if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return true;

            var accept = context.Request.Headers.Accept.ToString();
            // application/json without text/html: true JSON-only client, not a browser
            return accept.Contains("application/json") && !accept.Contains("text/html");
        }
    }
}
