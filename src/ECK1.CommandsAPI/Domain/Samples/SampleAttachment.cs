namespace ECK1.CommandsAPI.Domain.Samples;

public class SampleAttachment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string FileName { get; private set; } = default!;
    public string Url { get; private set; } = default!;

    private SampleAttachment() { }

    public SampleAttachment(string fileName, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, nameof(fileName));

        FileName = fileName;
        Url = url;
    }

    public void ValidateUpdate(string newFileName, string newUrl)
    {
        ArgumentNullException.ThrowIfNull(newFileName);
        ArgumentNullException.ThrowIfNull(newUrl);
    }

    public void ApplyUpdate(string newFileName, string newUrl)
    {
        FileName = newFileName;
        Url = newUrl;
    }

    public SampleAttachment DeepClone() => new()
    {
        FileName = FileName,
        Url = Url,
        Id = Id
    };
}

