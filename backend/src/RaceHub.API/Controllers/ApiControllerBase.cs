using Microsoft.AspNetCore.Mvc;
using RaceHub.Application.Common;

namespace RaceHub.API.Controllers;

/// <summary>
/// Base for all API controllers. Centralizes turning an Application-layer
/// Result / Result&lt;T&gt; into the standard ApiResponse envelope with the
/// right HTTP status code, so individual actions don't hand-roll
/// Ok(...)/BadRequest(...) with slightly different shapes each time.
///
/// Status code convention (chosen by ErrorCode, since Result doesn't carry
/// one itself):
///   - "*_not_found" / "not_found"      -> 404
///   - "invalid_credentials" / "unauthorized" -> 401
///   - "forbidden"                      -> 403
///   - "*_taken" / "conflict"           -> 409
///   - anything else                    -> 400
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult<T>(Result<T> result, string successMessage = "Request completed successfully.")
    {
        if (result.Succeeded)
        {
            return Ok(ApiResponse<T>.SuccessResponse(result.Value!, successMessage));
        }

        return FailureResult(result.Error!, result.ErrorCode);
    }

    protected IActionResult HandleResult(Result result, string successMessage = "Request completed successfully.")
    {
        if (result.Succeeded)
        {
            return Ok(ApiResponse.SuccessResponse(successMessage));
        }

        return FailureResult(result.Error!, result.ErrorCode);
    }

    private IActionResult FailureResult(string error, string? errorCode)
    {
        var response = ApiResponse.FailureResponse(error, errorCode);

        return errorCode switch
        {
            "invalid_credentials" or "unauthorized" => Unauthorized(response),
            "forbidden" => StatusCode(StatusCodes.Status403Forbidden, response),
            "not_found" => NotFound(response),
            var code when code is not null && code.EndsWith("_not_found") => NotFound(response),
            "conflict" => Conflict(response),
            var code when code is not null && code.EndsWith("_taken") => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
