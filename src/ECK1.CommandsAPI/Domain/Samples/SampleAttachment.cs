namespace ECK1.CommandsAPI.Domain.Samples;

public class SampleAttachment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string FileName { get; private set; } = default!;
    public string Url { get; private set; } = default!;

    private SampleAttachment() { }

    public SampleAttachment(Guid id, string fileName, string url)
    {
        Id = id;
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        Url = url ?? throw new ArgumentNullException(nameof(url));
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
}

