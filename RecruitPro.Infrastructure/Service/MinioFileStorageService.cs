using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service;

/// <summary>
/// Provides private-file storage operations backed by MinIO.
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly ILogger<MinioFileStorageService> _logger;
    private readonly MinioSettings _settings;

    public MinioFileStorageService(
        IMinioClient client,
        IOptions<MinioSettings> options,
        ILogger<MinioFileStorageService> logger)
    {
        _client = client;
        _logger = logger;
        _settings = options.Value;
    }

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

    private int GetPresignedUrlExpiryInSeconds()
    {
        return _settings.PresignedUrlExpiryInSeconds > 0
            ? _settings.PresignedUrlExpiryInSeconds
            : 3600;
    }

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
