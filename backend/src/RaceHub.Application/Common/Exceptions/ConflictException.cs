namespace RaceHub.Application.Common.Exceptions;

/// <summary>
/// Thrown by handlers when a request conflicts with the current state of
/// the resource (e.g. a duplicate/unique-constraint style conflict that
/// wasn't already caught as an application-level Result.Failure).
/// Mapped by ExceptionHandlingMiddleware to HTTP 409.
/// </summary>
public class ConflictException : Exception
{
    public string ErrorCode { get; }

    public ConflictException(string message, string errorCode = "conflict")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
