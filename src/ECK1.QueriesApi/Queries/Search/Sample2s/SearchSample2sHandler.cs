using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.Search;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using ECK1.QueriesAPI.Views;
using MediatR;

namespace ECK1.QueriesAPI.Queries.Search.Sample2s;

public class SearchSample2sHandler : IRequestHandler<SearchSample2sQuery, PagedResponse<Sample2View>>
{
    private const string Sample2Index = "sample2-full-records";

    private readonly ElasticsearchClient _client;

    public SearchSample2sHandler(ElasticsearchClient client)
    {
        _client = client;
    }

    public async Task<PagedResponse<Sample2View>> Handle(SearchSample2sQuery request, CancellationToken ct)
    {
        var query = BuildSample2Query(request);
        var sort = BuildSample2Sort(request);

        var searchRequest = new SearchRequest(Sample2Index)
        {
            From = request.Skip,
            Size = request.Top,
            TrackTotalHits = new TrackHits(true),
            Query = query,
            Sort = sort.Count == 0 ? null : sort
        };

        var response = await _client.SearchAsync<Sample2FullRecord>(searchRequest, ct);

        var items = (response.Hits ?? [])
            .Select(h => h.Source)
            .Where(x => x is not null)
            .Select(Map)
            .ToArray();

        return new PagedResponse<Sample2View>
        {
            Items = items,
            Total = ElasticSearchShared.GetTotal(response.HitsMetadata?.Total)
        };
    }

