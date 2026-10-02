# 04-test-project-net10: Retarget TimeZoneConverterTest to net10.0 and add endpoint wire tests

Retarget `src48/TimeZoneConverterTest` to `net10.0` against the migrated host. Move MSTest 2.2.7 to the `MSTest` 4.4.1 metapackage and update namespaces or references if task 03 changed the contract or serializer types (`TimeZoneConvertor.Serializers.ConvertToOffsetResponseSerializer`, `TimeZoneConverter.Contracts.*`). Keep both existing tests unchanged in intent. `ConversionReponseSerializerTest` checks an exact XML string, including the order of `xmlns:xsd`/`xmlns:xsi` (WCF-009). #skill:migrating-wcf-to-corewcf

Add the endpoint-level tests from WCF-010. These are loopback tests through an in-process host (e.g. `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 `WebApplicationFactory`, or in-process Kestrel) that replay the SoapUI raw-XML requests and check the response Action, namespaces, element order and `ToDateTime` value against the golden baseline from task 01. They become the repeatable Strict XML parity check. Any WCF-client test uses `System.ServiceModel.*` 10.0.0 in the client role only. The host may need a `public partial class Program` or `InternalsVisibleTo` so the test host can find its entry point.

**Done when**: The full `src48/TimeZoneConverter.sln` builds on net10.0. Both original unit tests and the new endpoint wire tests pass, with the parity assertions tied to the recorded baseline (or to the labeled offline baseline if the original host could not be captured).


---

## Research findings (executor, 2026-10-01, before the first source edit)

**Decomposition verdict: atomic.** Evaluated: scenario `execution.md` (rule small-cohesive-wcf: one test project with a known file list; no open design or security decision; the SDK checkpoint was already done in task 02) and the `migrating-wcf-to-corewcf` decomposition section (it has no split triggers for this task). The scenario has no separate Breakdown Hints files. No scenario extensions apply at the Execution scope.

### Confirmed current state (repo, HEAD 15fb33f)
- `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj`: SDK-style (task 02), `net481`, `LangVersion latest`, `GenerateAssemblyInfo false`, `Reference System.Xml.Serialization` (GAC, must go on net10.0), `ProjectReference ..\TimeZoneConverter\TimeZoneConverter.csproj`, `MSTest.TestAdapter` 2.2.7 + `MSTest.TestFramework` 2.2.7, no `Microsoft.NET.Test.Sdk`.
- Files: `ConversionReponseSerializerTest.cs` (2 tests: `WriteObject`, `ReadObject`) and `Properties/AssemblyInfo.cs`. There are no `ServiceHost` self-host fixtures to replace.
- Host (task 03): `Microsoft.NET.Sdk.Web` net10.0, AssemblyName `TimeZoneConvertor`, top-level `Program.cs` (global namespace, no explicit `public partial class Program`). The .NET 10 web SDK source generator is expected to emit a public `Program`; this is verified at build. The host reads `urls`/`https_ports`/`Kestrel:Endpoints` to decide whether to register E2. With the test host using an http-only loopback, only E1 is registered, which matches the golden baseline.
- Namespaces and types the tests use are unchanged by task 03: `TimeZoneConverter.Contracts.{ConvertToOffsetResponse,TimeZoneConversionResponse}`, `TimeZoneConvertor.Serializers.ConvertToOffsetResponseSerializer` (xsd-before-xsi fix already applied in task 03). The existing test sources need no code change.
- There is no `Directory.Build.props`, `Directory.Packages.props`, `global.json` or `nuget.config` in the repo, so the TFM is set in the csproj itself. NuGet restore goes through the `azure-default` proxy feed.
- `.gitattributes` = `* text=auto` and `core.autocrlf=true`. 3 of the 4 golden request files are CRLF in the worktree (LF in the index), and `ConvertToHigherOffset.request.xml` is LF. The manifest SHA256 values match the **worktree** bytes. So the copied test resources need `-text` to stay byte-exact.

### Golden baseline used (task 01, LIVE GOLDEN net481/IIS Express): `wcf-baseline/`
- `envelopes/*.request.xml` / `*.response.xml`: 4 SoapUI samples + `FaultProbe-UnknownAction`. Success responses are 397 B, single line, no XML declaration, no `<s:Header>`. The manifest gives request headers `Content-Type: text/xml;charset=UTF-8` and `SOAPAction: "http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest"`.
- `metadata/`: `wsdl.xml`, `wsdl-wsdl0.xml`, `xsd-xsd0.xsd`, `singleWsdl.xml`, `service-help-page.html` (all with authority `http://localhost:50355`).
- `capture-manifest.json`: SHA256 of every artifact. This is the provenance link.

### Approach
1. csproj: `net10.0`, remove the GAC `System.Xml.Serialization` reference, and replace the MSTest 2.2.7 pair with `MSTest` 4.4.1 (metapackage: framework + adapter + Microsoft.NET.Test.Sdk + analyzers → `dotnet test`). Add `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 (versions from scenario-instructions Package Versions). Copy the golden files to `TimeZoneConverterTest/WireBaseline/` (byte-exact, plus `.gitattributes` `* -text` and a provenance README) and `CopyToOutputDirectory`.
2. Keep `ConversionReponseSerializerTest` unchanged. Add an assembly-level parallelization attribute only if the MSTest 4 analyzers require it.
3. New `EndpointWireTests` (WCF-010): `WebApplicationFactory<Program>` with **real Kestrel on 127.0.0.1, dynamic port** (`UseKestrel(0)`, .NET 10 API) instead of TestServer. Reasons: real HTTP loopback, CoreWCF derives base addresses from server addresses, and binding to the loopback IP gives containment. Tests:
   - Provenance: every copied golden file's SHA256 equals `capture-manifest.json`.
   - The 4 SoapUI replays (exact request bytes and headers): HTTP 200, `text/xml; charset=utf-8`, `X-Frame-Options: SAMEORIGIN`, **response bytes equal to golden**, plus explicit structural asserts (SOAP 1.1 `s` prefix, no `<s:Header>` = no reply Action on the wire per WCF-006 baseline note, body root QName, default ns, `xmlns:xsd` before `xmlns:xsi`, element order Response › TimeZoneConversionResponse › ToDateTime only, `ToDateTime` value from golden).
   - Fault probe: 500, `s:Fault`, faultcode `a:ActionNotSupported` with the golden addressing-none ns, `xml:lang`, no `<detail>`. The structure is compared rather than the wording, per baseline README §5.
   - Metadata: `?xsd=xsd0` byte-equal. `?wsdl`, `?wsdl=wsdl0` and `?singleWsdl` are compared as an infoset with namespace-declaration order ignored (accepted decision in scenario-instructions), and everything else strict (element/attribute names, prefixes, attribute order, values, text). The golden authority `http://localhost:50355` is substituted with the test host's base address, which is the only documented test-host normalization. The help page is byte-equal after the same substitution.
   - Reply Action code-level check (WCF-006): `ResponseDispatchFormatter.SerializeReply` produces Action `"ConvertToOffset"`. This is the only place it is observable, because BasicHttpBinding/AddressingNone emits no Action on the wire.
   - Request Action dispatch: the golden SOAPAction is accepted (covered by the replays) and an unknown action faults (fault probe).
4. No WCF-client (`System.ServiceModel.*`) test is added: the validation oracle is the SoapUI raw-XML client, and the task makes client tests optional.
5. Build: `dotnet build src48\TimeZoneConverter.sln` (0 warnings). Test: `dotnet test src48\TimeZoneConverterTest\TimeZoneConverterTest.csproj`. Then update Build Tool Decisions for the test project.

---

## Code-review fix pass — research (executor, 2026-10-01, before source edits)

Decomposition verdict for this pass: **atomic** (scenario `execution.md` rule small-cohesive-wcf: two known files in the host plus the test file, no open decision beyond the documented #2 choice). No scenario extensions apply at the Execution scope.

Findings and confirmed state:
1. **BLOCKING** `src48/TimeZoneConverter/Program.cs` `HasHttpsBinding` returns true for `https_port` (ASPNETCORE_HTTPS_PORT). That setting is a redirect-target hint, not a listener. With it set and only an HTTP listener, E2 gets registered and CoreWCF can't find an https base address. Fix: remove `https_port` as a signal and detect only real listeners (`urls`, `https_ports`, `Kestrel:Endpoints:*:Url`).
2. **WARNING** IIS in-process: the site's HTTPS binding doesn't show up in those settings (ANCM only sets `ASPNETCORE_HTTPS_PORT`). Fix: add an explicit switch `TimeZoneConverter:EnableHttpsEndpoint` (`auto` | `true` | `false`, default `auto` in appsettings.json).
   - `true`: always register E2. The server must provide an HTTPS address (IIS binding or Kestrel); otherwise CoreWCF fails startup fast.
   - `false`: never register E2.
   - `auto`: register E2 only when a real HTTPS listener is configured. If `https_port` is set but no HTTPS listener is detected (typical for IIS with an https binding), log a warning that tells the deployer to set the switch.
   - Any other value: fail fast with a clear message.
3. **WARNING** WSDL `soap:address` / `wsdl:import` are built from the listen address. CoreWCF 1.9.1 has the public `CoreWCF.Description.UseRequestHeadersForMetadataAddressBehavior` in CoreWCF.Primitives (confirmed by reflection on the build output). Register it as an `IServiceBehavior` singleton in DI; CoreWCF applies DI service behaviors to the service. Existing WSDL tests send Host = the bound `127.0.0.1:port`, so their substitution still holds. Add a test that sends `Host: localhost:50355` and compares to the golden **without** substitution.
4. **INFO** `EndpointWireTests` host: pin `urls`, `https_ports` and `https_port` to empty and set `TimeZoneConverter:EnableHttpsEndpoint=auto` via `UseSetting`, so machine environment variables can't change which endpoints are registered. Add a test for finding 1: a host with `https_port=44300` and an HTTP-only loopback listener starts, serves E1 byte-identical to the golden, and its WSDL has a single (http) port. Also add tests for the explicit switch (`false` with an https URL configured → no E2; `true` with no HTTPS listener → startup fails, error mentions https), plus a test for finding 3.
