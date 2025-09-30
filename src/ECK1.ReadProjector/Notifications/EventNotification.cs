using MediatR;

namespace ECK1.ReadProjector.Notifications;

public record EventNotification<TEvent>(TEvent Event) : INotification;
