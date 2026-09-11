namespace CEBAS.Infrastructure.Configuration;

public class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Url { get; set; } = "http://localhost:9200";
    public string PostsIndex { get; set; } = "posts_current";
    public string UsersIndex { get; set; } = "users_current";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int RequestTimeoutSeconds { get; set; } = 5;
    public bool EnableDebugMode { get; set; } = false;
}
