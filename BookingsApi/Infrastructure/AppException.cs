namespace BookingsApi.Infrastructure;

/// <summary>Domain failure carrying the HTTP status, title and detail to return as ProblemDetails.</summary>
public sealed class AppException(
    int statusCode,
    string title,
    string? detail = null,
    IDictionary<string, string[]>? errors = null) : Exception(detail ?? title)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
    public string? Detail { get; } = detail;
    public IDictionary<string, string[]>? Errors { get; } = errors;

    public static AppException Validation(string field, string message) =>
        new(StatusCodes.Status400BadRequest, "Validation failed", message,
            new Dictionary<string, string[]> { [field] = [message] });
}
