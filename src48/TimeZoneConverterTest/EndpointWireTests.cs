using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimeZoneConverter;
using TimeZoneConverter.Contracts;

namespace TimeZoneConverterTest
{
    /// <summary>
    /// Endpoint-level wire tests (upgrade finding WCF-010). These tests replay the SoapUI raw-XML
    /// requests against the CoreWCF host in-process over real HTTP on loopback (Kestrel bound to
    /// 127.0.0.1 on a dynamic port). They assert Strict XML parity with the golden net481 baseline
    /// in <c>WireBaseline/</c>, which was captured from the original .NET Framework WCF host.
    /// The class also covers the host's endpoint-selection settings (TimeZoneConverter:EnableHttpsEndpoint,
    /// https_port) and Host-header-based metadata addresses, each on its own short-lived loopback host.
    /// </summary>
    [TestClass]
    public class EndpointWireTests
    {
        private const string ServicePath = "/TimeZoneConverter.svc";

        // Authority of the original host recorded in the golden metadata (IISUrl / SoapUI endpoint).
        private const string GoldenAuthority = "http://localhost:50355";

        private const string SoapEnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
        private const string ServiceNamespace = "http://bartjolling.github.io/";
        private const string XsdNamespace = "http://www.w3.org/2001/XMLSchema";
        private const string XsiNamespace = "http://www.w3.org/2001/XMLSchema-instance";

        private static readonly string s_baselineRoot = Path.Combine(AppContext.BaseDirectory, "WireBaseline");

