using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BookingsApi.Infrastructure;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        switch (exception)
        {
            case AppException app when app.Errors is not null:
                problem = new ValidationProblemDetails(new Dictionary<string, string[]>(app.Errors))
                {
                    Status = app.StatusCode,
                    Title = app.Title,
                    Detail = app.Detail
                };
                break;
            case AppException app:
                problem = new ProblemDetails { Status = app.StatusCode, Title = app.Title, Detail = app.Detail };
                break;
            case DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } }:
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Conflict",
                    Detail = "The request conflicts with existing data."
                };
                break;
            default:
                logger.LogError(exception, "Unhandled exception");
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Internal server error",
                    Detail = "An unexpected error occurred."
                };
                break;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
