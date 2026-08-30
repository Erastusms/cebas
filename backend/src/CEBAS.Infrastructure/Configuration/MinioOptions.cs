namespace CEBAS.Infrastructure.Configuration;

public class MinioOptions
{
    public const string SectionName = "Minio";

    public string Endpoint { get; set; } = "http://localhost:9000";
    public string AccessKey { get; set; } = "cebas_minio_admin";
    public string SecretKey { get; set; } = "cebas_minio_secure_password";
    public string Bucket { get; set; } = "cebas-media";
    public int ConsolePort { get; set; } = 9001;
}
