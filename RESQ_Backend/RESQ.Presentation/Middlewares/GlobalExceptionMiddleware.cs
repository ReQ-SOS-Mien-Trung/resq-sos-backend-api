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
                response.Message = "Lỗi nghiệp vụ";
                response.Errors = new Dictionary<string, string[]>
                {
                    ["_domainMsg"] = new[] { domainEx.Message }
                };
                _logger.LogWarning("Domain violation: {Message}", domainEx.Message);
                break;


            //// ✅ Application BadRequest (mapped from DomainBehaviour)
            //case BadRequestException badRequestEx:
            //    context.Response.StatusCode = StatusCodes.Status400BadRequest;
            //    response.Message = badRequestEx.Message;
            //    _logger.LogWarning("Bad request: {Message}", badRequestEx.Message);
            //    break;

            // ✅ Not Found
            case NotFoundException notFoundEx:
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                response.Message = "Không tìm thấy";
                response.Errors = new Dictionary<string, string[]>
                {
                    ["_notFoundMsg"] = new[] { notFoundEx.Message }
                };
                _logger.LogWarning("Not found: {Message}", notFoundEx.Message);
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
    public IDictionary<string, string[]>? Errors { get; set; }
}
