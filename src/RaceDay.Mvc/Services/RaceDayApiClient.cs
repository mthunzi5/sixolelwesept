using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Mvc.Auth;
using RaceDay.Mvc.Models;

namespace RaceDay.Mvc.Services;

/// <summary>
/// The only way the MVC app reads or changes data: a typed wrapper over the RaceDay API (Part 2).
/// <para>
/// Authentication: when a user logs in, the API answers with its own session cookie
/// (<c>.RaceDay.Session</c>). That cookie is kept in the user's MVC session and sent back on every
/// API call made for that user, so the API knows who is calling and applies its role rules.
/// </para>
/// API errors (ProblemDetails / validation errors) are turned into readable messages rather than exceptions.
/// </summary>
public class RaceDayApiClient
{
    private const string ApiSessionCookieName = ".RaceDay.Session";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly ILogger<RaceDayApiClient> _logger;

    public RaceDayApiClient(HttpClient http, IHttpContextAccessor contextAccessor, ILogger<RaceDayApiClient> logger)
    {
        _http = http;
        _contextAccessor = contextAccessor;
        _logger = logger;
    }

    private ISession? Session => _contextAccessor.HttpContext?.Session;

    // ---------- Auth ----------

    public Task<ApiResult<UserDto>> RegisterAsync(RegisterForm form) =>
        SendAsync<UserDto>(HttpMethod.Post, "api/auth/register", new
        {
            fullName = form.FullName,
            email = form.Email,
            password = form.Password,
            role = form.Role,
            phoneNumber = form.PhoneNumber
        });

