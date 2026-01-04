using System.Linq.Expressions;

namespace ECK1.FailedViewRebuilder.Models;

public class QueryParams<TEntity, TKey>
    where TEntity: class
{
    public Expression<Func<TEntity, bool>> Filter { get; set; }
    public Expression<Func<TEntity, TKey>> OrderBy { get; set; }
    public bool IsAsc { get; set; }
    public int? Count { get; set; }
}
