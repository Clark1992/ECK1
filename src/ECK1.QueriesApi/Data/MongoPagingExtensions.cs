using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System.Linq.Dynamic.Core;

namespace ECK1.QueriesAPI.Data;

public interface IPageRequest
{
    public int Top { get; set; }
    public int Skip { get; set; }
    public string Order { get; set; }
}

public static class MongoPagingExtensions
{
    public static IMongoQueryable<T> ApplyPaging<T>(
        this IMongoQueryable<T> query,
        IPageRequest request)
    {
        if (request == null) return query;

        if (!string.IsNullOrWhiteSpace(request.Order))
        {
            var direction = request.Order[0];
            var field = request.Order.Substring(1);

            var orderExpr = direction == '-' ? $"{field} descending" : field;

            query = (IMongoQueryable<T>)query.OrderBy(orderExpr);
        }
        else
        {
            query = (IMongoQueryable<T>)query.OrderBy("_id");
        }

        if (request.Skip > 0)
            query = query.Skip(request.Skip);

        if (request.Top > 0)
            query = query.Take(request.Top);

        return query;
    }

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