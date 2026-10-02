# Progress — 04-test-project-net10

## Result (2026-10-01)

**Outcome: done.** `src48/TimeZoneConverterTest` now targets `net10.0` on MSTest 4.4.1, against the CoreWCF host from task 03. The full `src48/TimeZoneConverter.sln` builds with **0 warnings and 0 errors**. **15/15 tests pass, 0 failed, 0 skipped:** the 2 original unit tests (unchanged) plus 13 new endpoint wire tests (WCF-010). Their parity assertions read the task 01 golden net481 baseline, and each file is SHA256-checked against `capture-manifest.json`.

Decomposition verdict: **atomic**. I evaluated scenario `execution.md` (small-cohesive-wcf: one test project, known file list, no open decision; the SDK checkpoint was already done in task 02) and the `migrating-wcf-to-corewcf` decomposition section. The scenario has no separate Breakdown Hints files, and no scenario extensions apply at Execution scope. task.md was enriched before the first source edit.

## Files changed (src48 only; `src60/` not read or touched; host project unchanged)
| File | Change |
|---|---|
| `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj` | `net481` → `net10.0`. Removed GAC `Reference System.Xml.Serialization` and `MSTest.TestAdapter`/`MSTest.TestFramework` 2.2.7. Added `MSTest` 4.4.1 (metapackage, which includes Microsoft.NET.Test.Sdk and the analyzers), `Microsoft.AspNetCore.Mvc.Testing` 10.0.12, `FrameworkReference Microsoft.AspNetCore.App` (see "Warning fixed"), `IsPackable=false`, `IsTestProject=true`, and `WireBaseline\**` copied to output. Kept `LangVersion latest`, `GenerateAssemblyInfo false` (`Properties/AssemblyInfo.cs`) and the ProjectReference. |
| `src48/TimeZoneConverterTest/ConversionReponseSerializerTest.cs` | **Unchanged.** Both tests pass on net10.0 as-is, including the exact XML string with `xmlns:xsd` before `xmlns:xsi` (WCF-009). |
| `src48/TimeZoneConverterTest/EndpointWireTests.cs` | **New**: 9 test methods, 13 test cases. Details below. |
| `src48/TimeZoneConverterTest/WireBaseline/` | **New.** Byte-exact copies of `wcf-baseline/envelopes/*` (10), `wcf-baseline/metadata/*` (5) and `capture-manifest.json`, plus `README.md` (provenance) and `.gitattributes` (`* -text`, so the CRLF/LF mix of the SoapUI request bodies stays as captured; checked with `git check-attr text` → unset). |
| Workflow: `task.md` (research section), `scenario-instructions.md` (Build Tool Decisions → test project now uses `dotnet build`/`dotnet test`), `net10-tests.trx` (test results), this file | |

`Program` (top-level statements in the host) is visible to `WebApplicationFactory<Program>` without changes. The .NET 10 web SDK generates the public partial `Program`, so no `InternalsVisibleTo` or host edit was needed.

## Endpoint wire tests (WCF-010): host and assertions
Host: `WebApplicationFactory<Program>` + `UseKestrel(o => o.Listen(IPAddress.Loopback, 0))` + `StartServer()`. This is real HTTP on **127.0.0.1, dynamic port**, in the test process, and is disposed in `ClassCleanup`. Only E1 is registered (no https URL), which matches the golden host. I chose real Kestrel over TestServer because it gives a real loopback transport, CoreWCF derives its base address from the Kestrel server addresses, and binding to the loopback IP keeps the test host contained.

