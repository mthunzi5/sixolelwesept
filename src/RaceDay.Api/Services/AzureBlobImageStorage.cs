using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>
/// Azure Blob Storage implementation. The connection string comes from
/// <c>ConnectionStrings:AzureBlobStorage</c>: a real Azure Storage account in production, or
/// <c>UseDevelopmentStorage=true</c> / an Azurite connection string for local development and Docker.
/// Containers are private; images are served to clients through the API (GET /api/images/...).
/// </summary>
public sealed class AzureBlobImageStorage : IBlobImageStorage
{
    private readonly BlobServiceClient? _service;
    private readonly ILogger<AzureBlobImageStorage> _logger;

    public AzureBlobImageStorage(IConfiguration configuration, ILogger<AzureBlobImageStorage> logger)
    {
        _logger = logger;
        var connectionString = configuration.GetConnectionString("AzureBlobStorage");
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            _service = new BlobServiceClient(connectionString);
        }
    }

    public bool IsConfigured => _service is not null;

    public async Task<string> UploadAsync(string containerName, string blobName, string contentType, Stream content, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(containerName, cancellationToken);
        try
        {
            await container.GetBlobClient(blobName).UploadAsync(
                content,
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
                cancellationToken);
            return blobName;
        }
        catch (Exception ex) when (ex is RequestFailedException or HttpRequestException or AggregateException)
        {
            _logger.LogError(ex, "Uploading {Blob} to {Container} failed", blobName, containerName);
            throw new ImageStorageUnavailableException("The image could not be saved to Azure Blob Storage.", ex);
        }
    }

    public async Task<StoredImage?> OpenReadAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync(containerName, cancellationToken);
        try
        {
            var blob = container.GetBlobClient(blobName);
            var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new StoredImage(download.Value.Content, download.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
        catch (Exception ex) when (ex is RequestFailedException or HttpRequestException or AggregateException)
        {
            _logger.LogError(ex, "Reading {Blob} from {Container} failed", blobName, containerName);
            throw new ImageStorageUnavailableException("The image could not be read from Azure Blob Storage.", ex);
        }
    }

    private async Task<BlobContainerClient> GetContainerAsync(string containerName, CancellationToken cancellationToken)
    {
        if (_service is null)
        {
            throw new ImageStorageUnavailableException("Azure Blob Storage is not configured. Set ConnectionStrings:AzureBlobStorage.");
        }

        var container = _service.GetBlobContainerClient(containerName);
        try
        {
            await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is RequestFailedException or HttpRequestException or AggregateException)
        {
            _logger.LogError(ex, "Could not reach blob container {Container}", containerName);
            throw new ImageStorageUnavailableException("Azure Blob Storage could not be reached.", ex);
        }

        return container;
    }
}
