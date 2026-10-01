# WCF to CoreWCF Upgrade Plan

## Overview

**Target**: Rehost the IIS `.svc` WCF service `TimeZoneConverterService` (`src48/TimeZoneConverter`) on ASP.NET Core + CoreWCF on `net10.0` in place, and move its MSTest project (`src48/TimeZoneConverterTest`) to SDK-style `net10.0`, with strict XML wire parity for the SoapUI raw-XML consumer.
**Scope**: Small solution, 2 projects (`src48/TimeZoneConverter.sln`), 1 SOAP 1.1 service with 1 operation, 2 default endpoints + HTTP(S) GET metadata, 6 custom extensibility/serializer types, 2 unit tests. `src60/` is out of scope and must not be read or changed. Engine baseline unavailable (coverage gap, WCF-012); findings come from the AI WCF inventory ([assessment/wcf/inventory.md](assessment/wcf/inventory.md)).

### Selected Strategy
**Bottom-Up (Dependency-First)** — Upgrade from leaf nodes to root applications, tier by tier.
**Rationale**: 2 .NET Framework 4.8.1 projects with a 2-tier dependency graph (test project → service host); Framework→Core crossing with different mechanics per project (WCF rehost vs. MSTest retarget).

```
Tier 2: [TimeZoneConverterTest]  (MSTest, ProjectReference → host)
              ↓
Tier 1: [TimeZoneConverter]      (WCF .svc host, assembly TimeZoneConvertor)
```

- **Tier 1 — TimeZoneConverter**: no internal references. Complete when it builds on `net10.0` with CoreWCF and serves E1/M1 (E2 where an HTTPS binding is available) with wire parity against the baseline.
- **Tier 2 — TimeZoneConverterTest**: depends on Tier 1. Complete when it builds on `net10.0` against the migrated host and its unit and endpoint tests pass.
- Between-tier note: a `net481` test project cannot reference a `net10.0` host, so between Tier 1 and Tier 2 the test project is expected not to build. Tier 1 is validated through its own build and runtime wire checks, and Tier 2 follows immediately.

## Upgrade Options

| Option | Selected | Why |
|--------|----------|-----|
| Upgrade Strategy | Bottom-Up | Multiple .NET Framework projects (net481 host + dependent test project); Framework→Core boundary fixes Bottom-Up |
| Project Approach | In-place | Non-WCF project (MSTest) has no remaining Framework consumers, so its TFM is replaced directly |
| WCF Host Approach | In-place | Pure `.svc` host with no other System.Web features and no retained Framework consumer (D-1) |
| Unsupported API Handling | Fix Inline | Few, mechanical `System.ServiceModel` → `CoreWCF` changes; no deferred stubs wanted |
| WCF Wire Compatibility | Strict XML parity | SoapUI project is a raw-XML consumer, custom formatter/serializer output (WCF-005/006), external consumers unknown (D-3) |
| Nullable Reference Types | Leave Disabled | Not enabled today; enabling is unrelated to the service rehost |
| WCF Configuration | Code and application configuration | Web.config holds only behaviors/protocolMapping/serviceHostingEnvironment that CoreWCF.ConfigurationManager does not cover; CoreWCF.ConfigurationManager not used (D-2) |
| WCF Validation Environment | Owner provides | Wire/security checks need IIS Express + net481 build tooling for the golden capture, NuGet feed access, and an existing HTTPS dev certificate for E2. Session runs on its own and uses prerequisites already on the machine (VS 18 MSBuild, WebApplication targets, .NET 10 SDK). Any check whose prerequisite is still missing is recorded as UNVERIFIED with the prerequisite named |

## Tasks

### 01-validation-environment-and-wire-baseline: Confirm prerequisites, baseline build/tests and capture the golden wire baseline

Confirm the validation prerequisites the WCF Validation Environment option lists before anything changes: .NET 10 SDK (10.0.203/10.0.401 present; no `global.json`), VS 18 MSBuild with `Microsoft.WebApplication.targets` and the .NET Framework 4.8.1 targeting pack, NuGet feed reachability (`api.nuget.org` failed with a TLS error during assessment, WCF-012), IIS Express for the original host on `http://localhost:50355`, and any HTTPS dev certificate that already exists for E2. Do not create certificates, change trust stores, add URL reservations or install anything globally. For each prerequisite, record whether it is available. Any check that depends on a missing prerequisite is marked **UNVERIFIED** with the prerequisite named. It is not passed, and the session does not wait on it.