| Test | Assertions (all compared against `WireBaseline/` golden) | Result |
|---|---|---|
| `TestHost_ListensOnLoopbackOnly` | Bound address is http + loopback IP | Passed |
| `GoldenBaseline_FilesMatchCaptureManifest` | SHA256 of all 15 golden files = manifest values (provenance). Every other test also checks the hash of each file it reads. | Passed |
| `ConvertToOffset_SoapUiRequest_ResponseMatchesGoldenBaseline` ×4 (Lower/Higher/Same/Negative) | Replays the exact SoapUI request bytes with the manifest `Content-Type`/`SOAPAction`. Checks: status 200 = manifest; `Content-Type: text/xml; charset=utf-8` and `X-Frame-Options: SAMEORIGIN` = manifest; **response bytes identical to golden**. Structural checks: `s:Envelope` SOAP 1.1, **no `<s:Header>`** (reply Action not on the wire, the golden E1 behavior), no SOAPAction response header; body root `{http://bartjolling.github.io/}ConvertToOffsetResponse` with ns declarations in order default, `xmlns:xsd`, `xmlns:xsi`; element order Response › TimeZoneConversionResponse › `ToDateTime` only (WCF-005); `ToDateTime` value = the golden value, e.g. `2022-09-24T02:23:45.6780000-06:00`. | 4 Passed |
| `UnknownAction_FaultMatchesGoldenBaselineStructure` | Unknown SOAPAction (request Action dispatch check). Checks: 500 + headers = manifest; Fault children = golden (`faultcode`, `faultstring`; **no `detail`**); faultcode `a:ActionNotSupported` with the same `a` namespace; `xml:lang` = golden (`en-US`); faultstring is non-empty. The wording itself is not compared, per baseline README §5. | Passed |
| `Metadata_Xsd_IsByteIdenticalToGoldenBaseline` | `?xsd=xsd0` bytes identical; status and headers match | Passed |
| `Metadata_Wsdl_MatchesGoldenBaselineInfoset` ×3 (`?wsdl`, `?wsdl=wsdl0`, `?singleWsdl`) | Token-by-token XML comparison: element and attribute names with literal prefixes and namespace URIs, attribute order, values, text, whitespace and the XML declaration. **The only normalization is sorting the `xmlns:*` declarations per element**, which is the accepted decision in scenario-instructions. The golden authority `http://localhost:50355` is replaced with the test host base address (port change only). Also asserts that the metadata addresses point at the CoreWCF endpoint. | 3 Passed |
| `ServiceHelpPage_MatchesGoldenBaseline` | GET `/TimeZoneConverter.svc` text identical after the same authority substitution | Passed |
| `ReplyAction_IsPreservedByResponseDispatchFormatter` | WCF-006: `ResponseDispatchFormatter.SerializeReply(Soap11, …)` → `Headers.Action == "ConvertToOffset"`. This is the only place the reply Action can be observed. | Passed |

**Sensitivity check (mutation):** I temporarily swapped the `xsd`/`xsi` `XmlSerializerNamespaces.Add` order in the host's `ConvertToOffsetResponseSerializer.cs`, which reverts task 03's wire-parity fix. Result: **5 failures** (`WriteObject` + all 4 SoapUI replays). The file was then restored byte-for-byte (SHA256 match verified) and rebuilt. The wire tests therefore detect a real parity regression and do not pass vacuously.

## Build / test commands and results
- `dotnet build src48\TimeZoneConverter.sln -c Debug --no-incremental` (SDK 10.0.401): **0 Warning(s), 0 Error(s)**. Outputs: `TimeZoneConverter\bin\Debug\net10.0\TimeZoneConvertor.dll`, `TimeZoneConverterTest\bin\Debug\net10.0\TimeZoneConverterTest.dll`. The NU1201 from task 03 (net481 test → net10.0 host) is resolved.
- `dotnet test src48\TimeZoneConverterTest\TimeZoneConverterTest.csproj --no-build -c Debug --logger trx`: **15 discovered, 15 executed, 15 passed, 0 failed, 0 skipped** (VSTest, ~1 s). TRX saved as `net10-tests.trx` in this task folder. `TestResults/` was deleted from src48 (it is gitignored anyway).
- `dotnet list …TimeZoneConverterTest.csproj package --vulnerable --include-transitive`: none.
- Restore went through `packagefeedproxy.microsoft.io`. MSTest 4.4.1 and Mvc.Testing 10.0.12 resolved.

## Warning fixed (not suppressed)
The first solution build had 10× **NU1903** (`System.Security.Cryptography.Xml` 8.0.3, high severity) on the test project. Path: `CoreWCF.Primitives`/`CoreWCF.Http` 1.9.1 and `Microsoft.AspNetCore.DataProtection` 2.3.10 (a CoreWCF dependency) → `System.Security.Cryptography.Xml` 8.0.3. The host doesn't get it because its `Microsoft.AspNetCore.App` framework reference makes NuGet prune those packages (I checked the host's `packagesToPrune`). The test project only had `Microsoft.NETCore.App` at restore, so nothing was pruned. Fix: a direct `FrameworkReference Microsoft.AspNetCore.App` in the test project, which matches the shared framework the in-process host runs on. After the fix: 0 warnings, no vulnerable packages.

