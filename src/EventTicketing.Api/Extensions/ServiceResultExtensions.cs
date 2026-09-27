using EventTicketing.Core.Results;
using Microsoft.AspNetCore.Mvc;

namespace EventTicketing.Api.Extensions;

public static class ServiceResultExtensions
{
    // Converts a service layer ServiceResult into the matching HTTP response
    public static IActionResult ToActionResult<T>(this ServiceResult<T> result, ControllerBase controller)
    {
        if (result.IsSuccess)
            return controller.Ok(result.Value);

        return result.ErrorType switch
        {
            ServiceErrorType.NotFound => controller.NotFound(new ProblemDetails
            {
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = result.ErrorMessage
            }),
            ServiceErrorType.Validation => controller.BadRequest(new ProblemDetails
            {
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = result.ErrorMessage
            }),
            ServiceErrorType.Conflict => controller.Conflict(new ProblemDetails
            {
                Title = "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = result.ErrorMessage
            }),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Unexpected Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = "An unexpected error occurred."
            })
        };
    }
}
