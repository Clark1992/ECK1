namespace ECK1.IntegrationContracts.Abstractions;

public interface IIntegrationEntity
{
    string Id { get; }

    int Version { get; }
}