        private static WebApplicationFactory<Program> s_factory;
        private static HttpClient s_client;
        private static string s_baseAddress;
        private static JsonElement s_manifest;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            using (JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(s_baselineRoot, "capture-manifest.json"))))
            {
                s_manifest = manifest.RootElement.Clone();
            }

            s_factory = StartLoopbackHost(new PinnedHostFactory(), out s_baseAddress);
            s_client = new HttpClient { BaseAddress = new Uri(s_baseAddress) };
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            s_client?.Dispose();
            s_factory?.Dispose();
        }

        [TestMethod]
        public void TestHost_ListensOnLoopbackOnly()
        {
            var uri = new Uri(s_baseAddress);
            Assert.AreEqual(Uri.UriSchemeHttp, uri.Scheme);
            Assert.IsTrue(IPAddress.IsLoopback(IPAddress.Parse(uri.Host)), $"Test host must bind to loopback only, got {s_baseAddress}");
        }

        [TestMethod]
        public void GoldenBaseline_FilesMatchCaptureManifest()
        {
            int checkedFiles = 0;
            foreach (JsonElement entry in s_manifest.GetProperty("metadata").EnumerateArray())
            {
                ReadGolden(entry.GetProperty("file").GetString(), entry.GetProperty("sha256").GetString());
                checkedFiles++;
            }

            foreach (JsonElement entry in s_manifest.GetProperty("envelopes").EnumerateArray())
            {
                ReadGolden(entry.GetProperty("requestFile").GetString(), entry.GetProperty("requestSha256").GetString());
                ReadGolden(entry.GetProperty("responseFile").GetString(), entry.GetProperty("responseSha256").GetString());
                checkedFiles += 2;
            }

            Assert.AreEqual(15, checkedFiles);
        }

        [TestMethod]
        [DataRow("ConvertToLowerOffset", "2022-09-24T09:23:45.6780000+01:00")]
        [DataRow("ConvertToHigherOffset", "2022-09-24T15:23:45.6780000+07:00")]
        [DataRow("ConvertToSameOffset", "2022-09-24T10:23:45.6780000+02:00")]
        [DataRow("ConvertToNegativeOffset", "2022-09-24T02:23:45.6780000-06:00")]
        public async Task ConvertToOffset_SoapUiRequest_ResponseMatchesGoldenBaseline(string sample, string expectedToDateTime)
        {
            JsonElement entry = GetEnvelopeEntry(sample);
            byte[] golden = ReadGolden(entry.GetProperty("responseFile").GetString(), entry.GetProperty("responseSha256").GetString());

            using HttpResponseMessage response = await ReplayAsync(s_client, entry);
            byte[] actual = await response.Content.ReadAsByteArrayAsync();

            // HTTP level
            Assert.AreEqual(entry.GetProperty("status").GetInt32(), (int)response.StatusCode);
            AssertResponseHeaders(entry.GetProperty("responseHeaders"), response);

            // Strict XML parity: the whole envelope is byte-identical to the original WCF host's response
            Assert.AreEqual(Encoding.UTF8.GetString(golden), Encoding.UTF8.GetString(actual), $"{sample}: SOAP response differs from golden baseline");
            CollectionAssert.AreEqual(golden, actual, $"{sample}: SOAP response bytes differ from golden baseline");

            // Explicit structural checks, so a failure names the item that broke.
            XElement envelope = XElement.Parse(Encoding.UTF8.GetString(actual));
            Assert.AreEqual(XName.Get("Envelope", SoapEnvelopeNamespace), envelope.Name);
            Assert.AreEqual("s", envelope.GetPrefixOfNamespace(SoapEnvelopeNamespace));

            // Reply Action: BasicHttpBinding is SOAP 1.1 with AddressingNone, so the reply Action
            // ("ConvertToOffset", WCF-006) is never written to the wire: no <s:Header>, as in the baseline.
            // ReplyAction_IsPreservedByResponseDispatchFormatter covers the code-level value.
            XElement body = envelope.Elements().Single();
            Assert.AreEqual(XName.Get("Body", SoapEnvelopeNamespace), body.Name, "Response must not carry a SOAP header");
            Assert.IsFalse(response.Headers.Contains("SOAPAction"), "No SOAPAction header on the response");

            XElement operationResponse = body.Elements().Single();
            Assert.AreEqual(XName.Get("ConvertToOffsetResponse", ServiceNamespace), operationResponse.Name);
            CollectionAssert.AreEqual(
                new[] { "xmlns=" + ServiceNamespace, "xmlns:xsd=" + XsdNamespace, "xmlns:xsi=" + XsiNamespace },
                operationResponse.Attributes().Select(a => (a.Name.LocalName == "xmlns" ? "xmlns" : "xmlns:" + a.Name.LocalName) + "=" + a.Value).ToArray(),
                "Namespace declarations on ConvertToOffsetResponse (default ns, then xsd before xsi)");

            XElement conversionResponse = operationResponse.Elements().Single();
            Assert.AreEqual(XName.Get("TimeZoneConversionResponse", ServiceNamespace), conversionResponse.Name);

            XElement toDateTime = conversionResponse.Elements().Single();
            Assert.AreEqual(XName.Get("ToDateTime", ServiceNamespace), toDateTime.Name, "Custom serializer must emit only <ToDateTime> (WCF-005)");
            Assert.AreEqual(expectedToDateTime, toDateTime.Value);

            // The DataRow value must be what the golden baseline recorded.
            Assert.AreEqual(expectedToDateTime, XElement.Parse(Encoding.UTF8.GetString(golden)).Descendants(XName.Get("ToDateTime", ServiceNamespace)).Single().Value);
        }

        [TestMethod]
        public async Task UnknownAction_FaultMatchesGoldenBaselineStructure()
        {
            JsonElement entry = GetEnvelopeEntry("FaultProbe-UnknownAction");
            byte[] golden = ReadGolden(entry.GetProperty("responseFile").GetString(), entry.GetProperty("responseSha256").GetString());

            using HttpResponseMessage response = await ReplayAsync(s_client, entry);
            string actualText = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(entry.GetProperty("status").GetInt32(), (int)response.StatusCode);
            AssertResponseHeaders(entry.GetProperty("responseHeaders"), response);

            // The faultstring wording depends on runtime and culture, so the comparison checks code and structure (baseline README section 5).
            XElement expectedFault = GetFault(XElement.Parse(Encoding.UTF8.GetString(golden)));
            XElement actualFault = GetFault(XElement.Parse(actualText));

            CollectionAssert.AreEqual(
                expectedFault.Elements().Select(e => e.Name.ToString()).ToArray(),
                actualFault.Elements().Select(e => e.Name.ToString()).ToArray(),
                "Fault children (faultcode, faultstring; no detail because includeExceptionDetailInFaults=false)");

            XElement expectedCode = expectedFault.Element("faultcode");
            XElement actualCode = actualFault.Element("faultcode");
            Assert.AreEqual(expectedCode.Value, actualCode.Value);
            Assert.AreEqual(expectedCode.GetNamespaceOfPrefix("a"), actualCode.GetNamespaceOfPrefix("a"));

            XElement expectedReason = expectedFault.Element("faultstring");
            XElement actualReason = actualFault.Element("faultstring");
            Assert.AreEqual(expectedReason.Attribute(XNamespace.Xml + "lang")?.Value, actualReason.Attribute(XNamespace.Xml + "lang")?.Value);
            Assert.IsFalse(string.IsNullOrWhiteSpace(actualReason.Value));
        }

        [TestMethod]
        public async Task Metadata_Xsd_IsByteIdenticalToGoldenBaseline()
        {
            JsonElement entry = GetMetadataEntry("metadata/xsd-xsd0.xsd");
            byte[] golden = ReadGolden(entry.GetProperty("file").GetString(), entry.GetProperty("sha256").GetString());

            using HttpResponseMessage response = await s_client.GetAsync(ServicePath + "?xsd=xsd0");
            byte[] actual = await response.Content.ReadAsByteArrayAsync();

            Assert.AreEqual(entry.GetProperty("status").GetInt32(), (int)response.StatusCode);
            AssertResponseHeaders(entry.GetProperty("headers"), response);
            Assert.AreEqual(Encoding.UTF8.GetString(golden), Encoding.UTF8.GetString(actual));
            CollectionAssert.AreEqual(golden, actual);
        }

        /// <summary>
        /// The WSDL documents must match the golden baseline item by item: elements, attributes and
        /// their order, prefixes, values and text, after substituting the test host's address for the
        /// original <c>http://localhost:50355</c>. The only difference allowed is the order of
        /// <c>xmlns:*</c> declarations on an element. scenario-instructions.md (Decisions) accepts it
        /// as non-semantic: the .NET Framework order came from Hashtable ordering.
        /// </summary>
        [TestMethod]
        [DataRow("metadata/wsdl.xml", "?wsdl")]
        [DataRow("metadata/wsdl-wsdl0.xml", "?wsdl=wsdl0")]
        [DataRow("metadata/singleWsdl.xml", "?singleWsdl")]
        public async Task Metadata_Wsdl_MatchesGoldenBaselineInfoset(string goldenFile, string query)
        {
            JsonElement entry = GetMetadataEntry(goldenFile);
            byte[] golden = ReadGolden(entry.GetProperty("file").GetString(), entry.GetProperty("sha256").GetString());
            string expected = Encoding.UTF8.GetString(golden).Replace(GoldenAuthority, s_baseAddress, StringComparison.Ordinal);

            using HttpResponseMessage response = await s_client.GetAsync(ServicePath + query);
            string actual = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(entry.GetProperty("status").GetInt32(), (int)response.StatusCode);
            AssertResponseHeaders(entry.GetProperty("headers"), response);
            Assert.Contains(s_baseAddress + ServicePath, actual, "Metadata addresses must point at the CoreWCF endpoint");

            AssertSameInfoset(expected, actual, query);

        }

        [TestMethod]
        public async Task ServiceHelpPage_MatchesGoldenBaseline()
        {
            JsonElement entry = GetMetadataEntry("metadata/service-help-page.html");
            byte[] golden = ReadGolden(entry.GetProperty("file").GetString(), entry.GetProperty("sha256").GetString());
            string expected = Encoding.UTF8.GetString(golden).Replace(GoldenAuthority, s_baseAddress, StringComparison.Ordinal);

            using HttpResponseMessage response = await s_client.GetAsync(ServicePath);
            string actual = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(entry.GetProperty("status").GetInt32(), (int)response.StatusCode);
            AssertResponseHeaders(entry.GetProperty("headers"), response);
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// WCF-006: the reply Action <c>"ConvertToOffset"</c> is hard-coded in the custom dispatch
        /// formatter and must be kept as-is. It never reaches the wire on BasicHttpBinding
        /// (AddressingNone), so it is checked at the formatter.
        /// </summary>
        [TestMethod]
        public void ReplyAction_IsPreservedByResponseDispatchFormatter()
        {
            var formatter = new ResponseDispatchFormatter(new UnusedRequestFormatter());
            var result = new ConvertToOffsetResponse(new TimeZoneConversionResponse { ToDateTimeOffset = "2022-09-24T09:23:45.6780000+01:00" });

            Message reply = formatter.SerializeReply(MessageVersion.Soap11, Array.Empty<object>(), result);
            try
            {
                Assert.AreEqual("ConvertToOffset", reply.Headers.Action);
                Assert.AreEqual(MessageVersion.Soap11, reply.Version);
            }
            finally
            {
                reply.Close();
            }
        }

        /// <summary>
        /// Code review finding 3: metadata addresses follow the request's Host header
        /// (UseRequestHeadersForMetadataAddressBehavior), not the listen address. The test host is bound
        /// to 127.0.0.1 on a dynamic port; a request carrying the original host's authority
        /// (<c>localhost:50355</c>) must get the golden WSDL with no address substitution at all.
        /// </summary>
        [TestMethod]
        [DataRow("metadata/wsdl.xml", "?wsdl")]
        [DataRow("metadata/wsdl-wsdl0.xml", "?wsdl=wsdl0")]
        [DataRow("metadata/singleWsdl.xml", "?singleWsdl")]
        public async Task Metadata_Wsdl_AddressesFollowRequestHostHeader(string goldenFile, string query)
        {
            JsonElement entry = GetMetadataEntry(goldenFile);
            string expected = Encoding.UTF8.GetString(ReadGolden(entry.GetProperty("file").GetString(), entry.GetProperty("sha256").GetString()));
            Assert.AreNotEqual(new Uri(GoldenAuthority).Authority, new Uri(s_baseAddress).Authority, "Test host must listen on a different authority than the Host header sent");

            using var request = new HttpRequestMessage(HttpMethod.Get, ServicePath + query);
            request.Headers.Host = new Uri(GoldenAuthority).Authority;
            using HttpResponseMessage response = await s_client.SendAsync(request);
            string actual = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(entry.GetProperty("status").GetInt32(), (int)response.StatusCode);
            Assert.DoesNotContain(s_baseAddress, actual, "Metadata must not expose the listen address");
            Assert.Contains(GoldenAuthority + ServicePath, actual);
            AssertSameInfoset(expected, actual, query);
        }

        /// <summary>
        /// Code review finding 1: <c>https_port</c> (ASPNETCORE_HTTPS_PORT) is only an HTTPS redirect hint.
        /// With it set and an HTTP-only listener, the host must start, register E1 only, serve it
        /// byte-identical to the golden baseline, and warn that E2 was not registered.
        /// </summary>
        [TestMethod]
        public async Task HttpsPortWithoutHttpsListener_HostStartsAndServesE1Only()
        {
            var logs = new CapturingLoggerProvider();
            using var factory = new PinnedHostFactory(logs, ("https_port", "44300"));
            StartLoopbackHost(factory, out string baseAddress);
            using var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

            JsonElement entry = GetEnvelopeEntry("ConvertToLowerOffset");
            byte[] golden = ReadGolden(entry.GetProperty("responseFile").GetString(), entry.GetProperty("responseSha256").GetString());
            using HttpResponseMessage response = await ReplayAsync(client, entry);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            CollectionAssert.AreEqual(golden, await response.Content.ReadAsByteArrayAsync());

            await AssertSingleHttpPortAsync(client, baseAddress);
            Assert.IsTrue(
                logs.Messages.Any(m => m.Level == LogLevel.Warning && m.Text.Contains("TimeZoneConverter:EnableHttpsEndpoint=true", StringComparison.Ordinal)),
                "Expected a warning naming the EnableHttpsEndpoint switch");
        }

        /// <summary>
        /// Code review finding 2: <c>TimeZoneConverter:EnableHttpsEndpoint=false</c> wins over a configured
        /// HTTPS URL (E2 not registered, host starts with E1 only).
        /// </summary>
        [TestMethod]
        public async Task EnableHttpsEndpointFalse_HttpsUrlConfigured_E2NotRegistered()
        {
            using var factory = new PinnedHostFactory(null, ("urls", "https://127.0.0.1:0"), (EnableHttpsEndpointKey, "false"));
            StartLoopbackHost(factory, out string baseAddress);
            using var client = new HttpClient { BaseAddress = new Uri(baseAddress) };

            await AssertSingleHttpPortAsync(client, baseAddress);
        }

        /// <summary>
        /// Code review finding 2: an explicit <c>true</c> registers E2 regardless of what configuration shows
        /// (the IIS case). Without an HTTPS address from the server that is a deployment error, and startup
        /// fails fast instead of running without the HTTPS endpoint the deployer asked for.
        /// </summary>
        [TestMethod]
        public void EnableHttpsEndpointTrue_NoHttpsAddress_StartupFailsFast()
        {
            using var factory = new PinnedHostFactory(null, (EnableHttpsEndpointKey, "true"));
            Exception error = Assert.Throws<Exception>(() => StartLoopbackHost(factory, out _), "Startup must fail when E2 is enabled but the server has no HTTPS address");

            string messages = string.Join(" | ", Flatten(error).Select(e => e.Message));
            Assert.Contains("TimeZoneConverter:EnableHttpsEndpoint", messages, messages);
            Assert.Contains("no https address", messages, StringComparison.OrdinalIgnoreCase, messages);
        }

        [TestMethod]
        public void EnableHttpsEndpointInvalidValue_StartupFailsWithClearMessage()
        {
            using var factory = new PinnedHostFactory(null, (EnableHttpsEndpointKey, "yes"));
            Exception error = Assert.Throws<Exception>(() => StartLoopbackHost(factory, out _));

            string messages = string.Join(" | ", Flatten(error).Select(e => e.Message));
            Assert.Contains("Invalid value 'yes' for TimeZoneConverter:EnableHttpsEndpoint", messages, messages);
        }

        private const string EnableHttpsEndpointKey = "TimeZoneConverter:EnableHttpsEndpoint";

        /// <summary>Starts the factory on real Kestrel bound to 127.0.0.1 with a dynamic port.</summary>
        private static WebApplicationFactory<Program> StartLoopbackHost(WebApplicationFactory<Program> factory, out string baseAddress)
        {
            factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            factory.StartServer();

            var addresses = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses;
            baseAddress = addresses.Single().TrimEnd('/');
            return factory;
        }

        private static async Task AssertSingleHttpPortAsync(HttpClient client, string baseAddress)
        {
            string wsdl = await client.GetStringAsync(ServicePath + "?wsdl");
            XNamespace soap = "http://schemas.xmlsoap.org/wsdl/soap/";
            string[] addresses = XElement.Parse(wsdl).Descendants(soap + "address").Select(a => (string)a.Attribute("location")).ToArray();
            CollectionAssert.AreEqual(new[] { baseAddress + ServicePath }, addresses, "Only E1 (http) may be exposed");
        }

        private static IEnumerable<Exception> Flatten(Exception error)
        {
            for (Exception current = error; current != null; current = current.InnerException)
            {
                yield return current;
                if (current is AggregateException aggregate)
                {
                    foreach (Exception inner in aggregate.InnerExceptions.SelectMany(Flatten))
                    {
                        yield return inner;
                    }

                    yield break;
                }
            }
        }

        private static void AssertSameInfoset(string expected, string actual, string label)
        {
            List<string> expectedTokens = Tokenize(expected);
            List<string> actualTokens = Tokenize(actual);
            for (int i = 0; i < Math.Min(expectedTokens.Count, actualTokens.Count); i++)
            {
                Assert.AreEqual(expectedTokens[i], actualTokens[i], $"{label}: first difference at XML node #{i}");
            }

            Assert.HasCount(expectedTokens.Count, actualTokens, $"{label}: XML node count");
        }

        private static async Task<HttpResponseMessage> ReplayAsync(HttpClient client, JsonElement entry)
        {
            byte[] requestBody = ReadGolden(entry.GetProperty("requestFile").GetString(), entry.GetProperty("requestSha256").GetString());
            JsonElement requestHeaders = entry.GetProperty("requestHeaders");

            var content = new ByteArrayContent(requestBody);
            Assert.IsTrue(content.Headers.TryAddWithoutValidation("Content-Type", requestHeaders.GetProperty("Content-Type").GetString()));

            using var request = new HttpRequestMessage(HttpMethod.Post, ServicePath) { Content = content };
            Assert.IsTrue(request.Headers.TryAddWithoutValidation("SOAPAction", requestHeaders.GetProperty("SOAPAction").GetString()));

            return await s_client.SendAsync(request);
        }

        private static void AssertResponseHeaders(JsonElement goldenHeaders, HttpResponseMessage response)
        {
            Assert.AreEqual(goldenHeaders.GetProperty("Content-Type").GetString(), response.Content.Headers.ContentType?.ToString(), "Content-Type");
            Assert.AreEqual(goldenHeaders.GetProperty("X-Frame-Options").GetString(), string.Join(",", response.Headers.GetValues("X-Frame-Options")), "X-Frame-Options (former Web.config customHeaders)");
        }

        private static XElement GetFault(XElement envelope)
        {
            XElement body = envelope.Elements().Single();
            Assert.AreEqual(XName.Get("Body", SoapEnvelopeNamespace), body.Name);
            XElement fault = body.Elements().Single();
            Assert.AreEqual(XName.Get("Fault", SoapEnvelopeNamespace), fault.Name);
            return fault;
        }

        private static JsonElement GetEnvelopeEntry(string name) =>
            s_manifest.GetProperty("envelopes").EnumerateArray().Single(e => e.GetProperty("name").GetString() == name);

        private static JsonElement GetMetadataEntry(string file) =>
            s_manifest.GetProperty("metadata").EnumerateArray().Single(e => e.GetProperty("file").GetString() == file);

        private static byte[] ReadGolden(string relativePath, string expectedSha256)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(s_baselineRoot, relativePath));
            Assert.AreEqual(expectedSha256, Convert.ToHexStringLower(SHA256.HashData(bytes)), $"Golden file {relativePath} does not match capture-manifest.json");
            return bytes;
        }

        /// <summary>
        /// Flattens an XML document into one token per node. Element tokens include the literal
        /// prefix, namespace URI, non-namespace attributes in document order, and the element's
        /// namespace declarations sorted by prefix. Sorting the declarations is the only normalization.
        /// </summary>
        private static List<string> Tokenize(string xml)
        {
            var tokens = new List<string>();
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        var token = new StringBuilder($"<{reader.Name} {{{reader.NamespaceURI}}}");
                        var declarations = new List<string>();
                        bool isEmpty = reader.IsEmptyElement;
                        if (reader.MoveToFirstAttribute())
                        {
                            do
                            {
                                if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/")
                                {
                                    declarations.Add($"{reader.Name}={reader.Value}");
                                }
                                else
                                {
                                    token.Append($" @{reader.Name} {{{reader.NamespaceURI}}}=\"{reader.Value}\"");
                                }
                            }
                            while (reader.MoveToNextAttribute());
                            reader.MoveToElement();
                        }

                        declarations.Sort(StringComparer.Ordinal);
                        token.Append(" ns[").Append(string.Join(" ", declarations)).Append(']');
                        tokens.Add(token.ToString());
                        if (isEmpty)
                        {
                            tokens.Add($"</{reader.Name}>");
                        }

                        break;
                    case XmlNodeType.EndElement:
                        tokens.Add($"</{reader.Name}>");
                        break;
                    case XmlNodeType.XmlDeclaration:
                        tokens.Add($"<?xml {reader.Value}?>");
                        break;
                    default:
                        tokens.Add($"{reader.NodeType}:{reader.Value}");
                        break;
                }
            }

            return tokens;
        }

        private sealed class UnusedRequestFormatter : IDispatchMessageFormatter
        {
            public void DeserializeRequest(Message message, object[] parameters) => throw new NotSupportedException();

            public Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result) => throw new NotSupportedException();
        }

        /// <summary>
        /// Test host whose endpoint-selection settings are pinned, so machine-level ASPNETCORE_URLS,
        /// ASPNETCORE_HTTPS_PORTS, ASPNETCORE_HTTPS_PORT or TimeZoneConverter__EnableHttpsEndpoint cannot
        /// change which endpoints the host registers. Kestrel is bound in code (<see cref="StartLoopbackHost"/>).
        /// </summary>
        private sealed class PinnedHostFactory : WebApplicationFactory<Program>
        {
            private static readonly (string Key, string Value)[] s_pinned =
            {
                ("urls", string.Empty),
                ("https_ports", string.Empty),
                ("https_port", string.Empty),
                (EnableHttpsEndpointKey, "auto"),
            };

            private readonly ILoggerProvider _logs;
            private readonly (string Key, string Value)[] _overrides;

            public PinnedHostFactory(ILoggerProvider logs = null, params (string Key, string Value)[] overrides)
            {
                _logs = logs;
                _overrides = overrides;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                foreach ((string key, string value) in s_pinned.Concat(_overrides))
                {
                    builder.UseSetting(key, value);
                }

                if (_logs != null)
                {
                    builder.ConfigureLogging(logging => logging.AddProvider(_logs));
                }
            }
        }

        private sealed class CapturingLoggerProvider : ILoggerProvider
        {
            private readonly ConcurrentQueue<(LogLevel Level, string Text)> _messages = new();

            public IReadOnlyCollection<(LogLevel Level, string Text)> Messages => _messages;

            public ILogger CreateLogger(string categoryName) => new CapturingLogger(_messages);

            public void Dispose()
            {
            }

            private sealed class CapturingLogger(ConcurrentQueue<(LogLevel Level, string Text)> messages) : ILogger
            {
                public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

                public bool IsEnabled(LogLevel logLevel) => true;

                public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
                    messages.Enqueue((logLevel, formatter(state, exception)));
            }
        }
    }
}
