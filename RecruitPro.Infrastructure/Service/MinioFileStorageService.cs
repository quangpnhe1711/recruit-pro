using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using RecruitPro.Application.Configurations;
using RecruitPro.Application.Interfaces;

namespace RecruitPro.Infrastructure.Service
{
    public class MinioFileStorageService : IFileStorageService
    {
        private readonly MinioSettings _settings;
        private readonly IMinioClient _client;
        public MinioFileStorageService(IOptions<MinioSettings> options)
        {
            _settings = options.Value;
            _client = new MinioClient()
                .WithEndpoint(_settings.Endpoint)
                .WithCredentials(_settings.AccessKey, _settings.SecretKey)
                .WithSSL(_settings.UseSsl)
                .Build();
        }

         public async Task<string> UploadFileAsync(Stream stream, string objectName, string contentType)
        {
            // ensure bucket exists
            bool exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(_settings.BucketName));
            if (!exists)
            {
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_settings.BucketName));
            }

            // ensure stream is seekable and has length
            MemoryStream ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            ms.Position = 0;

            var putArgs = new PutObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectName)
                .WithStreamData(ms)
                .WithObjectSize(ms.Length)
                .WithContentType(contentType ?? "application/octet-stream");

            await _client.PutObjectAsync(putArgs);

            // construct URL
            if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
            {
                return _settings.BaseUrl.TrimEnd('/') + "/" + _settings.BucketName + "/" + objectName;
            }

            var scheme = _settings.UseSsl ? "https" : "http";
            return $"{scheme}://{_settings.Endpoint.TrimEnd('/')}/{_settings.BucketName}/{objectName}";
        }

        public async Task DeleteFileAsync(string objectName)
        {
            try
            {
                await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_settings.BucketName).WithObject(objectName));
            }
            catch (Exception)
            {
                // ignore deletion errors - best effort cleanup
            }
        }
    }
}