    /// <summary>Logs in and, on success, stores the API's session cookie in the MVC session.</summary>
    public async Task<ApiResult<LoginResponse>> LoginAsync(string email, string password)
    {
        var result = await SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new { email, password }, captureSessionCookie: true);
        return result;
    }

    public Task<ApiResult> LogoutAsync() => SendAsync(HttpMethod.Post, "api/auth/logout");

    // ---------- Profile ----------

    public Task<ApiResult<UserDto>> GetMeAsync() => SendAsync<UserDto>(HttpMethod.Get, "api/users/me");

    public Task<ApiResult<UserDto>> UpdateMeAsync(ProfileForm form) =>
        SendAsync<UserDto>(HttpMethod.Put, "api/users/me", new { fullName = form.FullName, phoneNumber = form.PhoneNumber });

    public Task<ApiResult> ChangePasswordAsync(ChangePasswordForm form) =>
        SendAsync(HttpMethod.Put, "api/users/me/password", new { currentPassword = form.CurrentPassword, newPassword = form.NewPassword });

    public Task<ApiResult<ImageUploadResponse>> UploadProfilePictureAsync(IFormFile file) =>
        UploadImageAsync("api/users/me/profile-picture", file);

    // ---------- Events ----------

    public Task<ApiResult<List<EventDto>>> GetEventsAsync(EventStatus? status = null, EventType? eventType = null, DateOnly? from = null, DateOnly? to = null)
    {
        var query = new List<string>();
        if (status is not null) query.Add($"status={status}");
        if (eventType is not null) query.Add($"eventType={eventType}");
        if (from is not null) query.Add($"from={from:yyyy-MM-dd}");
        if (to is not null) query.Add($"to={to:yyyy-MM-dd}");
        var path = query.Count == 0 ? "api/events" : $"api/events?{string.Join('&', query)}";
        return SendAsync<List<EventDto>>(HttpMethod.Get, path);
    }

    public Task<ApiResult<EventDto>> GetEventAsync(int id) => SendAsync<EventDto>(HttpMethod.Get, $"api/events/{id}");

    public Task<ApiResult<List<EventDto>>> GetMyEventsAsync() => SendAsync<List<EventDto>>(HttpMethod.Get, "api/organisers/me/events");

    public Task<ApiResult<EventDto>> CreateEventAsync(EventForm form) => SendAsync<EventDto>(HttpMethod.Post, "api/events", EventPayload(form));

    public Task<ApiResult<EventDto>> UpdateEventAsync(int id, EventForm form) => SendAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", EventPayload(form));

    /// <summary>Changes only an event's status, re-sending its other details unchanged (the API's PUT takes the full event).</summary>
    public Task<ApiResult<EventDto>> SetEventStatusAsync(EventDto ev, EventStatus status) =>
        SendAsync<EventDto>(HttpMethod.Put, $"api/events/{ev.EventId}", new
        {
            eventName = ev.EventName,
            description = ev.Description,
            eventDate = ev.EventDate.ToString("yyyy-MM-dd"),
            location = ev.Location,
            distanceKm = ev.DistanceKm,
            eventType = ev.EventType,
            status
        });

    public Task<ApiResult> DeleteEventAsync(int id) => SendAsync(HttpMethod.Delete, $"api/events/{id}");

    public Task<ApiResult<ImageUploadResponse>> UploadBannerAsync(int eventId, IFormFile file) =>
        UploadImageAsync($"api/events/{eventId}/banner", file);

    public Task<ApiResult<List<EnrolmentDto>>> GetEventEnrolmentsAsync(int eventId) =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, $"api/events/{eventId}/enrolments");

    // ---------- Categories ----------

    public Task<ApiResult<CategoryDto>> CreateCategoryAsync(int eventId, CategoryForm form) =>
        SendAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", new
        {
            categoryName = form.CategoryName,
            categoryType = form.CategoryType,
            distanceKm = form.DistanceKm,
            minAge = form.MinAge,
            maxAge = form.MaxAge,
            entryFee = form.EntryFee,
            maxParticipants = form.MaxParticipants
        });

    public Task<ApiResult> DeleteCategoryAsync(int categoryId) => SendAsync(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ---------- Enrolments & payments ----------

    public Task<ApiResult<EnrolmentDto>> EnrolAsync(int categoryId) =>
        SendAsync<EnrolmentDto>(HttpMethod.Post, $"api/categories/{categoryId}/enrolments");

    public Task<ApiResult<List<EnrolmentDto>>> GetMyEnrolmentsAsync() =>
        SendAsync<List<EnrolmentDto>>(HttpMethod.Get, "api/participants/me/enrolments");

    public Task<ApiResult> WithdrawAsync(int enrolmentId) => SendAsync(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");

    public Task<ApiResult<PaymentDto>> PayAsync(PaymentForm form) =>
        SendAsync<PaymentDto>(HttpMethod.Post, $"api/enrolments/{form.EnrolmentId}/payment", new { amount = form.Amount, paymentMethod = form.PaymentMethod });

    // ---------- Results ----------

    public Task<ApiResult<List<ResultDto>>> GetMyResultsAsync() => SendAsync<List<ResultDto>>(HttpMethod.Get, "api/participants/me/results");

    public Task<ApiResult<List<ResultDto>>> GetEventResultsAsync(int eventId) => SendAsync<List<ResultDto>>(HttpMethod.Get, $"api/events/{eventId}/results");

    public Task<ApiResult<ResultDto>> CaptureResultAsync(int enrolmentId, int? finishTimeSeconds, int? position, ResultStatus status) =>
        SendAsync<ResultDto>(HttpMethod.Post, $"api/enrolments/{enrolmentId}/result", new { finishTimeSeconds, position, status });

    public Task<ApiResult<ResultDto>> UpdateResultAsync(int resultId, int? finishTimeSeconds, int? position, ResultStatus status) =>
        SendAsync<ResultDto>(HttpMethod.Put, $"api/results/{resultId}", new { finishTimeSeconds, position, status });

    // ---------- Images ----------

    /// <summary>Fetches an image from the API (which reads it from Azure Blob Storage). Caller disposes the response.</summary>
    public async Task<HttpResponseMessage?> GetImageAsync(string container, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.GetAsync($"api/images/{Uri.EscapeDataString(container)}/{Uri.EscapeDataString(fileName)}",
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode) return response;
            response.Dispose();
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not fetch image {Container}/{File} from the API", container, fileName);
            return null;
        }
    }

    // ---------- Plumbing ----------

    private static object EventPayload(EventForm form) => new
    {
        eventName = form.EventName,
        description = form.Description,
        eventDate = form.EventDate?.ToString("yyyy-MM-dd"),
        location = form.Location,
        distanceKm = form.DistanceKm,
        eventType = form.EventType,
        status = form.Status
    };

    private async Task<ApiResult<ImageUploadResponse>> UploadImageAsync(string path, IFormFile file)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
            ? type
            : new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "image", Path.GetFileName(file.FileName));
        return await SendAsync<ImageUploadResponse>(HttpMethod.Post, path, content);
    }

    private async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var result = await SendAsync<JsonElement>(method, path, body);
        return result.Success ? ApiResult.Ok(result.StatusCode) : ApiResult.Fail(result.Error!, result.StatusCode);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null, bool captureSessionCookie = false)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Content = body switch
        {
            null => null,
            HttpContent content => content,
            _ => JsonContent.Create(body, options: JsonOptions)
        };

        var apiCookie = Session?.GetString(SessionKeys.ApiCookie);
        if (!string.IsNullOrEmpty(apiCookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", apiCookie);
        }

        try
        {
            using var response = await _http.SendAsync(request);

            if (captureSessionCookie && response.IsSuccessStatusCode)
            {
                StoreApiSessionCookie(response);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Fail(await ReadErrorAsync(response), (int)response.StatusCode);
            }

            if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            {
                return ApiResult<T>.Ok(default, (int)response.StatusCode);
            }

            var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
            return ApiResult<T>.Ok(data, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "RaceDay API unreachable for {Method} {Path}", method, path);
            return ApiResult<T>.Fail("We couldn't reach the RaceDay service. Please try again in a moment.", StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "RaceDay API timed out for {Method} {Path}", method, path);
            return ApiResult<T>.Fail("The RaceDay service took too long to respond. Please try again.", StatusCodes.Status504GatewayTimeout);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Unreadable API response for {Method} {Path}", method, path);
            return ApiResult<T>.Fail("Something went wrong reading the response. Please try again.", StatusCodes.Status502BadGateway);
        }
    }

    private void StoreApiSessionCookie(HttpResponseMessage response)
    {
        if (Session is null || !response.Headers.TryGetValues("Set-Cookie", out var cookies)) return;

        var sessionCookie = cookies.FirstOrDefault(c => c.StartsWith(ApiSessionCookieName + "=", StringComparison.Ordinal));
        if (sessionCookie is not null)
        {
            // Keep only "name=value"; attributes like path/httponly don't apply to a server-side client.
            Session.SetString(SessionKeys.ApiCookie, sessionCookie.Split(';', 2)[0]);
        }
    }

    /// <summary>Turns an API error (ProblemDetails or validation errors) into one readable sentence.</summary>
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        var fallback = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Please log in to continue.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            HttpStatusCode.NotFound => "We couldn't find what you were looking for.",
            HttpStatusCode.ServiceUnavailable => "That service is unavailable right now. Please try again later.",
            _ => "Something went wrong. Please check your details and try again."
        };

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(JsonOptions);
            if (problem?.Errors is { Count: > 0 })
            {
                // Validation errors from [ApiController]: keep the human messages, drop JSON parser noise.
                var messages = problem.Errors.Values.SelectMany(m => m)
                    .Where(m => !m.Contains("JSON", StringComparison.OrdinalIgnoreCase)
                                && !m.StartsWith("The request field", StringComparison.Ordinal))
                    .Distinct()
                    .ToList();
                return messages.Count > 0 ? string.Join(" ", messages) : fallback;
            }

            return problem?.Detail ?? fallback;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return fallback;
        }
    }

    private sealed class ProblemBody
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
