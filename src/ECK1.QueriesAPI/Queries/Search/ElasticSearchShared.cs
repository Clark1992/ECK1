using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Clients.Elasticsearch.Core.Search;

namespace ECK1.QueriesAPI.Queries.Search;

internal static class ElasticSearchShared
{
    public static long GetTotal(Union<TotalHits, long> total) =>
        total is null ? 0 : total.Match(t => t.Value, v => v);

    public static (string Field, SortOrder Order) ParseOrder(string order)
    {
        var direction = order[0];
        var field = order.Length > 1 ? order.Substring(1) : string.Empty;
        return (field, direction == '-' ? SortOrder.Desc : SortOrder.Asc);
    }

    public static SortOptions SortField(string field, SortOrder order) =>
        new()
        {
            Field = new FieldSort { Field = field, Order = order }
        };

    public static void AddTermsFilter(List<Query> filter, string field, List<string> values)
    {
        if (values is null || values.Count == 0)
            return;

        filter.Add(new TermsQuery { Field = field, Terms = values.Select(v => FieldValue.String(v)).ToArray() });
    }

    public static string EscapeWildcard(string value) =>
        value.Replace("\\", "\\\\")
             .Replace("*", "\\*")
             .Replace("?", "\\?");
}
