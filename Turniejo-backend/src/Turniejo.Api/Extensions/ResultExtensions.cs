using Microsoft.AspNetCore.Mvc;
using Turniejo.SharedKernel.Results;

namespace Turniejo.Api.Extensions
{
    public static class ResultExtensions
    {
        public static IActionResult ToActionResult<T>(this Result<T> result) =>
            result.IsSuccess
                ? new OkObjectResult(result.Value)
                : result.Error.Type switch
                {
                    ErrorType.NotFound => new NotFoundObjectResult(result.Error),
                    ErrorType.Validation => new BadRequestObjectResult(result.Error),
                    ErrorType.Conflict => new ConflictObjectResult(result.Error),
                    _ => new BadRequestObjectResult(result.Error)
                };

        public static IActionResult ToActionResult(this Result result) =>
        result.IsSuccess
            ? new NoContentResult()
            : result.Error.Type switch
            {
                ErrorType.NotFound => new NotFoundObjectResult(result.Error),
                ErrorType.Validation => new BadRequestObjectResult(result.Error),
                ErrorType.Conflict => new ConflictObjectResult(result.Error),
                _ => new BadRequestObjectResult(result.Error)
            };
    }
}
