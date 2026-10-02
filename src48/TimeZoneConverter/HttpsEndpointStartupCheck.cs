using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Hosting;

namespace TimeZoneConverter
{
    /// <summary>
    /// Fails host startup when the HTTPS endpoint (E2) is registered but the server has no https address.
    /// CoreWCF resolves endpoint base addresses lazily on the first request; without this check such a
    /// host would start and then answer every request, E1 included, with
    /// "Could not find a base address that matches scheme https".
    /// Runs in <see cref="IHostedLifecycleService.StartedAsync"/>, after the server has bound its addresses.
    /// </summary>
    internal sealed class HttpsEndpointStartupCheck : IHostedLifecycleService
    {
        private readonly IServer _server;

        public HttpsEndpointStartupCheck(IServer server)
        {
            _server = server;
        }

        /// <summary>Set by Program.cs once it has decided whether E2 is registered.</summary>
        public bool HttpsEndpointRegistered { get; set; }

        public Task StartedAsync(CancellationToken cancellationToken)
        {
            if (!HttpsEndpointRegistered)
            {
                return Task.CompletedTask;
            }

            var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses.ToArray() ?? Array.Empty<string>();
            if (addresses.Any(address => address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                return Task.CompletedTask;
            }

            throw new InvalidOperationException(
                "The HTTPS endpoint (E2) is registered (TimeZoneConverter:EnableHttpsEndpoint, or an HTTPS listener in configuration), " +
                $"but the server has no https address (listening on: {(addresses.Length == 0 ? "<none reported>" : string.Join(", ", addresses))}). " +
                "Add an HTTPS binding or listener, or set TimeZoneConverter:EnableHttpsEndpoint to 'auto' or 'false'.");
        }

        public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
