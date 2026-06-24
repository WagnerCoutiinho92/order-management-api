using System.Text.Json;
using OrderManagement.Domain.Exceptions;

namespace OrderManagement.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var (statusCode, code, message) = exception switch
        {
            NotFoundException nfe    => (StatusCodes.Status404NotFound, "NOT_FOUND", nfe.Message),
            BusinessRuleException bre => (StatusCodes.Status422UnprocessableEntity, bre.Code, bre.Message),
            DomainException de        => (StatusCodes.Status400BadRequest, "DOMAIN_ERROR", de.Message),
            _                         => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Ocorreu um erro inesperado.")
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            code,
            message,
            traceId = context.TraceIdentifier
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        await context.Response.WriteAsync(body);
    }
}
