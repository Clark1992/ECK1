using MongoDB.Driver;

namespace ECK1.QueriesAPI.Data;

public interface IPageRequest
{
    public int Top { get; set; }
    public int Skip { get; set; }
    public string Order { get; set; }
}

public static class MongoPagingExtensions
{
    public static IFindFluent<T, T> ApplyPaging<T>(
        this IFindFluent<T, T> find,
        IPageRequest request)
    {
        if (request == null) return find;

        if (!string.IsNullOrWhiteSpace(request.Order))
        {
            var direction = request.Order[0];
            var field = request.Order.Substring(1);

            var sort = direction == '-'
                ? Builders<T>.Sort.Descending(field)
                : Builders<T>.Sort.Ascending(field);

            find = find.Sort(sort);
        }
        else
        {
            find = find.SortBy(x => x);
        }

        if (request.Skip > 0)
            find = find.Skip(request.Skip);

        if (request.Top > 0)
            find = find.Limit(request.Top);

        return find;
    }
}