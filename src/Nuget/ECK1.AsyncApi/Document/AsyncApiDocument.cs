namespace ECK1.AsyncApi.Document;

public class AsyncApiDocument
{
    public string ServiceName { get; set; } = string.Empty;
    public List<AsyncApiCommandDescriptor> Commands { get; set; } = [];
}

public class AsyncApiCommandDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public string Route { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string KeyProperty { get; set; }
    public List<AsyncApiPropertyDescriptor> Properties { get; set; } = [];
}

public class AsyncApiPropertyDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public bool IsComplex { get; set; }
    public bool IsCollection { get; set; }
    public string Source { get; set; } = "body";
    public string SourceName { get; set; }
    public List<AsyncApiPropertyDescriptor> Properties { get; set; }
}
