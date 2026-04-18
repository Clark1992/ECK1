namespace ECK1.QueriesAPI;

public class ElasticSearchConfig
{
    public string ApiKey { get; set; }

    public string ClusterUrl { get; set; }
}

public class ClickhouseConfig
{
    public string ConnectionString { get; set; } = "";
}
