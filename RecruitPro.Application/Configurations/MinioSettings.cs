namespace RecruitPro.Application.Configurations
{
    /// <summary>
    /// MinIO connection and presigned URL configuration.
    /// </summary>
    public class MinioSettings
    {
        public string Endpoint { get; set; } = null!;
        public string AccessKey { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
        public string BucketName { get; set; } = null!;
        public bool UseSsl { get; set; } = false;
        public string? BaseUrl { get; set; }
        public int PresignedUrlExpiryInSeconds { get; set; } = 3600;
    }
}
