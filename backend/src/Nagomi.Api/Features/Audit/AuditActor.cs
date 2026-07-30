namespace Nagomi.Api.Features.Audit;

public sealed record AuditActor
{
    private AuditActor(
        AuditSource source,
        string actorIdentifier,
        string actorDisplayName,
        string? providerIdentifier,
        string? providerName)
    {
        Source = source;
        ActorIdentifier = actorIdentifier;
        ActorDisplayName = actorDisplayName;
        ProviderIdentifier = providerIdentifier;
        ProviderName = providerName;
    }

    public AuditSource Source { get; }

    public string ActorIdentifier { get; }

    public string ActorDisplayName { get; }

    public string? ProviderIdentifier { get; }

    public string? ProviderName { get; }

    public static AuditActor ForSimulatedUser(string userIdentifier, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new AuditActor(AuditSource.SimulatedUser, userIdentifier, displayName, null, null);
    }

    public static AuditActor ForProvider(
        string clientIdentifier,
        string clientDisplayName,
        string providerIdentifier,
        string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerIdentifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        return new AuditActor(
            AuditSource.TransportProvider,
            clientIdentifier,
            clientDisplayName,
            providerIdentifier,
            providerName);
    }
}
