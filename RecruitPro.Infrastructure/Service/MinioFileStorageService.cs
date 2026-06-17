using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinioFileStorageService> _logger;
    private readonly MinioSettings _settings;

    /// <summary>
    /// Initializes a new instance of the MinioFileStorageService class.
    /// </summary>
    /// <param name="client">The <paramref name="client"/> value.</param>
    /// <param name="options">The <paramref name="options"/> value.</param>
    /// <param name="logger">The <paramref name="logger"/> value.</param>
    public MinioFileStorageService(
        IMinioClient client,
        IOptions<MinioSettings> options,
        ILogger<MinioFileStorageService> logger)
    {
        _client = client;
        _logger = logger;
        _settings = options.Value;
    }

    /// <summary>
    /// Uploads file.
    /// </summary>
    /// <param name="stream">The <paramref name="stream"/> value.</param>
    /// <param name="objectName">The <paramref name="objectName"/> value.</param>
    /// <param name="contentType">The <paramref name="contentType"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
    public async Task<string> UploadFileAsync(Stream stream, string objectName, string contentType)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(objectName);

        await EnsureBucketExistsAsync();

        await using MemoryStream bufferedStream = new();
        await stream.CopyToAsync(bufferedStream);
        bufferedStream.Position = 0;

        PutObjectArgs putArgs = new PutObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectName)
            .WithStreamData(bufferedStream)
            .WithObjectSize(bufferedStream.Length)
            .WithContentType(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        try
        {
            await _client.PutObjectAsync(putArgs);
            _logger.LogInformation(
                "Uploaded object {ObjectName} to private bucket {BucketName}.",
                objectName,
                _settings.BucketName);

            return objectName;
        }
        catch (MinioException ex)
        {
            _logger.LogError(
                ex,
                "Failed to upload object {ObjectName} to bucket {BucketName}.",
                objectName,
                _settings.BucketName);
            throw;
        }
    }

    /// <summary>
    /// Retrieves presigned url.
    /// </summary>
    /// <param name="objectName">The <paramref name="objectName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<string> GetPresignedUrlAsync(string objectName)
    {
        string normalizedObjectName = NormalizeObjectName(objectName);
        await EnsureBucketExistsAsync();

        PresignedGetObjectArgs args = new PresignedGetObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(normalizedObjectName)
            .WithExpiry(GetPresignedUrlExpiryInSeconds());

        try
        {
            string presignedUrl = await _client.PresignedGetObjectAsync(args);
            _logger.LogInformation(
                "Generated presigned URL for object {ObjectName} in bucket {BucketName}.",
                normalizedObjectName,
                _settings.BucketName);

            return presignedUrl;
        }
        catch (MinioException ex)
        {
            _logger.LogError(
                ex,
                "Failed to generate presigned URL for object {ObjectName} in bucket {BucketName}.",
                normalizedObjectName,
                _settings.BucketName);
            throw;
        }
    }

    /// <summary>
    /// Executes the download file operation.
    /// </summary>
    /// <param name="objectName">The <paramref name="objectName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation and returns the operation result.</returns>
    public async Task<Stream> DownloadFileAsync(string objectName)
    {
        string normalizedObjectName = NormalizeObjectName(objectName);
        await EnsureBucketExistsAsync();

        MemoryStream destination = new();
        GetObjectArgs args = new GetObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(normalizedObjectName)
            .WithCallbackStream(stream => stream.CopyTo(destination));

        try
        {
            await _client.GetObjectAsync(args);
            destination.Position = 0;
            return destination;
        }
        catch (MinioException ex)
        {
            destination.Dispose();
            _logger.LogWarning(
                ex,
                "Failed to download object {ObjectName} from bucket {BucketName}.",
                normalizedObjectName,
                _settings.BucketName);
            throw;
        }
    }

    /// <summary>
    /// Deletes file.
    /// </summary>
    /// <param name="objectName">The <paramref name="objectName"/> value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task DeleteFileAsync(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return;
        }

        string normalizedObjectName = NormalizeObjectName(objectName);

        try
        {
            await _client.RemoveObjectAsync(
                new RemoveObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(normalizedObjectName));

            _logger.LogInformation(
                "Deleted object {ObjectName} from bucket {BucketName}.",
                normalizedObjectName,
                _settings.BucketName);
        }
        catch (MinioException ex)
        {
            _logger.LogWarning(
                ex,
                "Best-effort deletion failed for object {ObjectName} in bucket {BucketName}.",
                normalizedObjectName,
                _settings.BucketName);
        }
    }

    /// <summary>
    /// Ensures bucket exists.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task EnsureBucketExistsAsync()
    {
        bool bucketExists = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_settings.BucketName));

        if (bucketExists)
        {
            return;
        }

        await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_settings.BucketName));
        _logger.LogInformation("Created MinIO bucket {BucketName}.", _settings.BucketName);
    }

    /// <summary>
    /// Retrieves presigned url expiry in seconds.
    /// </summary>
    /// <returns>The operation result.</returns>
    private int GetPresignedUrlExpiryInSeconds()
    {
        return _settings.PresignedUrlExpiryInSeconds > 0
            ? _settings.PresignedUrlExpiryInSeconds
            : 3600;
    }

    /// <summary>
    /// Normalizes object name.
    /// </summary>
    /// <param name="objectName">The <paramref name="objectName"/> value.</param>
    /// <returns>The resulting string value.</returns>
    /// <exception cref="ArgumentException">Thrown when the operation fails validation or encounters an invalid state.</exception>
    private string NormalizeObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Object name must be provided.", nameof(objectName));
        }

        string trimmedValue = objectName.Trim();
        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out Uri? uri))
        {
            return trimmedValue.TrimStart('/');
        }

        string path = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/');
        string bucketPrefix = $"{_settings.BucketName}/";
        if (path.StartsWith(bucketPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return path[bucketPrefix.Length..];
        }

        return path;
    }
}
