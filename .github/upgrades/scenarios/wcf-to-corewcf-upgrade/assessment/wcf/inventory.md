# WCF Inventory — TimeZoneConverter (src48)

Back to [assessment](../../assessment.md) · Scan: [scan.json](scan.json)

## Provenance and Coverage

| Item | Value |
|------|-------|
| Assessed | 2026-10-01 (AI WCF analysis, BreakGlass worker) |
| Scope (confirmed) | `src48/TimeZoneConverter.sln` → `src48/TimeZoneConverter/TimeZoneConverter.csproj`, `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj` |
| Excluded by owner rule | `src60/` (existing hand-written port) — not read, not used as reference |
| Target framework | `net10.0` (scenario property `UpgradeTargetFramework`) |
| WCF scan | `Get-WcfInventory` 1.0.0 (PowerShell 7.6.6), generated 2026-10-01T23:21:46Z, `assessment/wcf/scan.json`; 2 projects, 3 config, 1 `.svc`, 12 source files; **0 parse errors, 0 skipped files, no capped findings** |
| Engine baseline | **UNAVAILABLE** — `generate_dotnet_upgrade_assessment`, `get_projects_info`, `get_project_dependencies`, `get_supported_package_version`, `query_dotnet_assessment` are not exposed in this session (generic Assessor and BreakGlass both checked; tool search returned no match on three attempts). No engine `assessment.json`/`assessment.csv` exists. Engine-owned metrics (API catalog, package compat, dependency depth) are a **coverage gap**, not a clean result. |
| Package feed | `api.nuget.org` unreachable from this machine during assessment (TLS/SSL handshake failure). Package versions below come from the scenario skill's `support-matrix.md` (verified 2026-09-30: CoreWCF 1.9.1, WCF client 10.0.0), not a live lookup. |
| Manual reads | Every file under `src48/` (sln, both csproj, `packages.config`, `Web*.config`, `.svc`, all 11 `.cs`), `contracts/*.wsdl|xsd`, `soapui/TimeZoneConverter-net48-soapui-project.xml` (endpoint/action lines), `.github/workflows/codeql.yml`, `.github/dependabot.yml` |
| Not inspected | IIS/IIS Express `applicationHost.config`, machine.config, real production site bindings, certificate stores; `soapui/*net60*` (belongs to src60 port — ignored) |

Analyzed scope = approved retarget scope (both projects). No retained .NET Framework consumers exist in the repo.

## Executive Summary

