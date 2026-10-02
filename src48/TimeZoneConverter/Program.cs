using System;
using System.Linq;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeZoneConverter;
using TimeZoneConverter.Contracts;

// ASP.NET Core + CoreWCF host for TimeZoneConverterService.
// Replaces the IIS .svc activation (TimeZoneConverter.svc) and the <system.serviceModel>
// section of the former Web.config. See the upgrade workflow task
// 03-corewcf-service-host for the setting-by-setting mapping.

const string ServicePath = "/TimeZoneConverter.svc";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();

// WSDL soap:address / wsdl:import / help-page URLs are built from the request's Host header
// (as IIS-hosted WCF did) instead of the Kestrel listen address (e.g. 0.0.0.0 or a container IP).
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

builder.Services.AddSingleton<HttpsEndpointStartupCheck>();
builder.Services.AddHostedService(services => services.GetRequiredService<HttpsEndpointStartupCheck>());

var app = builder.Build();

// Former Web.config <httpProtocol><customHeaders>: X-Frame-Options: SAMEORIGIN on every response.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    await next(context);
});

// Former protocolMapping (https -> basicHttpsBinding): the default HTTPS endpoint only existed
// when the site had an https binding. Mirror that with the TimeZoneConverter:EnableHttpsEndpoint
// setting (appsettings.json, or env var TimeZoneConverter__EnableHttpsEndpoint):
//   auto  (default) E2 is added only when a real HTTPS listener is configured for Kestrel
//                   (urls / ASPNETCORE_URLS, https_ports / ASPNETCORE_HTTPS_PORTS, Kestrel:Endpoints:*:Url).
//   true            E2 is always added. Use this under IIS in-process hosting, where the site's https
//                   binding is not visible in configuration (ANCM only sets ASPNETCORE_HTTPS_PORT).
//   false           E2 is never added.
// Whenever E2 is added, HttpsEndpointStartupCheck fails startup if the server reports no https address
// (CoreWCF would otherwise fail every request, E1 included, on first use).
bool registerHttpsEndpoint = ShouldRegisterHttpsEndpoint(app.Configuration, app.Logger);
app.Services.GetRequiredService<HttpsEndpointStartupCheck>().HttpsEndpointRegistered = registerHttpsEndpoint;

app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<TimeZoneConverterService>(options =>
    {
        // Former <serviceDebug includeExceptionDetailInFaults="false"/>
        options.DebugBehavior.IncludeExceptionDetailInFaults = false;
    });

    // E1: former default endpoint for the http scheme (basicHttpBinding, Security None)
    serviceBuilder.AddServiceEndpoint<TimeZoneConverterService, ITimeZoneConverter>(
        new BasicHttpBinding(), ServicePath);

    if (registerHttpsEndpoint)
    {
        // E2: former default endpoint for the https scheme (protocolMapping -> basicHttpsBinding,
        // Transport security, no client credential). Named like WCF's BasicHttpsBinding so the
        // WSDL port/binding name matches the original default endpoint.
        serviceBuilder.AddServiceEndpoint<TimeZoneConverterService, ITimeZoneConverter>(
            new BasicHttpBinding(BasicHttpSecurityMode.Transport) { Name = "BasicHttpsBinding" }, ServicePath);
    }
});

// Former <serviceMetadata httpGetEnabled="true" httpsGetEnabled="true"/>
var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
serviceMetadataBehavior.HttpGetEnabled = true;
serviceMetadataBehavior.HttpsGetEnabled = true;

app.Run();

static bool ShouldRegisterHttpsEndpoint(IConfiguration configuration, ILogger logger)
{
    const string SwitchKey = "TimeZoneConverter:EnableHttpsEndpoint";

    string mode = configuration[SwitchKey]?.Trim();
    if (string.IsNullOrEmpty(mode) || string.Equals(mode, "auto", StringComparison.OrdinalIgnoreCase))
    {
        if (HasHttpsListener(configuration))
        {
            return true;
        }

        // ASPNETCORE_HTTPS_PORT (https_port) is only the HTTPS redirection target port, not a listener.
        // The ASP.NET Core Module sets it for IIS sites with an https binding, so it is the one hint
        // that E2 may be wanted on a server whose HTTPS address is not visible here.
        if (!string.IsNullOrWhiteSpace(configuration["https_port"]))
        {
            logger.LogWarning(
                "https_port (ASPNETCORE_HTTPS_PORT) is set but no HTTPS listener is configured, so the HTTPS endpoint (E2) is not registered. " +
                "If the server provides an HTTPS binding (for example IIS), set {SwitchKey}=true.",
                SwitchKey);
        }

        return false;
    }

    if (bool.TryParse(mode, out bool enabled))
    {
        return enabled;
    }

    throw new InvalidOperationException($"Invalid value '{mode}' for {SwitchKey}. Expected 'auto', 'true' or 'false'.");
}

static bool HasHttpsListener(IConfiguration configuration)
{
    static bool IsHttps(string url) =>
        url != null && url.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    // --urls / ASPNETCORE_URLS / launchSettings applicationUrl
    var urls = configuration["urls"];
    if (!string.IsNullOrWhiteSpace(urls) && urls.Split(';').Any(IsHttps))
    {
        return true;
    }

    // --https_ports / ASPNETCORE_HTTPS_PORTS: Kestrel binds https://*:{port} for each value
    if (!string.IsNullOrWhiteSpace(configuration["https_ports"]))
    {
        return true;
    }

    // Kestrel:Endpoints:{name}:Url in application configuration
    return configuration.GetSection("Kestrel:Endpoints").GetChildren().Any(endpoint => IsHttps(endpoint["Url"]));
}