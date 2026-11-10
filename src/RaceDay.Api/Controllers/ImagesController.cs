using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Image uploads (event banners, profile pictures) stored in Azure Blob Storage, and serving them back.</summary>
public class ImagesController : ApiControllerBase
{
    public const string BannerContainer = "event-banners";
    public const string ProfileContainer = "profile-pictures";

    private const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private static readonly HashSet<string> Containers = new() { BannerContainer, ProfileContainer };

    private readonly RaceDayDbContext _db;
    private readonly IBlobImageStorage _storage;

    public ImagesController(RaceDayDbContext db, IBlobImageStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>Upload or replace an event's banner image. Only the Organiser who created the event may do this.</summary>
    /// <remarks>Send the file as multipart/form-data in a field named <c>image</c>. JPG, PNG or WebP, up to 5 MB.</remarks>
    /// <param name="id">The event ID.</param>
    /// <param name="image">The banner image file.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Stored. Returns the URL to display the banner with.</response>
    /// <response code="400">No file, the file is too large, or it isn't a JPG, PNG or WebP image.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant, or as a different Organiser.</response>
    /// <response code="404">No event with that ID.</response>
    /// <response code="503">Azure Blob Storage is not configured or can't be reached.</response>
    [HttpPost("api/events/{id:int}/banner")]
    [SessionAuthorize(UserRole.Organiser)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadBanner(int id, IFormFile? image, CancellationToken cancellationToken)
    {
        var ev = await _db.Events.FindAsync([id], cancellationToken);
        if (ev is null) return NotFoundProblem($"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return ForbiddenProblem("You can only upload banners for events you created.");

        if (ValidateImage(image) is string error) return BadRequestProblem(error);

        try
        {
            ev.BannerImageUrl = await StoreAsync(BannerContainer, image!, cancellationToken);
        }
        catch (ImageStorageUnavailableException ex)
        {
            return StorageUnavailable(ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ImageUploadResponse { ImageUrl = ev.BannerImageUrl });
    }

    /// <summary>Upload or replace the logged-in user's profile picture.</summary>
    /// <remarks>Send the file as multipart/form-data in a field named <c>image</c>. JPG, PNG or WebP, up to 5 MB.</remarks>
    /// <param name="image">The profile picture file.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Stored. Returns the URL to display the picture with.</response>
    /// <response code="400">No file, the file is too large, or it isn't a JPG, PNG or WebP image.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="503">Azure Blob Storage is not configured or can't be reached.</response>
    [HttpPost("api/users/me/profile-picture")]
    [SessionAuthorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadProfilePicture(IFormFile? image, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync([CurrentUserId], cancellationToken);
        if (user is null) return NotFoundProblem("Your account no longer exists.");

        if (ValidateImage(image) is string error) return BadRequestProblem(error);

        try
        {
            user.ProfileImageUrl = await StoreAsync(ProfileContainer, image!, cancellationToken);
        }
        catch (ImageStorageUnavailableException ex)
        {
            return StorageUnavailable(ex.Message);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new ImageUploadResponse { ImageUrl = user.ProfileImageUrl });
    }

    /// <summary>Download a stored image (event banner or profile picture).</summary>
    /// <remarks>
    /// Images live in private Azure Blob containers. Clients never talk to Azure directly. They use the
    /// <c>bannerImageUrl</c> / <c>profileImageUrl</c> values returned by other endpoints, which point here.
    /// </remarks>
    /// <response code="200">The image bytes with their content type.</response>
    /// <response code="404">No such image.</response>
    /// <response code="503">Azure Blob Storage is not configured or can't be reached.</response>
    [HttpGet("api/images/{container}/{fileName}")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK, "image/png", "image/jpeg", "image/webp")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImage(string container, string fileName, CancellationToken cancellationToken)
    {
        if (!Containers.Contains(container) || fileName.Contains("..") || fileName.Contains('/'))
        {
            return NotFoundProblem("Image not found.");
        }

        try
        {
            var image = await _storage.OpenReadAsync(container, fileName, cancellationToken);
            if (image is null) return NotFoundProblem("Image not found.");

            // Blob names are random GUIDs and never overwritten, so the bytes can be cached for a long time.
            Response.Headers.CacheControl = "public, max-age=86400";
            return File(image.Content, image.ContentType);
        }
        catch (ImageStorageUnavailableException ex)
        {
            return StorageUnavailable(ex.Message);
        }
    }

    private static string? ValidateImage(IFormFile? image)
    {
        if (image is null || image.Length == 0) return "Choose an image file to upload.";
        if (image.Length > MaxImageBytes) return "Images must be 5 MB or smaller.";
        if (!ImageTypes.TryGetValue(Path.GetExtension(image.FileName), out var expectedType)) return "Use a JPG, PNG or WebP image.";
        if (!string.Equals(image.ContentType, expectedType, StringComparison.OrdinalIgnoreCase)
            && !(expectedType == "image/jpeg" && image.ContentType.Equals("image/pjpeg", StringComparison.OrdinalIgnoreCase)))
        {
            return "The file's type doesn't match its extension.";
        }

        return null;
    }

    /// <summary>Stores the file under a random name and returns the API URL it can be fetched from.</summary>
    private async Task<string> StoreAsync(string container, IFormFile image, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var blobName = $"{Guid.NewGuid():N}{extension}";
        await using var stream = image.OpenReadStream();
        await _storage.UploadAsync(container, blobName, ImageTypes[extension], stream, cancellationToken);
        return $"/api/images/{container}/{blobName}";
    }

    private ObjectResult StorageUnavailable(string detail) =>
        new(new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Title = "Image storage unavailable", Detail = detail })
        {
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
}

/// <summary>Where an uploaded image can be displayed from.</summary>
public class ImageUploadResponse
{
    /// <example>/api/images/event-banners/3f2a9c0e8b1d4c7a9e6f5b4a3c2d1e0f.jpg</example>
    public string? ImageUrl { get; set; }
}
