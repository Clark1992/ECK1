using ECK1.CommonUtils.Mapping;
using ECK1.ViewProjector.Events;
using ECK1.ViewProjector.Handlers;
using Contract = ECK1.Contracts.Kafka.BusinessEvents.Sample2;

namespace ECK1.ViewProjector.Mapping;

public class Sample2EventsMapping : MapByInterface<Contract.ISample2Event, ISample2Event>
{
    public Sample2EventsMapping() : base()
    {
        this.CreateMap<Sample2EventFailure, Contract.Sample2EventFailure>();
    }
}
