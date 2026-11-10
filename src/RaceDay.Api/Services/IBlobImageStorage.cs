namespace RaceDay.Api.Services;

/// <summary>Stores and serves uploaded images (event banners, profile pictures).</summary>
public interface IBlobImageStorage
{
    /// <summary>True when a storage connection string has been configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Uploads an image and returns the blob name it was stored under.</summary>
    Task<string> UploadAsync(string containerName, string blobName, string contentType, Stream content, CancellationToken cancellationToken);

    /// <summary>Opens a stored image for reading, or returns null if it does not exist.</summary>
    Task<StoredImage?> OpenReadAsync(string containerName, string blobName, CancellationToken cancellationToken);
}

/// <summary>An image read back out of storage.</summary>
public sealed record StoredImage(Stream Content, string ContentType);

/// <summary>Thrown when the storage account can't be reached or isn't configured.</summary>
public sealed class ImageStorageUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
