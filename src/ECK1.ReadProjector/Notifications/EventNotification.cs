using MediatR;

namespace ECK1.ReadProjector.Notifications;

public record EventNotification<TEvent>(TEvent Event) : INotification;
public record EventWithStateNotification<TEvent, TView>(TEvent Event, TView State) : IRequest<TView>;
