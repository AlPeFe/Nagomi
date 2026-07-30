namespace Nagomi.Api.Infrastructure.Authentication;

public sealed class ProviderAuthenticationOptions
{
    public const string SectionName = "Authentication:ProviderIntegration";

    public string? SigningCertificatePath { get; set; }
    public string? SigningCertificatePassword { get; set; }
    public string? EncryptionCertificatePath { get; set; }
    public string? EncryptionCertificatePassword { get; set; }
    public ProviderClientOptions? BootstrapClient { get; set; }
    public Uri? Issuer { get; set; }
}

public sealed class ProviderClientOptions
{
    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? DisplayName { get; set; }
    public Guid ProviderId { get; set; }
    public string Role { get; set; } = "operator";
    public string[] Contracts { get; set; } = [];
}
