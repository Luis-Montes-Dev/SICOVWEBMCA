namespace SICOVWEB_MCA.Helpers
{
    // Middleware para deshabilitar la caché en las respuestas HTTP
    public class NoCacheMiddleware
    {
        private readonly RequestDelegate _next;
        // Constructor que recibe el RequestDelegate
        public NoCacheMiddleware(RequestDelegate next)
        {
            _next = next;
        }
        // Método Invoke que se ejecuta para cada solicitud HTTP
        public async Task Invoke(HttpContext context)
        {
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            await _next(context);
        }
    }

}
