namespace TechHub.Core.Configuration;

public class MongoDbSettings
{
    public const string SectionName = "MongoDB";

    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "TechHubBoard";
    public string AnalyticsDatabaseName { get; set; } = "TechHubAnalytics";
}