Build `src48/TimeZoneConverter.sln` on net481 and run the 2 MSTest tests (needs MSTest 2.2.7 restore; `src48/packages` does not exist yet). Capture the golden wire baseline from the original host: `?wsdl`, `?singleWsdl`, every `?xsd=xsdN`, plus the request and response envelopes for each of the 4 SoapUI samples in `soapui/TimeZoneConverter-net48-soapui-project.xml`. The baseline must include the reply Action `ConvertToOffset` (WCF-006), request Action `http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest`, the `xsi`/`xsd` prefixes and the `<ToDateTime>` offset format (WCF-005). If the Framework host cannot run, record the reduced-fidelity offline baseline the inventory defines: the SoapUI cached WSDL/XSD, `contracts/*.wsdl|xsd`, and the expected XML in `ConversionReponseSerializerTest.cs:18`. Also save an unchanged copy of `Contracts/ITimeZoneConverter.cs` (server contract + generated client, D-4) under the workflow folder. #skill:migrating-wcf-to-corewcf

**Done when**: A prerequisite checklist (available / missing → UNVERIFIED) and the baseline artifacts (net481 build result, test result, golden metadata and envelopes, or the labeled offline baseline) are stored under the workflow folder. No source files are changed.

---

### 02-sdk-style-conversion: Convert both projects to SDK-style on net481

Convert `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj` and `src48/TimeZoneConverter/TimeZoneConverter.csproj` to SDK-style while **staying on net481**. Keep this separate from the TFM, hosting and API migration. For the test project: migrate `packages.config` to `PackageReference` at the same MSTest 2.2.7 versions, and remove the TeamTest import, the HintPaths and the `EnsureNuGetPackageBuildImports` target (WCF-009). For the host: keep `AssemblyName`/`RootNamespace` = `TimeZoneConvertor` and remove the stale `None` items `Contracts\TimeZoneConverter.wsdl|xsd`, which are missing on disk (WCF-008). The `.svc`, `Web.config` and transforms stay in place on net481 for now. #skill:migrating-wcf-to-corewcf

Risk: SDK-style conversion of a classic WebApplication (`{349c5851…}`) `.svc` project on net481 may not be supported by the conversion tooling, or may lose IIS Express/.svc build behavior. If the converted host cannot build on net481 with its `.svc` content intact, revert only the host's conversion, record why, and move the host's project-format change into task 03, where it becomes `Microsoft.NET.Sdk.Web`. The test project conversion still lands here.

**Done when**: Each converted project is SDK-style on net481. The solution builds and the 2 unit tests pass on net481, or are recorded UNVERIFIED with the missing prerequisite named (e.g. NuGet restore). Any host-conversion fallback is recorded with its reason.

---

### 03-corewcf-service-host: Rehost TimeZoneConverterService on ASP.NET Core + CoreWCF (net10.0)

Migrate `src48/TimeZoneConverter` in place to `net10.0`, `Microsoft.NET.Sdk.Web` (`OutputType=Exe`), keeping `AssemblyName`/`RootNamespace` = `TimeZoneConvertor`. Replace the GAC references with `CoreWCF.Primitives` 1.9.1 and `CoreWCF.Http` 1.9.1 (no `CoreWCF.ConfigurationManager`). Add a `Program.cs` that registers the service, endpoints and behaviors in code. E1 is `BasicHttpBinding()` at `/TimeZoneConverter.svc`. E2 is `BasicHttpBinding(BasicHttpSecurityMode.Transport)`, active only where an HTTPS binding exists. M1 is `ServiceMetadataBehavior` with HTTP and HTTPS GET, and `includeExceptionDetailInFaults=false` is set explicitly (WCF-002/003/004). Remove the `.svc` file and its `Content`/`DependentUpon` items, `aspNetCompatibilityEnabled` (WCF-001; no HttpContext/System.Web use), `UseTaskFriendlySynchronizationContext`, IIS Express props and Web.config transforms. Map each effective Web.config setting to its new source per the inventory's behaviors/settings table. Move `X-Frame-Options: SAMEORIGIN` to middleware, keep directory browsing off, and put `http://localhost:50355` in `launchSettings.json` so the SoapUI address stays the same. #skill:migrating-wcf-to-corewcf

