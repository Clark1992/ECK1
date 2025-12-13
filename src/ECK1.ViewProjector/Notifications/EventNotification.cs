using MediatR;

namespace ECK1.ViewProjector.Notifications;

public record EventNotification<TEvent, TView>(TEvent Event, TView State) : INotification;
public record EventMessage<TEvent, TView>(TEvent Event, TView State) : IRequest<TView>;
