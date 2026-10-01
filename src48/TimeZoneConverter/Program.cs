using System;
using System.Linq;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

var app = builder.Build();

// Former Web.config <httpProtocol><customHeaders>: X-Frame-Options: SAMEORIGIN on every response.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    await next(context);
});

// Former protocolMapping (https -> basicHttpsBinding): the default HTTPS endpoint only existed
// when the site had an https binding. Mirror that: E2 is added only when the host is configured
// with an HTTPS address.
bool hasHttpsBinding = HasHttpsBinding(app.Configuration);

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

    if (hasHttpsBinding)
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

static bool HasHttpsBinding(IConfiguration configuration)
{
    static bool IsHttps(string url) =>
        url != null && url.TrimStart().StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    // --urls / ASPNETCORE_URLS / launchSettings applicationUrl
    var urls = configuration["urls"];
    if (!string.IsNullOrWhiteSpace(urls) && urls.Split(';').Any(IsHttps))
    {
        return true;
    }

    // ASPNETCORE_HTTPS_PORTS (Kestrel) / ASPNETCORE_HTTPS_PORT (set by the ASP.NET Core Module for IIS https bindings)
    if (!string.IsNullOrWhiteSpace(configuration["https_ports"]) || !string.IsNullOrWhiteSpace(configuration["https_port"]))
    {
        return true;
    }

    // Kestrel:Endpoints:{name}:Url in application configuration
    return configuration.GetSection("Kestrel:Endpoints").GetChildren().Any(endpoint => IsHttps(endpoint["Url"]));
}