## Deviations / notes
- No WCF-client (`System.ServiceModel.*` 10.0.0) test was added. The task makes it conditional ("any WCF-client test"), and the plan's validation oracle is the SoapUI raw-XML client, which these tests replay byte-for-byte. A typed-client compatibility test (for example with the unchanged generated proxy in `wcf-baseline/contracts-original/`) remains an optional add-on.
- The fault test compares `xml:lang` with the golden `en-US`. This value can depend on the runtime culture, so a CI agent running a non-en-US culture could see a different value. The faultstring wording is intentionally not compared.
- The WSDL tests depend on the accepted xmlns-declaration-order decision. Every other byte of structure and content is compared strictly.
- No MSTest analyzer warnings appeared, so no parallelization attribute was needed. The tests share one host per class and run sequentially by default.

## UNVERIFIED (carried forward, not newly introduced)
- E2 / M1 over HTTPS: needs a trusted certificate or production HTTPS binding (owner input). The tests cover E1 and M1 over HTTP only.
- Real SoapUI application run: SoapUI isn't available, so its payloads are replayed byte-for-byte by the tests.

## Processes owned at return
No long-lived processes were started. The in-process Kestrel host ran inside the `dotnet test` testhost and was disposed in `ClassCleanup`, and each testhost exited when its run completed. Afterwards no `testhost`/`TimeZoneConvertor`/`dotnet` process from this worktree is running. Nothing was committed, and no certificates, trust stores or URL reservations were touched.

## Code review fixes (2026-10-01)

**Outcome: done.** All 4 findings are fixed. `dotnet build src48\TimeZoneConverter.sln -c Debug --no-incremental` gives **0 Warning(s), 0 Error(s)** with no suppressions. `dotnet test` result: **22 passed, 0 failed, 0 skipped** (the 15 earlier tests plus 7 new cases). TRX: `net10-tests-review-fixes.trx` in this folder. Decomposition verdict for the fix pass: **atomic** (`execution.md` small-cohesive-wcf). No scenario extensions apply at Execution. I enriched task.md (section "Code-review fix pass — research") before the first source edit.

### Files changed
| File | Change |
|---|---|
| `src48/TimeZoneConverter/Program.cs` | (1) `https_port` is no longer treated as a listener signal. `HasHttpsListener` now looks only at `urls`, `https_ports` and `Kestrel:Endpoints:*:Url`. (2) New switch `TimeZoneConverter:EnableHttpsEndpoint` (`auto`/`true`/`false`), handled by `ShouldRegisterHttpsEndpoint`. In `auto` mode, if `https_port` is set but there's no HTTPS listener, it logs a warning that names the switch. Any other value fails fast. (3) Registers `UseRequestHeadersForMetadataAddressBehavior` as a DI `IServiceBehavior`. Also registers `HttpsEndpointStartupCheck` as a hosted service. |
| `src48/TimeZoneConverter/HttpsEndpointStartupCheck.cs` | **New.** An `IHostedLifecycleService.StartedAsync` check that runs after the server has bound its addresses. If E2 is registered and `IServerAddressesFeature` reports no `https://` address, startup fails with an `InvalidOperationException` that names the switch and the addresses actually bound. |
| `src48/TimeZoneConverter/appsettings.json` | Adds `"TimeZoneConverter": { "EnableHttpsEndpoint": "auto" }` with comments documenting each value (the ASP.NET Core JSON provider accepts comments). |
| `src48/TimeZoneConverterTest/EndpointWireTests.cs` | Adds `PinnedHostFactory` and `StartLoopbackHost`, the 7 new test cases listed below, the helpers `AssertSameInfoset`/`AssertSingleHttpPortAsync`/`Flatten`, and `CapturingLoggerProvider`. `ReplayAsync` now takes the client as a parameter. |
| Workflow: `task.md` (research section), `scenario-instructions.md` (the E2 line and owner inputs now mention the switch), this file, `net10-tests-review-fixes.trx` | |

### Decision for finding 2 (IIS / explicit switch)
- `TimeZoneConverter:EnableHttpsEndpoint`, set in `appsettings.json` or via the environment variable `TimeZoneConverter__EnableHttpsEndpoint`:
  - **`auto`** (default): E2 is registered only when Kestrel has a real HTTPS listener configured.
  - **`true`**: E2 is always registered. This is the setting for IIS in-process hosting, where the site's https binding isn't visible in configuration.
  - **`false`**: E2 is never registered.
