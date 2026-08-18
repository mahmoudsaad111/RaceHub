namespace RaceHub.Application.Common.Exceptions;

/// <summary>
/// Thrown by handlers when a requested entity doesn't exist.
/// Mapped by ExceptionHandlingMiddleware to HTTP 404.
/// </summary>
public class NotFoundException : Exception
{
    public string ErrorCode { get; }

    public NotFoundException(string message, string errorCode = "not_found")
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.")
    {
        ErrorCode = "not_found";
    }
}
