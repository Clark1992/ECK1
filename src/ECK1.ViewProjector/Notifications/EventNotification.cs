using MediatR;

namespace ECK1.ViewProjector.Notifications;

public record EventNotification<TEvent>(TEvent Event) : INotification;
public record EventWithStateNotification<TEvent, TView>(TEvent Event, TView State) : IRequest<TView>;
