using Microsoft.AspNetCore.Http.HttpResults;
using RESQ.Application.Exceptions;
using RESQ.Domain.Entities.Exceptions;
using System.Text.Json;

namespace RESQ.Presentation.Middlewares;

public class GlobalExceptionMiddleware : IMiddleware
{
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(ILogger<GlobalExceptionMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = new ErrorResponse();

        switch (exception)
        {
            // ✅ Validation (FluentValidation / Application)
            case ValidationException validationEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = "Lỗi xác thực dữ liệu";
                response.Errors = validationEx.Errors;
                _logger.LogWarning("Validation failed");
                break;

            // ✅ Domain rule violation
            case DomainException domainEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = domainEx.Message;
                _logger.LogWarning("Domain violation: {Message}", domainEx.Message);
                break;


            //// ✅ Application BadRequest (mapped from DomainBehaviour)
            //case BadRequestException badRequestEx:
            //    context.Response.StatusCode = StatusCodes.Status400BadRequest;
            //    response.Message = badRequestEx.Message;
            //    _logger.LogWarning("Bad request: {Message}", badRequestEx.Message);
            //    break;

            // ✅ Bad Request
            case BadRequestException badRequestEx:
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                response.Message = badRequestEx.Message;
                _logger.LogWarning("Bad request: {Message}", badRequestEx.Message);
                break;

            // ✅ Unauthorized (Application layer)
            case UnauthorizedException unauthorizedEx:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                response.Message = unauthorizedEx.Message;
                _logger.LogWarning("Unauthorized: {Message}", unauthorizedEx.Message);
                break;

            // ✅ Unauthorized (System — safety net cho UnauthorizedAccessException)
            case UnauthorizedAccessException unauthorizedAccessEx:
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                response.Message = unauthorizedAccessEx.Message;
                _logger.LogWarning("UnauthorizedAccess: {Message}", unauthorizedAccessEx.Message);
                break;

            // ✅ Forbidden
            case ForbiddenException forbiddenEx:
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                response.Message = forbiddenEx.Message;
                _logger.LogWarning("Forbidden: {Message}", forbiddenEx.Message);
                break;

            // ✅ Not Found
            case NotFoundException notFoundEx:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                response.Message = notFoundEx.Message;
                _logger.LogWarning("Not found: {Message}", notFoundEx.Message);
                break;

            // ✅ Conflict
            case ConflictException conflictEx:
                context.Response.StatusCode = StatusCodes.Status409Conflict;
                response.Message = conflictEx.Message;
                response.Code = "CONCURRENCY_CONFLICT";
                _logger.LogWarning("Conflict: {Message}", conflictEx.Message);
                break;

            // ✅ Too Many Requests
            case TooManyRequestsException tooManyEx:
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                response.Message = tooManyEx.Message;
                _logger.LogWarning("Too many requests: {Message}", tooManyEx.Message);
                break;

            // ❌ System error
            default:
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                response.Message = "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau.";
                _logger.LogError(exception, "Unhandled exception");
                break;
        }

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        await context.Response.WriteAsync(json);
    }
}

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? InnerError { get; set; }
    public IDictionary<string, string[]>? Errors { get; set; }
}
