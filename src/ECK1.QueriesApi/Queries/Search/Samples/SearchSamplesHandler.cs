using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.QueriesAPI.Views;
using MediatR;

namespace ECK1.QueriesAPI.Queries.Search.Samples;

public class SearchSamplesHandler : IRequestHandler<SearchSamplesQuery, PagedResponse<SampleView>>
{
    private const string SampleIndex = "sample-full-records";

    private readonly ElasticsearchClient _client;

    public SearchSamplesHandler(ElasticsearchClient client)
    {
        _client = client;
    }

    public async Task<PagedResponse<SampleView>> Handle(SearchSamplesQuery request, CancellationToken ct)
    {
        var query = BuildSampleQuery(request);
        var sort = BuildSampleSort(request);

        var searchRequest = new SearchRequest(SampleIndex)
        {
            From = request.Skip,
            Size = request.Top,
            TrackTotalHits = new TrackHits(true),
            Query = query,
            Sort = sort.Count == 0 ? null : sort
        };

        var response = await _client.SearchAsync<SampleFullRecord>(searchRequest, ct);

        var items = (response.Hits ?? [])
            .Select(h => h.Source)
            .Where(x => x is not null)
            .Select(Map)
            .ToArray();

        return new PagedResponse<SampleView>
        {
            Items = items,
            Total = ElasticSearchShared.GetTotal(response.HitsMetadata?.Total)
        };
    }

    private static SampleView Map(SampleFullRecord record) => new()
    {
        SampleId = record.SampleId,
        Name = record.Name,
        Description = record.Description,
        Address = record.Address is null ? null : new SampleAddressView
        {
            City = record.Address.City,
            Country = record.Address.Country,
            Street = record.Address.Street,
        },
        Attachments = record.Attachments?.Select(a => new SampleAttachmentView
        {
            Id = a.Id,
            FileName = a.FileName,
            Url = a.Url,
        }).ToList() ?? new()
    };

    private static Query BuildSampleQuery(SearchSamplesQuery request)
    {
        var filter = new List<Query>();

        if (request.HasAddress is true)
            filter.Add(new ExistsQuery { Field = "address.country" });
        else if (request.HasAddress is false)
            filter.Add(new BoolQuery { MustNot = [new ExistsQuery { Field = "address.country" }] });

        if (request.HasAttachments is true)
            filter.Add(new NestedQuery { Path = "attachments", Query = new ExistsQuery { Field = "attachments.id" } });
        else if (request.HasAttachments is false)
            filter.Add(new BoolQuery { MustNot = [new NestedQuery { Path = "attachments", Query = new ExistsQuery { Field = "attachments.id" } }] });

        ElasticSearchShared.AddTermsFilter(filter, "address.country.keyword", request.Countries);
        ElasticSearchShared.AddTermsFilter(filter, "address.city.keyword", request.Cities);
        ElasticSearchShared.AddTermsFilter(filter, "address.street.keyword", request.Streets);

        var should = BuildSampleTextShould(request.Q);

        if (should.Count == 0 && filter.Count == 0)
            return null;

        return new BoolQuery
        {
            Filter = filter.Count == 0 ? null : filter,
            Should = should.Count == 0 ? null : should,
            MinimumShouldMatch = should.Count == 0 ? null : 1
        };
    }

    private static List<Query> BuildSampleTextShould(string q)
    {
        var should = new List<Query>();
        if (string.IsNullOrWhiteSpace(q))
            return should;

        if (ElasticSearchQueryText.CanUseWildcard(q))
        {
            var expr = ElasticSearchQueryText.BuildContainsQueryString(q);
            if (!string.IsNullOrWhiteSpace(expr))
            {
                should.Add(new QueryStringQuery
                {
                    Query = expr,
                    AnalyzeWildcard = true,
                    AllowLeadingWildcard = true,
                    Fields = new[]
                    {
                        "name",
                        "name.keyword",
                        "description",
                        "description.keyword",
                        "address.street",
                        "address.street.keyword",
                        "address.city",
                        "address.city.keyword",
                        "address.country",
                        "address.country.keyword"
                    }
                });
            }
        }
        else
        {
            should.Add(new MultiMatchQuery
            {
                Query = q,
                Fields = new[]
                {
                    "name",
                    "description",
                    "address.street",
                    "address.city",
                    "address.country"
                }
            });
        }

        // Nested attachments search (filename partial + url substring).
        var attachmentShould = new List<Query>();

        if (ElasticSearchQueryText.CanUseWildcard(q))
        {
            var expr = ElasticSearchQueryText.BuildContainsQueryString(q);
            if (!string.IsNullOrWhiteSpace(expr))
            {
                attachmentShould.Add(new QueryStringQuery
                {
                    Query = expr,
                    AnalyzeWildcard = true,
                    AllowLeadingWildcard = true,
                    Fields = new[] { "attachments.fileName", "attachments.fileName.keyword" }
                });
            }
        }
        else
        {
            attachmentShould.Add(new MultiMatchQuery
            {
                Query = q,
                Fields = new[] { "attachments.fileName" }
            });
        }

        if (q.Trim().Length >= 3)
        {
            attachmentShould.Add(new WildcardQuery
            {
                Field = "attachments.url",
                Value = $"*{ElasticSearchShared.EscapeWildcard(q.Trim())}*",
                CaseInsensitive = true,
            });
        }

        if (attachmentShould.Count > 0)
        {
            should.Add(new NestedQuery
            {
                Path = "attachments",
                Query = new BoolQuery { Should = attachmentShould, MinimumShouldMatch = 1 }
            });
        }

        return should;
    }

    private static List<SortOptions> BuildSampleSort(SearchSamplesQuery request)
    {
        var sort = new List<SortOptions>();

        if (!string.IsNullOrWhiteSpace(request.Order))
        {
            var (field, order) = ElasticSearchShared.ParseOrder(request.Order);
            var esField = field switch
            {
                "name" => "name.keyword",
                "description" => "description.keyword",
                "address.street" => "address.street.keyword",
                "address.city" => "address.city.keyword",
                "address.country" => "address.country.keyword",
                _ => null
            };

            if (esField is not null)
                sort.Add(ElasticSearchShared.SortField(esField, order));
        }

        if (sort.Count == 0)
            sort.Add(ElasticSearchShared.SortField("sampleId", SortOrder.Asc));

        return sort;
    }
}