    private static Sample2View Map(Sample2FullRecord record) => new()
    {
        Sample2Id = record.Sample2Id,
        Customer = record.Customer is null ? null : new Sample2CustomerView
        {
            CustomerId = record.Customer.CustomerId,
            Email = record.Customer.Email,
            Segment = record.Customer.Segment,
        },
        ShippingAddress = record.ShippingAddress is null ? null : new Sample2AddressView
        {
            City = record.ShippingAddress.City,
            Country = record.ShippingAddress.Country,
            Street = record.ShippingAddress.Street,
        },
        LineItems = record.LineItems?.Select(li => new Sample2LineItemView
        {
            ItemId = li.ItemId,
            Quantity = li.Quantity,
            Sku = li.Sku,
            UnitPrice = li.UnitPrice is null ? null : new Sample2MoneyView
            {
                Amount = li.UnitPrice.Amount,
                Currency = li.UnitPrice.Currency,
            }
        }).ToList() ?? new(),
        Tags = record.Tags?.Select(t => t.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? new(),
        Status = (int)record.Status,
    };

    private static Query BuildSample2Query(SearchSample2sQuery request)
    {
        var filter = new List<Query>();

        if (request.HasCustomer is true)
            filter.Add(new ExistsQuery { Field = "customer.customerId" });
        else if (request.HasCustomer is false)
            filter.Add(new BoolQuery { MustNot = [new ExistsQuery { Field = "customer.customerId" }] });

        if (request.HasShippingAddress is true)
            filter.Add(new ExistsQuery { Field = "shippingAddress.country" });
        else if (request.HasShippingAddress is false)
            filter.Add(new BoolQuery { MustNot = [new ExistsQuery { Field = "shippingAddress.country" }] });

        if (request.HasLineItems is true)
            filter.Add(new NestedQuery { Path = "lineItems", Query = new ExistsQuery { Field = "lineItems.itemId" } });
        else if (request.HasLineItems is false)
            filter.Add(new BoolQuery { MustNot = [new NestedQuery { Path = "lineItems", Query = new ExistsQuery { Field = "lineItems.itemId" } }] });

        ElasticSearchShared.AddTermsFilter(filter, "shippingAddress.country.keyword", request.Countries);
        ElasticSearchShared.AddTermsFilter(filter, "shippingAddress.city.keyword", request.Cities);
        ElasticSearchShared.AddTermsFilter(filter, "shippingAddress.street.keyword", request.Streets);

        if (request.Tags.Count > 0)
        {
            filter.Add(new NestedQuery
            {
                Path = "tags",
                Query = new TermsQuery { Field = "tags.value.keyword", Terms = request.Tags.Select(t => FieldValue.String(t)).ToArray() }
            });
        }

        if (request.ExcludeTags.Count > 0)
        {
            filter.Add(new BoolQuery
            {
                MustNot =
                [
                    new NestedQuery
                    {
                        Path = "tags",
                        Query = new TermsQuery { Field = "tags.value.keyword", Terms = request.ExcludeTags.Select(t => FieldValue.String(t)).ToArray() }
                    }
                ]
            });
        }

        if (request.Statuses.Count > 0)
        {
            var statusTerms = request.Statuses
                .Select(s => s?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(NormalizeStatus)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(FieldValue.String)
                .ToArray();

            filter.Add(new TermsQuery { Field = "status", Terms = statusTerms });
        }

        AddLineItemAmountFilter(filter, request.LineItemUnitPriceAmountGt, request.HasLineItemUnitPriceAmountGt, isGreaterThan: true);
        AddLineItemAmountFilter(filter, request.LineItemUnitPriceAmountLt, request.HasLineItemUnitPriceAmountLt, isGreaterThan: false);

        var should = BuildSample2TextShould(request.Q);

        if (should.Count == 0 && filter.Count == 0)
            return null;

        return new BoolQuery
        {
            Filter = filter.Count == 0 ? null : filter,
            Should = should.Count == 0 ? null : should,
            MinimumShouldMatch = should.Count == 0 ? null : 1
        };
    }

    private static List<Query> BuildSample2TextShould(string q)
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
                        "sample2Id",
                        "customer.email",
                        "customer.email.keyword",
                        "customer.segment",
                        "customer.segment.keyword",
                        "shippingAddress.street",
                        "shippingAddress.street.keyword",
                        "shippingAddress.city",
                        "shippingAddress.city.keyword",
                        "shippingAddress.country",
                        "shippingAddress.country.keyword",
                        "status"
                    }
                });

                should.Add(new NestedQuery
                {
                    Path = "lineItems",
                    Query = new QueryStringQuery
                    {
                        Query = expr,
                        AnalyzeWildcard = true,
                        AllowLeadingWildcard = true,
                        Fields = new[] { "lineItems.sku", "lineItems.sku.keyword" }
                    }
                });

                should.Add(new NestedQuery
                {
                    Path = "tags",
                    Query = new QueryStringQuery
                    {
                        Query = expr,
                        AnalyzeWildcard = true,
                        AllowLeadingWildcard = true,
                        Fields = new[] { "tags.value", "tags.value.keyword" }
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
                    "sample2Id",
                    "customer.email",
                    "customer.segment",
                    "shippingAddress.street",
                    "shippingAddress.city",
                    "shippingAddress.country",
                    "status"
                }
            });

            should.Add(new NestedQuery
            {
                Path = "lineItems",
                Query = new MultiMatchQuery { Query = q, Fields = new[] { "lineItems.sku" } }
            });

            should.Add(new NestedQuery
            {
                Path = "tags",
                Query = new MultiMatchQuery { Query = q, Fields = new[] { "tags.value" } }
            });
        }

        return should;
    }

    private static List<SortOptions> BuildSample2Sort(SearchSample2sQuery request)
    {
        var sort = new List<SortOptions>();

        if (!string.IsNullOrWhiteSpace(request.Order))
        {
            var (field, order) = ElasticSearchShared.ParseOrder(request.Order);
            var esField = field switch
            {
                "sample2Id" => "sample2Id",
                "customer.email" => "customer.email.keyword",
                "customer.segment" => "customer.segment.keyword",
                "shippingAddress.street" => "shippingAddress.street.keyword",
                "shippingAddress.city" => "shippingAddress.city.keyword",
                "shippingAddress.country" => "shippingAddress.country.keyword",
                "status" => "status",
                _ => null
            };

            if (esField is not null)
                sort.Add(ElasticSearchShared.SortField(esField, order));
        }

        if (sort.Count == 0)
            sort.Add(ElasticSearchShared.SortField("sample2Id", SortOrder.Asc));

        return sort;
    }

    private static void AddLineItemAmountFilter(List<Query> filter, decimal? amount, bool? has, bool isGreaterThan)
    {
        if (amount is null)
            return;

        var mustHave = has ?? true;

        var range = new NumberRangeQuery
        {
            Field = "lineItems.unitPrice.amount",
            Gt = isGreaterThan ? (double?)amount : null,
            Lt = isGreaterThan ? null : (double?)amount,
        };

        var nested = new NestedQuery { Path = "lineItems", Query = range };

        if (mustHave)
            filter.Add(nested);
        else
            filter.Add(new BoolQuery { MustNot = [nested] });
    }

    private static string NormalizeStatus(string status)
    {
        // ES stores status as enum name string (keyword). Accept either enum name ("Paid")
        // or numeric values ("2") and normalize to the canonical enum name.
        if (int.TryParse(status, out var numeric) && Enum.IsDefined(typeof(Sample2Status), numeric))
            return ((Sample2Status)numeric).ToString();

        if (Enum.TryParse<Sample2Status>(status, ignoreCase: true, out var parsed))
            return parsed.ToString();

        return status;
    }
}
