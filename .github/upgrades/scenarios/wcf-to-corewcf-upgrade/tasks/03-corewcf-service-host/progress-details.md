# Progress — 03-corewcf-service-host

## Result (2026-10-01)

**Outcome: done.** `src48/TimeZoneConverter` is rehosted in place on ASP.NET Core + CoreWCF 1.9.1, `net10.0`, `Microsoft.NET.Sdk.Web`. Host build: **0 warnings, 0 errors**. E1 SOAP envelopes (4 SoapUI samples + fault probe) are **byte-identical** to the golden baseline. `?xsd=xsd0` and the help page are byte-identical. `?wsdl`, `?wsdl=wsdl0` and `?singleWsdl` have an **identical XML infoset** (elements, attributes, text, prefixes and namespace-declaration set); only the *order* of `xmlns:*` declarations on the root differs (see "Metadata note"). E2 is wired and shows up correctly in the WSDL, but the HTTPS wire exchange is **UNVERIFIED** (see below).

Decomposition verdict: **atomic**. I evaluated scenario `execution.md` (small-cohesive-wcf exception) and the `migrating-wcf-to-corewcf` decomposition section. There is one host, one service and one operation. The format-only SDK checkpoint is not applicable because task 02 folded the host's format change into this task. No scenario extensions apply at Execution scope.

## Files modified (src48 only; `src60/` not read or touched)
| File | Change |
|---|---|
| `src48/TimeZoneConverter/TimeZoneConverter.csproj` | Rewritten: `Microsoft.NET.Sdk.Web`, `net10.0`, `OutputType Exe`, `AssemblyName`/`RootNamespace` `TimeZoneConvertor`, `LangVersion latest`, `GenerateAssemblyInfo false` (keeps `Properties/AssemblyInfo.cs`), `ImplicitUsings`/`Nullable` disabled, PackageReference `CoreWCF.Primitives` 1.9.1 + `CoreWCF.Http` 1.9.1. Removed: 18 GAC refs (System.ServiceModel*, System.Web*, …), ProjectTypeGuids, IIS Express props, WebApplication.targets, `WcfConfigValidationEnabled`, `.svc` Content / `DependentUpon` items. |
| `src48/TimeZoneConverter/Program.cs` | **New.** `AddServiceModelServices` + `AddServiceModelMetadata`; X-Frame-Options SAMEORIGIN middleware; `AddService<TimeZoneConverterService>` with `DebugBehavior.IncludeExceptionDetailInFaults=false`; E1 `BasicHttpBinding()` at `/TimeZoneConverter.svc`; E2 `BasicHttpBinding(Transport){Name="BasicHttpsBinding"}` only when an HTTPS binding is configured (`urls`, `https_ports`, `https_port` (ANCM/IIS), `Kestrel:Endpoints:*:Url`); `ServiceMetadataBehavior` HttpGet+HttpsGet = true. |
| `src48/TimeZoneConverter/Properties/launchSettings.json` | **New.** Profile `TimeZoneConverter` (Kestrel, `http://localhost:50355`) + `IIS Express` profile (`http://localhost:50355`, sslPort 0, anonymous). |
| `src48/TimeZoneConverter/appsettings.json` | **New.** Standard logging levels only. |
| `src48/TimeZoneConverter/Contracts/ITimeZoneConverter.cs` | Now contains only the server contract, with `CoreWCF.*` attributes and identical Namespace/ConfigurationName/Action/ReplyAction/XmlSerializerFormat. The header says the file is hand-maintained and SvcUtil regeneration no longer applies. Removed: unused `TimeZoneConverterClient`, `ITimeZoneConverterChannel` (WCF-007, D-4). |
| `src48/TimeZoneConverter/Contracts/TimeZoneConverterMessages.cs` | **New** (split out). `TimeZoneConversionRequest`, generated part of `TimeZoneConversionResponse`, `ConvertToOffsetRequest`/`Response` message contracts. XmlType/XmlElement Order and WrapperName/WrapperNamespace/IsWrapped/MessageBodyMember Namespace+Order are identical; only `System.ServiceModel.*` → `CoreWCF.*` and the `GeneratedCode` attribute was dropped. Namespaces and type names are unchanged, so the test project only needs a retarget. |
| `ResponseSerializerAttribute.cs`, `RequestResponseSerializerAttribute.cs` | usings → `CoreWCF.Channels/Description/Dispatcher`. `operationDescription.Behaviors.Find<XmlSerializerOperationBehavior>()` → `operationDescription.OperationBehaviors.OfType<XmlSerializerOperationBehavior>().FirstOrDefault()`. CoreWCF 1.9.1 keeps `Behaviors` internal and exposes `OperationBehaviors` (KeyedCollection) instead. Same semantics: first assignable item. |
| `ResponseDispatchFormatter.cs`, `RequestResponseDispatchFormatter.cs` | usings → `CoreWCF.Channels/Dispatcher` only. The hard-coded reply Action `"ConvertToOffset"` is unchanged (WCF-006). |
| `Serializers/ConvertToOffsetResponseSerializer.cs` | Swapped `XmlSerializerNamespaces.Add` order to xsd then xsi, with a comment explaining why. **Wire-parity fix, not a behavior change:** .NET Framework emitted `xmlns:xsd` before `xmlns:xsi` (Hashtable order), while .NET keeps insertion order. Without the swap, the response emitted `xmlns:xsi` first (evidence: `runtime-capture-e1-prefix/`). `ConversionReponseSerializerTest` asserts the same order (relevant for task 04). |
| Deleted: `TimeZoneConverter.svc` | WCF-002. CoreWCF has no .svc activation. `TimeZoneConverter.svc.cs` (implementation) is kept unchanged. |
| Deleted: `Web.config`, `Web.Debug.config`, `Web.Release.config` | Every setting is mapped below. Sdk.Web publish generates the ANCM `web.config` for IIS. |
| Deleted: `GenerateContracts.ps1` | It runs .NET Framework SvcUtil 4.8 and would overwrite the hand-maintained CoreWCF contract with `System.ServiceModel` client code. The original generated file is preserved at `wcf-baseline/contracts-original/ITimeZoneConverter.cs` and in git history. |
| Workflow: `task.md` (research section added before the first edit), `scenario-instructions.md` (host Build Tool Decision → `dotnet build`), this file, `Compare-WcfWireCapture.ps1` (reusable semantic comparator), `runtime-capture-e1/`, `runtime-capture-e1-prefix/`, `runtime-capture-e1e2-http/` (evidence) | |

