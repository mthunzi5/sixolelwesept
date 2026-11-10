namespace RaceDay.Mvc.Services;

/// <summary>Outcome of an API call: success, or a user-friendly error message plus the HTTP status.</summary>
public class ApiResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    /// <summary>The API no longer recognises the session (expired or logged out).</summary>
    public bool IsUnauthorized => StatusCode == StatusCodes.Status401Unauthorized;

    public static ApiResult Ok(int statusCode) => new() { Success = true, StatusCode = statusCode };
    public static ApiResult Fail(string error, int statusCode) => new() { Success = false, Error = error, StatusCode = statusCode };
}

/// <summary>An API call that returns data.</summary>
public class ApiResult<T> : ApiResult
{
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T? data, int statusCode) => new() { Success = true, Data = data, StatusCode = statusCode };
    public static new ApiResult<T> Fail(string error, int statusCode) => new() { Success = false, Error = error, StatusCode = statusCode };
}
