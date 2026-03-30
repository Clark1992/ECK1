using AutoMapper;
using ECK1.Reconciliation.Contracts;
using ECK1.Reconciler.Data.Models;

namespace ECK1.Reconciler.Mapping;

public class ReconcilerProfile : Profile
{
    public ReconcilerProfile()
    {
        CreateMap<EntityState, ReconcileRequestItem>();
    }
}