Port the contract and extensibility code to `CoreWCF.*` namespaces with identical Name/Namespace/Action/ReplyAction/WrapperName/Order values. Split the server contract and wire types out of the generated `Contracts/ITimeZoneConverter.cs`, remove the unused `TimeZoneConverterClient` and the dead `<client>`/`<bindings>` config (WCF-007, D-4), and record that the file is now hand-maintained (SvcUtil regeneration no longer applies). Port `ResponseSerializerAttribute`/`ResponseDispatchFormatter`, the unused `RequestResponse*` pair (WCF-011) and the `XmlObjectSerializer` subclasses. **Highest risks:** whether CoreWCF applies an `IOperationBehavior` attribute placed on the implementation method and exposes `XmlSerializerOperationBehavior`/`DispatchOperation.Formatter` the same way (WCF-005), and preserving the hard-coded reply Action `"ConvertToOffset"` (WCF-006). Do not "fix" either. If attribute discovery differs, apply the behavior through `IServiceBehavior`/service-host configuration. Make sure the solution builds with a plain `dotnet build`/`msbuild` so `.github/workflows/codeql.yml` autobuild still works.

**Done when**: `TimeZoneConverter` builds on net10.0 with no `System.Web`/`System.ServiceModel` server references and no new warnings. Running locally, `POST /TimeZoneConverter.svc` with each SoapUI sample returns envelopes that match the golden baseline under Strict XML parity (Action, namespaces, element names and order, `xsi`/`xsd` prefixes, `<ToDateTime>` value). `?wsdl`/`?singleWsdl`/`?xsd` are served and match the baseline. E2 matches too, or is recorded UNVERIFIED if no HTTPS certificate exists. Every Web.config setting has a recorded destination or removal reason. The test project is expected not to build until task 04.

---

### 04-test-project-net10: Retarget TimeZoneConverterTest to net10.0 and add endpoint wire tests

Retarget `src48/TimeZoneConverterTest` to `net10.0` against the migrated host. Move MSTest 2.2.7 to the `MSTest` 4.4.1 metapackage and update namespaces or references if task 03 changed the contract or serializer types (`TimeZoneConvertor.Serializers.ConvertToOffsetResponseSerializer`, `TimeZoneConverter.Contracts.*`). Keep both existing tests unchanged in intent. `ConversionReponseSerializerTest` checks an exact XML string, including the order of `xmlns:xsd`/`xmlns:xsi` (WCF-009). #skill:migrating-wcf-to-corewcf

Add the endpoint-level tests from WCF-010. These are loopback tests through an in-process host (e.g. `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 `WebApplicationFactory`, or in-process Kestrel) that replay the SoapUI raw-XML requests and check the response Action, namespaces, element order and `ToDateTime` value against the golden baseline from task 01. They become the repeatable Strict XML parity check. Any WCF-client test uses `System.ServiceModel.*` 10.0.0 in the client role only. The host may need a `public partial class Program` or `InternalsVisibleTo` so the test host can find its entry point.

**Done when**: The full `src48/TimeZoneConverter.sln` builds on net10.0. Both original unit tests and the new endpoint wire tests pass, with the parity assertions tied to the recorded baseline (or to the labeled offline baseline if the original host could not be captured).

---

### 05-final-validation: Solution-wide validation, compatibility matrix and completion report

Validate the whole migrated scope: a clean build of `src48/TimeZoneConverter.sln`, all tests, and `dotnet publish` of the host producing a runnable ASP.NET Core output (Sdk.Web `web.config` for IIS in-process included). Re-run the endpoint/client compatibility matrix against the golden baseline. That covers E1 with all 4 SoapUI samples, M1 metadata over HTTP (and HTTPS when bound), E2 Transport where an existing certificate allows, and SoapUI pointed unchanged at `http://localhost:50355/TimeZoneConverter.svc`. Check that `src60/` is unchanged. #skill:migrating-wcf-to-corewcf

The completion report lists every check as passed, accepted change, or **UNVERIFIED (missing prerequisite named)**. It also lists the open owner inputs: external consumers, the production HTTPS binding and certificate, the deployment host and OS (IIS in-process / Kestrel / container), and whether CodeQL autobuild works on the net10.0 solution in CI. Deferred items are listed too: engine reassessment and live package re-verification (WCF-012), and the fact that the contract file is now hand-maintained.

**Done when**: Build, tests and publish succeed. Every assessed finding WCF-001…WCF-012 maps to a completed change or a recorded reason. The compatibility matrix shows Strict XML parity for E1/M1 (E2 passed or UNVERIFIED). No check is silently waived. The completion report records the UNVERIFIED checks and the owner inputs.
