using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Nagomi.Api.Infrastructure.Telemetry;

public static class NagomiTelemetry
{
    public const string ServiceName = "Nagomi.Api";
    public const string RabbitMqActivitySourceName = "Nagomi.RabbitMQ";
    public const string BackgroundWorkerActivitySourceName = "Nagomi.BackgroundWorkers";

    public static readonly ActivitySource RabbitMq = new(RabbitMqActivitySourceName);
    public static readonly ActivitySource BackgroundWorkers = new(BackgroundWorkerActivitySourceName);
}

public static class TelemetryServiceExtensions
{
    public static IServiceCollection AddNagomiTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(NagomiTelemetry.ServiceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = false;
                        options.Filter = context => !context.Request.Path.StartsWithSegments("/health");
                        options.EnrichWithHttpRequest = (activity, request) =>
                            activity.SetTag("url.query", request.QueryString.HasValue ? "[redacted]" : null);
                    })
                    .AddHttpClientInstrumentation(options => options.RecordException = true)
                    // Parameter values are not enabled, preventing clinical data from entering traces.
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddSource(
                    NagomiTelemetry.RabbitMqActivitySourceName,
                    NagomiTelemetry.BackgroundWorkerActivitySourceName);

                if (!string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
                {
                    tracing.AddOtlpExporter();
                }
            });

        return services;
    }
}
