namespace RaceHub.Application.Common.Exceptions;

/// <summary>
/// Thrown by handlers when an authenticated user is not allowed to perform
/// the requested operation (as opposed to not being authenticated at all,
/// which [Authorize] already rejects with 401 before the handler runs).
/// Mapped by ExceptionHandlingMiddleware to HTTP 403.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public string ErrorCode { get; }

    public ForbiddenAccessException(string message = "You do not have permission to perform this action.", string errorCode = "forbidden")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
