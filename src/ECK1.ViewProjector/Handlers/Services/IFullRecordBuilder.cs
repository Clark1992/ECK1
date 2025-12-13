namespace ECK1.ViewProjector.Handlers.Services;

public interface IFullRecordBuilder<TView, TThinEvent, TRecord>
{
    Task<TRecord> BuildRecord(TView state, TThinEvent @event);
}
