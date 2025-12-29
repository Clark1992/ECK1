namespace ECK1.IntegrationContracts.Abstractions;

public interface IIntegrationMessage
{
    string Id { get; }

    int Version { get; }
}