- **When E2 is registered without an https server address, startup fails fast with a clear message.** I chose this over skipping E2 with a warning. An explicit `true` means the deployer wants E2, so a missing HTTPS address is a deployment error and should be loud.
- **Why a startup check is needed:** I found that CoreWCF 1.9.1 resolves base addresses lazily, on the first request. Without the check, the host *starts*, and then every request, E1 and metadata included, returns 500 "Could not find a base address that matches scheme https". This is the "E1 dies silently" failure from the review, and the new check turns it into a startup failure.
- In `auto` mode, `https_port` alone (which ANCM sets for IIS https bindings) only produces a warning that names the switch. E1 keeps working.
- **Owner input (new):** set `TimeZoneConverter:EnableHttpsEndpoint=true` per environment wherever the original service exposed HTTPS through an IIS site binding. **UNVERIFIED:** under IIS in-process, the check and CoreWCF both rely on `IServerAddressesFeature` listing the https binding. That needs a run on the real IIS site with its certificate. Under IIS *out-of-process*, Kestrel only listens on ANCM's http port, so E2 can't be served there and `true` fails startup by design.

### Finding 3: metadata addresses
`UseRequestHeadersForMetadataAddressBehavior` (CoreWCF.Primitives 1.9.1, public type, confirmed by reflection) now builds `soap:address` and `wsdl:import` from the request Host header. With Host `localhost:50355` sent to a host listening on `127.0.0.1:<dynamic>`, `?wsdl`, `?wsdl=wsdl0` and `?singleWsdl` match the golden baseline **with no address substitution**, and the only difference is still the accepted xmlns-declaration order. The existing WSDL, help-page and XSD golden tests still pass unchanged. I did not separately assert that the help page follows the Host header.

### Finding 4: pinned test host and new tests
`PinnedHostFactory` pins `urls=""`, `https_ports=""`, `https_port=""` and `TimeZoneConverter:EnableHttpsEndpoint=auto` via `UseSetting`, and Kestrel is bound in code to 127.0.0.1:0. To verify the pinning, I re-ran the full suite with `ASPNETCORE_URLS=https://127.0.0.1:5999`, `ASPNETCORE_HTTPS_PORTS=5998`, `ASPNETCORE_HTTPS_PORT=44300` and `TimeZoneConverter__EnableHttpsEndpoint=true` set in the environment: **22/22 passed**. Remaining gap: machine-level `Kestrel__Endpoints__*` variables can't be cleared with `UseSetting`.

| New test | What it covers | Result |
|---|---|---|
| `Metadata_Wsdl_AddressesFollowRequestHostHeader` ×3 | Finding 3: Host `localhost:50355` → golden WSDL with no substitution, and the listen address is not exposed | Passed |
| `HttpsPortWithoutHttpsListener_HostStartsAndServesE1Only` | Finding 1: with `https_port=44300` and only an HTTP listener, the host starts, the SoapUI replay is byte-identical to the golden, the WSDL has exactly one (http) `soap:address`, and the switch warning is logged | Passed |
| `EnableHttpsEndpointFalse_HttpsUrlConfigured_E2NotRegistered` | `false` wins over an https `urls` value | Passed |
| `EnableHttpsEndpointTrue_NoHttpsAddress_StartupFailsFast` | `true` without an https address → startup throws a message that names the switch | Passed |
| `EnableHttpsEndpointInvalidValue_StartupFailsWithClearMessage` | `yes` → "Invalid value 'yes' for TimeZoneConverter:EnableHttpsEndpoint" | Passed |

**Mutation checks** (each source file was restored afterwards, verified by SHA256):
- Putting `https_port` back as a listener signal → `HttpsPortWithoutHttpsListener_…` fails.
- Removing the `UseRequestHeadersForMetadataAddressBehavior` registration → all 3 `AddressesFollowRequestHostHeader` cases fail.

### Processes / side effects
- Every host ran in-process inside the `dotnet test` testhost and was disposed. No `testhost`/`TimeZoneConvertor` process is left running.
- The environment variables were set only in a single short-lived shell.
- `src48/**/TestResults` was removed. No certificates, trust stores or URL reservations were touched, nothing was committed, and `src60/` was not read.
- I did not write an E2 HTTPS runtime test. It would need a certificate whose private key gets persisted, which the constraints rule out, so E2 over HTTPS stays UNVERIFIED (owner input).
