namespace RecruitPro.Application.Configurations
{
    public class MinioSettings
    {
        public string Endpoint { get; set; } = null!;
        public string AccessKey { get; set; } = null!;
        public string SecretKey { get; set; } = null!;
        public string BucketName { get; set; } = null!;
        public bool UseSsl { get; set; } = false;
        public string? BaseUrl { get; set; }
    }
}
