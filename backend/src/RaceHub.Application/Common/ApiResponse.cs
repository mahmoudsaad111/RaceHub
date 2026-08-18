namespace RaceHub.Application.Common;

/// <summary>
/// Uniform envelope for every API response — success or failure — so the
/// frontend (and any other client) can rely on one consistent shape instead
/// of parsing ad-hoc anonymous objects per-endpoint.
///
/// Success:
///   { "success": true, "message": "...", "data": { ... }, "errorCode": null, "errors": null }
/// Failure:
///   { "success": false, "message": "...", "data": null, "errorCode": "invalid_credentials", "errors": null }
/// Validation failure:
///   { "success": false, "message": "Validation failed.", "data": null, "errorCode": "validation_error",
///     "errors": { "Email": ["Email is required."] } }
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public T? Data { get; init; }

    public string? ErrorCode { get; init; }

    /// <summary>Field-level validation errors, keyed by property name.</summary>
    public IDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Request completed successfully.") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data
        };

    public static ApiResponse<T> FailureResponse(
        string message,
        string? errorCode = null,
        IDictionary<string, string[]>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors
        };
}

/// <summary>
/// Non-generic counterpart for endpoints that return no payload
/// (e.g. logout, delete) but should still respond with the same envelope.
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; }

    public string? Message { get; init; }

    public string? ErrorCode { get; init; }

    public IDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse SuccessResponse(string message = "Request completed successfully.") =>
        new()
        {
            Success = true,
            Message = message
        };

    public static ApiResponse FailureResponse(
        string message,
        string? errorCode = null,
        IDictionary<string, string[]>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors
        };
}