Deletions are unstaged working-tree deletions, and nothing is committed (the orchestrator owns commits). `bin/`/`obj/` are gitignored.

## Web.config → destination (every effective setting)
| Setting | Destination / disposition |
|---|---|
| `appSettings aspnet:UseTaskFriendlySynchronizationContext=true` | Removed. ASP.NET (System.Web) only; ASP.NET Core has no SynchronizationContext. |
| `system.web compilation debug` / `targetFramework`, `httpRuntime targetFramework`, Debug/Release transforms | Removed. Not applicable to a precompiled ASP.NET Core app; transforms deleted. |
| `bindings/basicHttpBinding BasicHttpBinding_ITimeZoneConverter` + `<client>` endpoint | Removed. Dead client config: the contract name didn't resolve and the proxy was unused (WCF-007, D-4). |
| `serviceMetadata httpGetEnabled=true httpsGetEnabled=true` | `Program.cs` `ServiceMetadataBehavior` (WCF-004). |
| `serviceDebug includeExceptionDetailInFaults=false` | `Program.cs` `ServiceOptions.DebugBehavior.IncludeExceptionDetailInFaults=false` (WCF-004). Verified: the fault carries no `<detail>` and is byte-identical to golden. |
| `protocolMapping https → basicHttpsBinding` | `Program.cs` E2, added only when an HTTPS binding exists (WCF-003). |
| `serviceHostingEnvironment aspNetCompatibilityEnabled=true` | Removed (WCF-001). No HttpContext/System.Web/OperationContext use. |
| `serviceHostingEnvironment multipleSiteBindingsEnabled=true` | Host URLs (launchSettings / `--urls` / Kestrel config / IIS bindings). CoreWCF derives base addresses from server addresses; verified with two bindings (`runtime-capture-e1e2-http`). |
| `system.webServer modules runAllManagedModulesForAllRequests=true` | Removed. IIS managed-module setting, not applicable to ASP.NET Core. |
| `httpProtocol customHeaders X-Frame-Options: SAMEORIGIN` | `Program.cs` middleware. Verified on every response (SOAP 200/500, WSDL, XSD, help page). |
| `directoryBrowse enabled=false` (Debug transform: true) | Preserved. ASP.NET Core has no directory browsing because `UseDirectoryBrowser` is not used. The Debug-only `true` was dev convenience and isn't carried over. |
| IIS Express `IISUrl http://localhost:50355/`, `IISExpressSSLPort` empty | `Properties/launchSettings.json` (`http://localhost:50355`, sslPort 0). |
| `.svc` `Debug="true"` | Removed. Not applicable. |

