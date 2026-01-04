using ECK1.QueriesAPI.Views;

namespace ECK1.QueriesAPI.Queries.Search.Samples;

public record SearchSamplesQuery : PagedQuery<SampleView>
{
    public string Q { get; set; } = string.Empty;

    public bool? HasAttachments { get; set; }
    public bool? HasAddress { get; set; }

    public List<string> Countries { get; set; } = new();
    public List<string> Cities { get; set; } = new();
    public List<string> Streets { get; set; } = new();
}
