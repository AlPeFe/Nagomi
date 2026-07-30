using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Nagomi.Api.Infrastructure.Persistence;
using OpenIddict.Validation.AspNetCore;

namespace Nagomi.Api.Infrastructure.Authentication;

public static class ProviderAuthenticationServiceExtensions
{
    public static IServiceCollection AddProviderAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(ProviderAuthenticationOptions.SectionName);
        var settings = section.Get<ProviderAuthenticationOptions>() ?? new();
        services.Configure<ProviderAuthenticationOptions>(section);

        // OpenIddict's entity sets are added to the existing Nagomi model without owning its database registration.
        services.AddDbContext<NagomiDbContext>(options => options.UseOpenIddict());

        services.AddOpenIddict()
            .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<NagomiDbContext>())
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("/connect/token")
                    .AllowClientCredentialsFlow()
                    .AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.HandleTokenRequestContext>(builder =>
                        builder.UseScopedHandler<ProviderClientCredentialsHandler>());

                var aspNetCore = options.UseAspNetCore();
                if (environment.IsDevelopment())
                    aspNetCore.DisableTransportSecurityRequirement();

                if (settings.Issuer is not null)
                    options.SetIssuer(settings.Issuer);

                ConfigureCertificates(options, settings, environment);
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.EnableTokenEntryValidation();
                options.UseAspNetCore();
            });

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        });
        services.AddHostedService<ProviderClientBootstrapService>();

        return services;
    }

    private static void ConfigureCertificates(
        OpenIddictServerBuilder server,
        ProviderAuthenticationOptions settings,
        IHostEnvironment environment)
    {
        var hasSigningCertificate = !string.IsNullOrWhiteSpace(settings.SigningCertificatePath);
        var hasEncryptionCertificate = !string.IsNullOrWhiteSpace(settings.EncryptionCertificatePath);
        if (hasSigningCertificate != hasEncryptionCertificate)
            throw new InvalidOperationException(
                $"{ProviderAuthenticationOptions.SectionName} must configure both signing and encryption certificates.");

        if (hasSigningCertificate)
        {
            server.AddSigningCertificate(LoadCertificate(
                settings.SigningCertificatePath!, settings.SigningCertificatePassword));
            server.AddEncryptionCertificate(LoadCertificate(
                settings.EncryptionCertificatePath!, settings.EncryptionCertificatePassword));
            return;
        }

        if (!environment.IsDevelopment())
            throw new InvalidOperationException(
                $"{ProviderAuthenticationOptions.SectionName} signing and encryption certificates are required outside Development.");

        server.AddEphemeralSigningKey();
        server.AddEphemeralEncryptionKey();
    }

    private static X509Certificate2 LoadCertificate(string path, string? password) =>
        X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);
}