## Build
- `dotnet build src48\TimeZoneConverter\TimeZoneConverter.csproj -c Debug --no-incremental` (SDK 10.0.401) → **0 Warning(s), 0 Error(s)** → `bin\Debug\net10.0\TimeZoneConvertor.dll`.
- Restore went through `packagefeedproxy.microsoft.io` (CoreWCF 1.9.1 resolved). Restore logs are empty (no NU1608/downgrade). `dotnet list package --vulnerable --include-transitive`: none. Transitive `System.Web.Services.Description` 8.1.2 is CoreWCF's WSDL object-model package, not System.Web.
- Package graph has no `System.ServiceModel.*` or `System.Web` packages or references.
- `dotnet build src48\TimeZoneConverter.sln` → **NU1201** on TimeZoneConverterTest (net481 can't reference the net10.0 host). This is **expected and accepted per plan until task 04**. The host itself builds clean in the solution build. CodeQL autobuild on the solution will need task 04 to land.

## Runtime parity (Strict XML parity vs `wcf-baseline/`)
Host: `dotnet run --no-build --launch-profile TimeZoneConverter` → Kestrel `http://localhost:50355`, listening on 127.0.0.1 and ::1 only (loopback). Capture: `wcf-baseline/Capture-WcfWireBaseline.ps1` (unchanged), with output under this task folder. Compare: `Compare-WcfWireCapture.ps1 -GoldenRoot <wcf-baseline> -CaptureRoot <capture>`.

Final capture `runtime-capture-e1/` (E1 only, same config as the original dev host):
| Artifact | Result |
|---|---|
| ConvertToLowerOffset / HigherOffset / SameOffset / NegativeOffset response | **BYTES-EQUAL** (397 B each). HTTP 200, `text/xml; charset=utf-8`, no `<s:Header>`, `xmlns:xsd` before `xmlns:xsi`, `<ToDateTime>` in `o` format with offset (e.g. `2022-09-24T02:23:45.6780000-06:00`). |
| FaultProbe-UnknownAction response | **BYTES-EQUAL** (762 B). HTTP 500, `a:ActionNotSupported`, `xml:lang="en-US"`, no detail. |
| Request files (4 + probe) | BYTES-EQUAL (same SoapUI-derived payloads; sanity check). |
| `?xsd=xsd0` | **BYTES-EQUAL** |
| GET `/TimeZoneConverter.svc` help page (supplementary) | **BYTES-EQUAL** |
| `?wsdl`, `?wsdl=wsdl0`, `?singleWsdl` | **NSDECL-ORDER**: same elements, attributes, text, prefixes, `soap:address http://localhost:50355/TimeZoneConverter.svc`, targetNamespaces, import locations, operation/soapAction/wsaw:Action, messages and schema types, and the same *set* of root `xmlns:*` declarations. Only the declaration order differs. Content-Type `text/xml; charset=UTF-8` (golden is the same). |
| Response headers | `X-Frame-Options: SAMEORIGIN` present. Host-specific differences that are not SOAP parity: `Server: Kestrel` (was `Microsoft-IIS/10.0`); no `X-AspNet-Version` / `X-Powered-By` / `X-SourceFiles` / `Cache-Control: private`. |

- **WCF-005 (highest risk) verified:** CoreWCF applies the `[ResponseSerializer]` `IOperationBehavior` attribute placed on the implementation method, and `XmlSerializerOperationBehavior` / `DispatchOperation.Formatter` behave as on WCF. The custom serializer output (`<ToDateTime>` with offset, no `ToDateTimeOffset` element) is on the wire, so no `IServiceBehavior` fallback was needed.
- **WCF-006:** the reply Action `"ConvertToOffset"` is preserved in code (`ResponseDispatchFormatter.cs`). On the wire BasicHttpBinding/AddressingNone emits no Action, which matches golden byte-for-byte. The request Action `…/ConvertToOffsetRequest` dispatches and an unknown action faults the same way. Nothing was "fixed".

**Metadata note (for orchestrator / owner acceptance):** the golden xmlns declaration order is a .NET Framework `Hashtable` artifact. It even differs between golden `wsdl.xml` (`wsdl,wsx,i0,wsu,…`) and `singleWsdl.xml` (`wsdl,wsam,wsx,…`) for the same prefixes. CoreWCF's WSDL writer emits insertion order (`wsdl,xsd,wsp,wsu,…`). XML namespace-declaration order has no meaning in the XML infoset, WSDL or XSD, and every compared semantic item is identical. Reproducing it would mean rewriting the metadata response bytes in middleware, which I did not do. Recorded as an **infoset-identical difference to be accepted** rather than a byte match.

E2 wiring capture `runtime-capture-e1e2-http/` (`--urls "http://localhost:50355;https://localhost:44355"`, Kestrel used the **existing** untrusted ASP.NET Core dev cert; no certificate was created and no trust change was made; all requests went over HTTP):
- E1 envelopes, fault, xsd and help page: still BYTES-EQUAL. `wsdl-wsdl0`: NSDECL-ORDER.
- `?wsdl` / `?singleWsdl`: show the expected *addition* versus golden (golden had no https binding). Binding `BasicHttpsBinding_ITimeZoneConverter` + `wsp:Policy` `sp:TransportBinding/HttpsToken RequireClientCertificate="false"` + port `BasicHttpsBinding_ITimeZoneConverter` at `https://localhost:44355/TimeZoneConverter.svc`. This is the WCF basicHttpsBinding default-endpoint shape (Transport, client credential None). E2 registration is conditional and verified for both states (absent without https, present with https).

## UNVERIFIED (prerequisite named)
- **E2 wire exchange over HTTPS** (POST envelopes and `?wsdl` over https). Missing prerequisite: a **trusted** HTTPS certificate or production binding. The dev cert exists but is untrusted (`dotnet dev-certs https --check --trust` exit 7 in task 01). Trust-store changes and TLS-validation bypass are prohibited. There is also no golden E2 baseline, because the original host had no https binding (task 01).
- **M1 metadata over HTTPS (HttpsGetEnabled)**: same prerequisite.
- **Real SoapUI run** against the migrated host: SoapUI not available. The SoapUI payloads were replayed byte-for-byte by script instead.
- **IIS Express launch profile** and **IIS in-process hosting**: not run. The Kestrel profile was run. Deployment host/OS is an owner input, and `dotnet publish` is in task 05.

## Processes owned at return
I started three host processes, each via `dotnet run` in an attached shell: PID 48100, 43612 and 12644 (`…\bin\Debug\net10.0\TimeZoneConvertor.exe`). **All three were stopped** with `stop_powershell` on their own shell IDs. Afterwards: none of the PIDs is alive, there are no listeners on 50355/44355, and no process runs from this worktree. No IIS Express was started. No certificates, trust stores or URL reservations were touched.
