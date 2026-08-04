using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Server.AspNetCore;
using Nagomi.Api.Features.Audit;
using Nagomi.Api.Features.EmergencyTransports;
using Nagomi.Api.Features.Journeys;
using Nagomi.Api.Features.Operations;
using Nagomi.Api.Features.ProviderIntegration;
using Nagomi.Api.Features.ReferenceData;
using Nagomi.Api.Features.TransportRequests;
using Nagomi.Api.Features.UserAdministration;
using Nagomi.Api.Infrastructure.Authentication;
using Nagomi.Api.Infrastructure.Errors;
using Nagomi.Api.Infrastructure.Identity;
using Nagomi.Api.Infrastructure.Persistence;
using Nagomi.Api.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddNagomiPersistence(builder.Configuration);
builder.Services.AddNagomiProblemDetails();
builder.Services.AddNagomiTelemetry(builder.Configuration);
builder.Services.AddSimulatedIdentity(builder.Configuration);
builder.Services.AddReferenceData();
builder.Services.AddProviderAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddUserAuthentication(builder.Configuration);
builder.Services.AddProviderIntegration(builder.Configuration);
builder.Services.AddScoped<IProviderResourceGateway, TransportProviderResourceGateway>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHealthChecks().AddDbContextCheck<NagomiDbContext>();
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseExceptionHandler();
if (corsOrigins.Length > 0)
    app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => TypedResults.Ok(new { status = "healthy" }));
app.MapHealthChecks("/ready");
app.MapReferenceDataEndpoints();
app.MapTransportRequestEndpoints();
app.MapJourneyEndpoints();
app.MapOperationsEndpoints();
app.MapAuditEndpoints();
app.MapEmergencyTransportEndpoints();
app.MapUserEndpoints();
app.MapUserAdministrationEndpoints();
app.MapProviderIntegrationEndpoints();
app.MapProviderAuthenticationAdministrationEndpoints();

if (app.Configuration.GetValue("Database:MigrateOnStartup", app.Environment.IsDevelopment()))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<NagomiDbContext>().Database.MigrateAsync();
    await UserSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

await app.RunAsync();

public partial class Program;
