using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using RaceHub.Application.Common;
using RaceHub.Application.Common.Exceptions;

namespace RaceHub.API.Middleware;

/// <summary>
/// Single place where every unhandled exception in the pipeline is turned
/// into a consistent ApiResponse envelope. Sits first in the middleware
/// pipeline (see Program.cs) so it can catch anything thrown downstream,
/// including FluentValidation failures raised by the MediatR
/// ValidationBehavior.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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
        var (statusCode, response) = MapException(context, exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, SerializerOptions));
    }

    private (int StatusCode, ApiResponse Response) MapException(HttpContext context, Exception exception)
    {
        switch (exception)
        {
            case ValidationException validationException:
            {
                _logger.LogWarning(
                    validationException,
                    "Validation failed for {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                var errors = validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

                return (
                    (int)HttpStatusCode.BadRequest,
                    ApiResponse.FailureResponse("Validation failed.", "validation_error", errors));
            }

            case NotFoundException notFoundException:
            {
                _logger.LogWarning(
                    "Not found for {Method} {Path}: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    notFoundException.Message);

                return (
                    (int)HttpStatusCode.NotFound,
                    ApiResponse.FailureResponse(notFoundException.Message, notFoundException.ErrorCode));
            }

            case ForbiddenAccessException forbiddenException:
            {
                _logger.LogWarning(
                    "Forbidden for {Method} {Path}: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    forbiddenException.Message);

                return (
                    (int)HttpStatusCode.Forbidden,
                    ApiResponse.FailureResponse(forbiddenException.Message, forbiddenException.ErrorCode));
            }

            case ConflictException conflictException:
            {
                _logger.LogWarning(
                    "Conflict for {Method} {Path}: {Message}",
                    context.Request.Method,
                    context.Request.Path,
                    conflictException.Message);

                return (
                    (int)HttpStatusCode.Conflict,
                    ApiResponse.FailureResponse(conflictException.Message, conflictException.ErrorCode));
            }

            case UnauthorizedAccessException:
            {
                _logger.LogWarning(
                    "Unauthorized access for {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                return (
                    (int)HttpStatusCode.Unauthorized,
                    ApiResponse.FailureResponse("You are not authorized to perform this action.", "unauthorized"));
            }

            default:
            {
                _logger.LogError(
                    exception,
                    "Unhandled exception for {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                // Never leak internal exception details to the client outside
                // Development — the log already has the full exception.
                var message = _environment.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred. Please try again later.";

                return (
                    (int)HttpStatusCode.InternalServerError,
                    ApiResponse.FailureResponse(message, "server_error"));
            }
        }
    }
}
