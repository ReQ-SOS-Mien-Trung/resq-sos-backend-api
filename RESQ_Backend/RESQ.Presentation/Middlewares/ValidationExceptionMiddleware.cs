using System.Net;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Threading.Tasks;
using RESQ.Application.Exceptions;

namespace RESQ.Presentation.Middlewares
{
    public class ValidationExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ValidationExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (ValidationException ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";
                var result = JsonSerializer.Serialize(new
                {
                    message = ex.Message,
                    errors = ex.Errors
                });
                await context.Response.WriteAsync(result);
            }
        }
    }
}