- **Readiness: Ready with changes — no hard blocker after analysis.** One small SOAP 1.1 service (`TimeZoneConverterService`, 1 operation) hosted in IIS via `.svc` with default endpoints (basicHttp + basicHttps via `protocolMapping`), HTTP(S) GET metadata, and custom `IOperationBehavior`/`IDispatchMessageFormatter` extensibility wrapping the `XmlSerializer` formatter.
- Scan blocker `WCFSCAN-HOST-ASPNETCOMPAT` (Web.config:34) is **resolvable by removal**: no `HttpContext`, `System.Web`, `OperationContext` or `AspNetCompatibilityRequirements` use exists in any source file (confirmed by search).
- Client side: the generated proxy class `TimeZoneConverterClient` is **unused**, and the `<client>` endpoint names contract `TimeZoneConverter.ITimeZoneConverter`, which does not match the proxy's `ConfigurationName` `TimeZoneConverter.Contracts.ITimeZoneConverter` — dead configuration. The same generated file holds the **server** contract and wire types, so it must be split/ported, not deleted.
- Wire-sensitive items: custom reply action `"ConvertToOffset"` emitted by the formatter, request Action `…/ConvertToOffsetRequest` (differs from the design WSDL's soapAction), `XmlSerializerFormat` + wrapped message contracts, custom XML for `ToDateTime`. All must be preserved.
- Proposed: In-place rehost to ASP.NET Core + CoreWCF (`CoreWCF.Primitives`, `CoreWCF.Http`) on `net10.0`, code-based configuration; test project → SDK-style `net10.0` MSTest.
- Unknowns: external consumers (SoapUI project implies manual/SOAP clients), production HTTPS site binding, deployment host/OS.

## High-Level Metrics

| Metric | Count |
|--------|-------|
| Projects inspected | 2 / 2 (1 server+client web app, 1 test) |
| Services | 1 (`TimeZoneConverter.TimeZoneConverterService`) |
| Service contracts | 1 (`ITimeZoneConverter`, ns `http://bartjolling.github.io/`), 1 operation |
| Service endpoints (effective) | 2 default (basicHttpBinding/None on http, basicHttpsBinding/Transport on https) + HTTP(S) GET metadata |
| Client endpoints | 1 configured (dead — contract name unresolvable), 0 code usages |
| Custom extensibility types | 4 (2 `IOperationBehavior` attributes, 2 `IDispatchMessageFormatter`) + 2 `XmlObjectSerializer` subclasses |
| Scan findings | 1 blocker (resolvable), 5 change, 10 info (8 extensibility hits) |
| AI findings | 12 (`WCF-001`…`WCF-012`): 0 blocker, 2 high, 6 medium, 4 low/info |
| Scan parse errors / skipped | 0 / 0 |
| Tests | 2 MSTest unit tests (serializer only); no endpoint/integration tests |

## Projects and Dependencies

| Project | Role | Current | Proposed | Format / packages |
|---------|------|---------|----------|-------------------|
| `src48/TimeZoneConverter/TimeZoneConverter.csproj` (Assembly/RootNamespace **`TimeZoneConvertor`**) | server (+ unused generated client) | net481, non-SDK WebApplication (`{349c5851…}`), IIS Express port 50355, `OutputType=Library` | net10.0, `Microsoft.NET.Sdk.Web`, `OutputType=Exe`, keep `AssemblyName`/`RootNamespace` = `TimeZoneConvertor` | GAC refs: System.ServiceModel, System.ServiceModel.Web, System.Runtime.Serialization, System.Web*, System.EnterpriseServices, etc. No NuGet packages |
| `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj` | test | net481, non-SDK MSTest, `packages.config` (MSTest.TestAdapter/Framework 2.2.7, **not restored** — no `src48/packages`) | net10.0 SDK-style MSTest | ProjectReference → TimeZoneConverter |

- Edges: Test → TimeZoneConverter (project reference; uses `TimeZoneConvertor.Serializers.ConvertToOffsetResponseSerializer` and `TimeZoneConverter.Contracts.*`). No service-call edges in repo (proxy unused).
- No `global.json`, `Directory.Build.*`, `Directory.Packages.props` in scope. SDKs present: 8.0.425, 10.0.203, 10.0.401 (+10.0.400 preview).
- Solution folder `contracts` links `../contracts/TimeZoneConverter.wsdl|xsd`; csproj also lists `Contracts\TimeZoneConverter.wsdl|xsd` as `None` items that **do not exist** on disk (stale items; drop during SDK conversion).
- CI: `.github/workflows/codeql.yml` uses CodeQL autobuild on `windows-latest` (builds whatever solution it finds — will need to build net10.0 after migration). `dependabot.yml` nuget at `/`.

## Services and Endpoints

Service `TimeZoneConverter.TimeZoneConverterService` (`TimeZoneConverter.svc.cs:7`) implements `TimeZoneConverter.Contracts.ITimeZoneConverter`. Activated by `TimeZoneConverter.svc` (no factory, `Debug="true"`). No `<service>` element → default endpoints (`WCFSCAN-HOST-DEFAULT-ENDPOINTS`), `multipleSiteBindingsEnabled=true`. Instancing/concurrency defaults (PerSession→effectively PerCall on basicHttp, ConcurrencyMode.Single); no `[ServiceBehavior]`.

| # | Effective address | Binding / security | Source | CoreWCF mapping | Class |
|---|-------------------|--------------------|--------|-----------------|-------|
| E1 | `http://<site>/TimeZoneConverter.svc` (dev: `http://localhost:50355/TimeZoneConverter.svc`) | basicHttpBinding, Security None, SOAP 1.1, text, defaults | default endpoint | `BasicHttpBinding()` at `/TimeZoneConverter.svc` | supported with changes (`WCFSCAN-HOST-SVC`, `-DEFAULT-ENDPOINTS`) |
| E2 | `https://<site>/TimeZoneConverter.svc` | basicHttpsBinding (protocolMapping, Web.config:32) → Transport, client cred None | default endpoint, **only if site has https binding** (IIS Express `IISExpressSSLPort` empty → not active in dev) | `BasicHttpBinding(BasicHttpSecurityMode.Transport)` + Kestrel HTTPS (cert = prerequisite) | supported with changes; needs owner input on HTTPS |
| M1 | `…/TimeZoneConverter.svc?wsdl`, `?singleWsdl`, `?xsd=xsd0…` | serviceMetadata httpGetEnabled/httpsGetEnabled=true (Web.config:25) | default service behavior | `AddServiceModelMetadata()` + `ServiceMetadataBehavior { HttpGetEnabled=true, HttpsGetEnabled=true }` | supported (`WCFSCAN-META-HTTPGET`) |

Behaviors / settings map:

| Source | Value | Target | Disposition |
|--------|-------|--------|-------------|
| Web.config:25 `serviceMetadata` | httpGet+httpsGet true | code `ServiceMetadataBehavior` | mapped |
| Web.config:27 `serviceDebug` | includeExceptionDetailInFaults=false | CoreWCF default (false); set explicitly in code | preserved |
| Web.config:31-33 `protocolMapping` | https→basicHttpsBinding | explicit E2 endpoint | mapped |
| Web.config:34 `aspNetCompatibilityEnabled` | true | none (no code dependency) | unsupported with evidence → removed (WCF-001) |
| Web.config:34 `multipleSiteBindingsEnabled` | true | Kestrel URLs / host bindings | mapped (host config) |
| Web.config:4 `aspnet:UseTaskFriendlySynchronizationContext` | true | n/a on ASP.NET Core | removed (not applicable) |
| Web.config:13,16-20 `<bindings>`/`<client>` | client endpoint to localhost:50355, contract name mismatch | none (dead) | removed with evidence (WCF-007) |
| Web.config:40 `X-Frame-Options: SAMEORIGIN` | custom header | ASP.NET Core middleware | preserved (host behavior) |
| Web.config:47 / transforms `directoryBrowse` | false (Debug transform true) | not enabled in ASP.NET Core by default | preserved |
| Web.config:7 / transforms `compilation debug` | false (Debug: true) | n/a | not applicable |
| `.svc` `Debug="true"` | page debug | n/a | not applicable |

Operation `ConvertToOffset` (`ITimeZoneConverter.cs:21-23`): Action `http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest`, ReplyAction `…/ConvertToOffsetResponse`, `[XmlSerializerFormat]`, wrapped message contracts `ConvertToOffsetRequest`/`ConvertToOffsetResponse` (wrapper ns `http://bartjolling.github.io/`). Implementation method carries `[ResponseSerializer]` (`TimeZoneConverter.svc.cs:9`) which replaces the reply formatter with `ResponseDispatchFormatter` → `ConvertToOffsetResponseSerializer` (XmlSerializer with overrides: emits `ToDateTimeOffset` string as `<ToDateTime>`; declares `xsi`/`xsd` prefixes). **Reply message Action is hard-coded `"ConvertToOffset"`** (`ResponseDispatchFormatter.cs:29`), not the contract ReplyAction. No faults declared; no sessions, duplex, transactions, streaming, security credentials.

## Clients and Shared Contracts

| Client | Evidence | Target | Classification | Disposition |
|--------|----------|--------|----------------|-------------|
| `TimeZoneConverterClient : ClientBase<ITimeZoneConverter>` | `Contracts/ITimeZoneConverter.cs:157` (svcutil 4.8 generated via `GenerateContracts.ps1`) | own service | in-repo, **unused** (0 references) | keep an unchanged copy as baseline; remove from server build or move to test project with `System.ServiceModel.Http` (decision D-4) |
| `<client>` endpoint `BasicHttpBinding_ITimeZoneConverter` | Web.config:17 → `http://localhost:50355/TimeZoneConverter.svc` | own service | dead: contract `TimeZoneConverter.ITimeZoneConverter` ≠ `ConfigurationName` | remove (modern WCF client does not read `<client>` anyway — `WCFSCAN-CLIENT-CONFIG`) |
| SoapUI project `soapui/TimeZoneConverter-net48-soapui-project.xml` | endpoint `http://localhost:50355/TimeZoneConverter.svc`, soapAction `…/ConvertToOffsetRequest`, 4 sample requests | E1 | in-repo manual/test client (non-.NET, raw XML) | must keep working unchanged → validation oracle |
| External consumers | none visible | — | **unknown** | owner input |

Shared contract file `Contracts/ITimeZoneConverter.cs` mixes server contract (interface + message contracts + XmlSerializer types) and client proxy, all with fully-qualified `System.ServiceModel.*` attributes; `Contracts/TimeZoneConversionResponse.cs` is a hand-written partial extending the generated type. Server copy must use `CoreWCF.*` attributes with identical Name/Namespace/Action/ReplyAction/WrapperName/Order values. Generated header says "do not edit"; the regeneration script uses .NET Framework `SvcUtil.exe` (not available for CoreWCF server code) → treat the file as hand-maintained after migration and record that.

## Packages and API Changes

| Project | Remove | Add (version evidence) |
|---------|--------|------------------------|
| TimeZoneConverter | GAC refs System.ServiceModel, System.ServiceModel.Web, System.Web*, System.EnterpriseServices, System.Data*, System.Drawing, System.Configuration, Microsoft.CSharp (SDK provides needed BCL) | `CoreWCF.Primitives` 1.9.1, `CoreWCF.Http` 1.9.1 (support matrix verified 2026-09-30; live NuGet check failed — re-verify at restore); framework `Microsoft.AspNetCore.App` via Sdk.Web. **Not** `CoreWCF.ConfigurationManager` (config uses only unsupported sections) |
| TimeZoneConverterTest | `packages.config`, MSTest 2.2.7 HintPaths, TeamTest import, `EnsureNuGetPackageBuildImports` target | `MSTest` (current 3.x/4.x — choose in planning, live lookup unavailable) or `MSTest.TestFramework`/`TestAdapter` + `Microsoft.NET.Test.Sdk`; optional `System.ServiceModel.Http`/`Primitives` 10.0.0 if a loopback WCF-client test is added (client-role use only) |

API/namespace changes (server): `System.ServiceModel` → `CoreWCF` for `ServiceContract`, `OperationContract`, `XmlSerializerFormat`, `MessageContract`, `MessageBodyMember`; `System.ServiceModel.Channels` → `CoreWCF.Channels` (`Message`, `MessageVersion`, `BindingParameterCollection`); `System.ServiceModel.Description` → `CoreWCF.Description` (`IOperationBehavior`, `OperationDescription`, `XmlSerializerOperationBehavior`); `System.ServiceModel.Dispatcher` → `CoreWCF.Dispatcher` (`IDispatchMessageFormatter`, `DispatchOperation`, `ClientOperation`). `System.Runtime.Serialization.XmlObjectSerializer`, `System.Xml.Serialization` are in-box on net10.0. Hosting: new `Program.cs` with `AddServiceModelServices()`, `AddServiceModelMetadata()`, `UseServiceModel(b => b.AddService<TimeZoneConverterService>().AddServiceEndpoint<…>(…, "/TimeZoneConverter.svc"))`. File-scoped namespaces / C# `latest` already used; fine on net10.0.

## Migration Findings

| ID | Sev | Finding / evidence | Action | Acceptance |
|----|-----|--------------------|--------|-----------|
| WCF-001 | High (scan blocker, resolvable) | `aspNetCompatibilityEnabled=true` Web.config:34 (`WCFSCAN-HOST-ASPNETCOMPAT`); no HttpContext/System.Web/OperationContext use in source | Drop setting; do not add `AspNetCompatibilityRequirements` | No System.Web refs; service answers SoapUI requests |
| WCF-002 | Medium | `.svc` activation (`WCFSCAN-HOST-SVC`) | Remove `TimeZoneConverter.svc` + `<Content>`/`DependentUpon`; keep `TimeZoneConverter.svc.cs`; endpoint path `/TimeZoneConverter.svc` | `POST /TimeZoneConverter.svc` works with unchanged URL |
| WCF-003 | Medium | Default endpoints + protocolMapping (`WCFSCAN-HOST-DEFAULT-ENDPOINTS`) | Declare E1 (BasicHttp None) and E2 (BasicHttp Transport) explicitly | Both schemes respond where bound; WSDL lists expected ports |
| WCF-004 | Medium | Behaviors in config (`WCFSCAN-CFG-BEHAVIORS`, `WCFSCAN-META-HTTPGET`) | Code `ServiceMetadataBehavior` http+https GET; `includeExceptionDetailInFaults=false` | `?wsdl`/`?singleWsdl`/`?xsd` served; faults carry no detail |
| WCF-005 | High | Custom `IOperationBehavior` attribute on **implementation method** + formatter wrapping `XmlSerializerOperationBehavior` (`WCFSCAN-PM-EXTENSIBILITY` ×8) | Port 4 classes to CoreWCF namespaces; verify CoreWCF applies operation-behavior attributes from service methods and exposes `XmlSerializerOperationBehavior`/`DispatchOperation.Formatter` same way; fallback: apply via `IServiceBehavior`/`ConfigureServiceHostBase` | Response body byte-equivalent to golden capture (`<ToDateTime>` with offset) |
| WCF-006 | High (wire) | Reply Action `"ConvertToOffset"` (`ResponseDispatchFormatter.cs:29`, `RequestResponseDispatchFormatter.cs:24`); request Action `…/ConvertToOffsetRequest` vs design WSDL soapAction `…/ConvertToOffset` | Preserve runtime behavior exactly; do not "fix" | Captured request/response envelopes match baseline (action, ns, element order) |
| WCF-007 | Medium | Shared generated file mixes server contract and unused client; dead `<client>` config (contract name mismatch) (`WCFSCAN-CLIENT-CONFIG`, `-GENERATED`) | Keep unchanged baseline copy; convert server contract/message attributes to CoreWCF; remove dead client config; client class per D-4 | Build clean; no CoreWCF+System.ServiceModel type clash; wire identity unchanged |
| WCF-008 | Medium | Non-SDK WebApplication net481, IIS Express hosting | Convert to `Microsoft.NET.Sdk.Web` net10.0, keep AssemblyName/RootNamespace `TimeZoneConvertor`, drop stale `None` items, IIS Express props, Web.config transforms; migrate X-Frame-Options header | Project builds on net10.0 with 0 warnings introduced |
| WCF-009 | Medium | Test project non-SDK, packages.config (not restored), MSTest 2.2.7 | SDK-style net10.0 MSTest; keep both tests unchanged in intent | `ConversionReponseSerializerTest` passes on net10.0 (exact XML string incl. `xmlns:xsd`/`xmlns:xsi` order) |
| WCF-010 | Low | No endpoint-level tests | Add loopback endpoint test(s) (in-process Kestrel or `WebApplicationFactory`) replaying SoapUI requests | Test asserts action, namespaces, `ToDateTime` value |
| WCF-011 | Low | `RequestResponseSerializerAttribute`/`RequestResponseDispatchFormatter` unused (0 references) | Port as-is (keep code parity) — no behavior change | Compiles under CoreWCF |
| WCF-012 | Info | Engine baseline unavailable; NuGet feed unreachable during assessment | Re-run engine assessment when tools are available; verify package versions at first restore | Restore succeeds with chosen versions |

## Validation and Deployment

- Existing baseline evidence: 2 serializer unit tests; SoapUI net48 project (4 requests, cached runtime WSDL with soapAction `…/ConvertToOffsetRequest`); design contract `contracts/TimeZoneConverter.wsdl|xsd`.
- **Golden metadata capture plan** (execution, before changes): from the original IIS Express host `http://localhost:50355/TimeZoneConverter.svc` capture `?wsdl`, `?singleWsdl`, each `?xsd=xsdN`, plus one request/response envelope per SoapUI sample. Prerequisites: .NET Framework 4.8.1 targeting pack + VS WebApplication MSBuild targets, IIS Express, MSTest packages restore. **Offline alternative** if the Framework host cannot run: the SoapUI project's cached WSDL/XSD (runtime-generated) + `contracts/*` + the expected XML string in `ConversionReponseSerializerTest.cs:18`; record as reduced-fidelity baseline.
- Per-endpoint checks: E1 SOAP 1.1 POST with SoapUI payloads → identical response envelope (action `ConvertToOffset`, ns `http://bartjolling.github.io/`, `xsi`/`xsd` prefixes, `ToDateTime` ISO-8601 with offset); M1 metadata available on http (and https when bound) and comparable to golden; E2 only if an HTTPS dev cert is available (otherwise unverified).
- Deployment: currently IIS/IIS Express (`UseIIS=True`, port 50355). Target host (Kestrel / IIS in-process via ASP.NET Core Module / container) and OS unknown → owner input. Keep dev URL `http://localhost:50355` in `launchSettings.json` to preserve the SoapUI/client address.
- CI: CodeQL autobuild must build the net10.0 solution (Windows runner has .NET 10 SDK) — verify after migration.

## Decisions and Open Questions

| ID | Decision | Options | Recommendation (default per scenario-instructions) |
|----|----------|---------|-----------------------------------------------------|
| D-1 | WCF Host Approach | In-place / Side-by-side | **In-place** (no other System.Web features to keep; no retained Framework consumer) |
| D-2 | WCF Configuration | Code and application configuration / Retain supported XML | **Code and application configuration** (config uses only behaviors/protocolMapping/serviceHostingEnvironment, which ConfigurationManager ignores) |
| D-3 | WCF Wire Compatibility | Preserve contract / Strict XML parity / Regenerate owned clients | **Strict XML parity** (SoapUI raw-XML consumer; custom serializer output; unknown externals) |
| D-4 | Unused generated client class | Remove from server (keep baseline copy) / keep with `System.ServiceModel.Http` 10.0.0 | **Remove from server project**, keep unchanged baseline copy under the workflow folder; use a WCF client only in tests if needed |
| D-5 | aspNetCompatibility | Remove (no dependency) | **Remove** — evidence: no HttpContext/System.Web use |
| D-6 | WCF Validation Environment | Available / Owner provides / Accept unverified | **Accept unverified** for HTTPS endpoint E2 and original-host golden capture if Framework host cannot run; use offline baseline |

**Owner inputs** (open; defaults above apply under Automatic mode):
- Clients: any external consumers beyond the SoapUI project? Technology/owner; can they change URL or regenerate proxies?
- Security intent: HTTP (None) + HTTPS (Transport, anonymous) as today — confirm no change.
- Deployment target and OS (IIS in-process, Kestrel, container); production HTTPS site binding and certificate.
- Load expectations: not required (no singleton/throttling flags).
- Wire compatibility level: D-3.
- Validation prerequisites: .NET Framework 4.8.1 build tooling + IIS Express for golden capture (availability unverified); HTTPS dev certificate for E2 (unverified); NuGet feed access (failed TLS during assessment — must work for restore).

## Detailed Reports

- [scan.json](scan.json) — deterministic WCF scan
- [../../assessment.md](../../assessment.md) — root assessment entry point
