namespace CEBAS.Infrastructure.Configuration;

public class MediaStorageOptions
{
    public const string SectionName = "MediaStorage";

    public string Provider { get; set; } = "Local";
    public string RootPath { get; set; } = "Storage/Media";
    public int UploadUrlExpirationMinutes { get; set; } = 15;
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB
    public string? BaseUrl { get; set; }

    // S3 / R2 / MinIO compatibility settings
    public string? BucketName { get; set; } = "cebas-media";
    public string? Region { get; set; } = "us-east-1";
    public string? ServiceUrl { get; set; } = "http://localhost:9000";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public bool ForcePathStyle { get; set; } = true;
}
