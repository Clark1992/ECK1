using ECK1.FailedViewRebuilder.Data.Models;
using System.Linq.Expressions;

namespace ECK1.FailedViewRebuilder.Models;

public class QueryParams<TOrder>
{
    public Expression<Func<EventFailure, bool>> Filter { get; set; }
    public Expression<Func<EventFailure, TOrder>> OrderBy { get; set; }
    public bool IsAsc { get; set; }
    public int? Count { get; set; }
}
