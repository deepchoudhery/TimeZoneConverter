# WCF → CoreWCF Completion Report — TimeZoneConverter (src48)

Date: 2026-10-01 · Scenario `wcf-to-corewcf-upgrade` · Scope `src48/TimeZoneConverter.sln` · Target `net10.0` · CoreWCF 1.9.1
HEAD at validation: `8f87ac3` (task 04). Golden baseline: [`wcf-baseline/`](../../wcf-baseline/README.md), a live capture from the original net481 IIS Express host taken in task 01.

Every check below is classified as **PASSED**, **ACCEPTED CHANGE** (a recorded decision), or **UNVERIFIED** (with the missing prerequisite named). No check is silently waived.

## 1. Build, tests, publish

| # | Check | Command / evidence | Result |
|---|---|---|---|
| B1 | Clean solution build | `dotnet clean src48\TimeZoneConverter.sln -c Release`, then `dotnet restore … --force`, then `dotnet build src48\TimeZoneConverter.sln -c Release --no-incremental` (SDK 10.0.401) | **PASSED**: 0 warnings, 0 errors. Outputs are under `bin\Release\net10.0\`. |
| B2 | MSBuild.exe build (local stand-in for CodeQL autobuild) | VS 18 `MSBuild.exe src48\TimeZoneConverter.sln /restore /t:Rebuild /p:Configuration=Release` | **PASSED**: exit 0, no warnings. |
| B3 | All tests | `dotnet test src48\TimeZoneConverterTest\TimeZoneConverterTest.csproj -c Release --logger trx` | **PASSED**: 22 discovered, 22 executed, 22 passed, 0 failed, 0 skipped ([final-release.trx](final-release.trx)). That is 2 original unit tests plus 20 endpoint wire and HTTPS-switch cases. |
| B4 | Package health | `dotnet list … package --vulnerable --include-transitive` and `--outdated` (proxy feed) | **PASSED**: no vulnerable packages and no newer versions. Restore showed no NU1608 or downgrade warnings. |
| B5 | `dotnet publish` of the host | `dotnet publish src48\TimeZoneConverter\TimeZoneConverter.csproj -c Release -o %TEMP%\tzc-final-publish` (outside the repo, deleted after validation) | **PASSED**. Output contains `TimeZoneConvertor.dll/.exe/.deps/.runtimeconfig` (Microsoft.NETCore.App + Microsoft.AspNetCore.App 10.0), `appsettings.json`, CoreWCF.Primitives/Http **1.9.1.31**, and the Sdk.Web **`web.config`**: `AspNetCoreModuleV2`, `processPath="dotnet" arguments=".\TimeZoneConvertor.dll"`, `hostingModel="inprocess"`. It contains **no** `System.ServiceModel.*`, `System.Web.dll` or stub/shim assemblies. `System.Web.Services.Description.dll` is CoreWCF's WSDL object-model package, not System.Web. |
| B6 | Published output starts and serves | `dotnet TimeZoneConvertor.dll --urls http://localhost:50355` from the publish folder (Production environment) | **PASSED**: listens on 127.0.0.1:50355 and [::1]:50355 only, and serves the full matrix below. Stopped by PID. |
| B7 | Launch profile (the dev URL SoapUI uses) | `dotnet run --no-build -c Release --launch-profile TimeZoneConverter` | **PASSED**: `http://localhost:50355`, and the 4 SoapUI replays are byte-identical to golden. |
| B8 | E2 switch fail-fast (`TimeZoneConverter:EnableHttpsEndpoint=true` with HTTP only) on the published binary | env `TimeZoneConverter__EnableHttpsEndpoint=true`, `--urls http://localhost:50355` | **PASSED (fail-fast)**: "Hosting failed to start", non-zero exit, no listener left. *Observation:* in the real process CoreWCF builds its dispatcher during server start, so the failure message is CoreWCF's own ("Could not find a base address that matches scheme https …"). The switch-naming message from `HttpsEndpointStartupCheck` doesn't appear because CoreWCF fails first. That check's message shows up under `WebApplicationFactory` (test `EnableHttpsEndpointTrue_NoHttpsAddress_StartupFailsFast`). The fail-fast outcome required by the decision holds. The diagnostic text is a low-severity follow-up (see §6). |
| B9 | `src60/` unchanged (checked with git only, contents not opened) | `git diff --stat 02bf930 HEAD -- src60` → empty; `git log 02bf930..HEAD -- src60` → empty; `git status --porcelain --ignored -- src60` → empty | **PASSED** |
| B10 | Other out-of-scope paths unchanged | `git diff --stat 02bf930 HEAD -- soapui contracts .github/workflows` → empty | **PASSED** (SoapUI project unchanged, still targets `http://localhost:50355/TimeZoneConverter.svc`) |

## 2. Endpoint / client compatibility matrix (Strict XML parity vs golden)

Capture: `wcf-baseline/Capture-WcfWireBaseline.ps1` (unchanged) run against the **published output**. Comparison: `tasks/03-…/Compare-WcfWireCapture.ps1`. Evidence: [`runtime-capture-published-e1/`](runtime-capture-published-e1/) and [`runtime-capture-published-e1e2-http/`](runtime-capture-published-e1e2-http/).

| # | Endpoint / artifact | Result vs golden | Classification |
|---|---|---|---|
| E1-1 | POST `ConvertToLowerOffset` (SoapUI request bytes, SOAPAction `…/ConvertToOffsetRequest`) | **BYTES-EQUAL** (397 B). 200, `text/xml; charset=utf-8`, `<ToDateTime>2022-09-24T09:23:45.6780000+01:00` | PASSED |
| E1-2 | POST `ConvertToHigherOffset` | **BYTES-EQUAL**, `…15:23:45.6780000+07:00` | PASSED |
| E1-3 | POST `ConvertToSameOffset` | **BYTES-EQUAL**, `…10:23:45.6780000+02:00` | PASSED |
| E1-4 | POST `ConvertToNegativeOffset` | **BYTES-EQUAL**, `…02:23:45.6780000-06:00` | PASSED |
| E1-F | Fault probe (unknown SOAPAction) | **BYTES-EQUAL** (762 B). 500 `a:ActionNotSupported`, no `<detail>` | PASSED |
| E1-H | `X-Frame-Options: SAMEORIGIN`, status codes, Content-Type on every response | Identical to the golden manifest for all 10 responses | PASSED |
| E1-A | Reply Action `"ConvertToOffset"` (WCF-006) | Golden E1 has no `<s:Header>` and no Action on the wire, and the migrated responses match byte-for-byte. Test `ReplyAction_IsPreservedByResponseDispatchFormatter` asserts `Headers.Action == "ConvertToOffset"` | PASSED |
| M1-1 | `?xsd=xsd0` | **BYTES-EQUAL** | PASSED |
| M1-2 | GET `/TimeZoneConverter.svc` help page | **BYTES-EQUAL** | PASSED |
| M1-3 | `?wsdl`, `?wsdl=wsdl0`, `?singleWsdl` | **NSDECL-ORDER**: identical infoset (elements, attributes, prefixes, text, `soap:address http://localhost:50355/TimeZoneConverter.svc`, operations, soapAction, messages, schema types, same *set* of root `xmlns` declarations). Only the order of the declarations differs | **ACCEPTED CHANGE** (scenario-instructions Decision: namespace-declaration order has no XML meaning) |
| M1-4 | SOAP MEX | The original had no MEX endpoint: default endpoints only, `serviceMetadata` HTTP(S) GET only. `/TimeZoneConverter.svc/mex` GET returns 400 | N/A (no original surface; nothing removed) |
| M1-5 | Metadata over HTTPS (`HttpsGetEnabled=true` configured) | Configured in code. HTTPS exchange not run | **UNVERIFIED**: missing prerequisite is a **trusted HTTPS certificate / production HTTPS binding** (the existing dev cert is untrusted, `dotnet dev-certs https --check --trust` exit 7; no trust change or TLS bypass allowed) |
| E2-1 | E2 registration and WSDL (`--urls "http://localhost:50355;https://localhost:44355"`, existing dev cert, captured over HTTP) | E1 envelopes, fault, xsd and help page are still BYTES-EQUAL, and `wsdl-wsdl0` is NSDECL-ORDER. `?wsdl`/`?singleWsdl` add the expected port `BasicHttpsBinding_ITimeZoneConverter` at `https://localhost:44355/TimeZoneConverter.svc` + `sp:TransportBinding/HttpsToken RequireClientCertificate="false"`. This is byte-identical to the task 03 capture | PASSED (wiring). The structural match against a *Framework* E2 WSDL is **UNVERIFIED**: there is no golden E2 because the original dev host had no https binding. Missing prerequisite: the original host with an https site binding |
| E2-2 | E2 SOAP exchange over HTTPS | Not run | **UNVERIFIED**: missing prerequisite is a **trusted HTTPS certificate / production HTTPS binding** (owner input) |
| C-1 | SoapUI pointed unchanged at `http://localhost:50355/TimeZoneConverter.svc` | SoapUI project file unchanged (git). Its 4 requests replayed byte-for-byte with its SOAPAction against both the launch profile and the published output, and all were byte-identical to golden | PASSED (payload/URL level). **Running the real SoapUI app is UNVERIFIED**: missing prerequisite is a **SoapUI installation** |
| C-2 | Unchanged typed WCF client (`wcf-baseline/contracts-original` proxy) | The proxy was unused in-repo (WCF-007). No typed-client probe was run | **UNVERIFIED** (optional): missing prerequisite is an owner-identified typed/external consumer. External consumers are unknown (owner input) |
| X-1 | Application-exception fault (supplementary, out-of-range `FromOffset`) | 500 `a:InternalServiceFault`, generic faultstring, **no detail**. This matches `includeExceptionDetailInFaults=false` | PASSED (behavior). Parity is **UNVERIFIED**: the golden baseline doesn't contain this probe. Missing prerequisite: a capture of the same probe from the original net481 host |
| X-2 | Host-level HTTP headers | Golden (IIS) also sent `Server: Microsoft-IIS/10.0`, `X-AspNet-Version`, `X-Powered-By`, `X-SourceFiles` (IIS Express only) and `Cache-Control: private`, and used `Content-Length` on metadata. Migrated (Kestrel) sends `Server: Kestrel`, no `Cache-Control`, and chunked metadata | **ACCEPTED CHANGE** (host-level, recorded in task 03, outside the SOAP Strict XML parity scope). The owner may ask to reinstate `Cache-Control: private` through middleware (see §5) |
| D-1 | IIS in-process hosting of the published `web.config` (+ `EnableHttpsEndpoint=true` with an IIS https binding) | Not run | **UNVERIFIED**: missing prerequisites are an **IIS + ASP.NET Core Module V2 deployment target** and the deployment host decision (owner input) |
| D-2 | IIS Express launch profile | Not run (the Kestrel profile was run) | **UNVERIFIED**: missing prerequisite is the ASP.NET Core Module in IIS Express. Low impact, dev only |
| D-3 | CodeQL autobuild in CI | Local stand-ins B1 and B2 pass | **UNVERIFIED**: missing prerequisite is a **GitHub Actions `windows-latest` CodeQL run** on the branch (owner input) |
| D-4 | Non-Windows / container platform | Not run | **UNVERIFIED**: missing prerequisite is the deployment OS decision (owner input) |

**Matrix verdict:** E1 has Strict XML parity (all 4 SoapUI samples + fault byte-identical). M1 has parity (xsd and help page byte-identical; WSDL ×3 infoset-identical under the accepted xmlns-order decision). E2 wiring passes, and the E2/M1 HTTPS exchange is UNVERIFIED with the prerequisite named.

## 3. Finding reconciliation (WCF-001 … WCF-012)

| ID | Finding | Disposition | Evidence |
|---|---|---|---|
| WCF-001 | `aspNetCompatibilityEnabled` | **Done (task 03)**: removed, with no `AspNetCompatibilityRequirements` added | No `System.Web`/`HttpContext`/`System.ServiceModel` code in src48 (grep: only comments). Publish has no System.Web. Service answers SoapUI (E1-1…4) |
| WCF-002 | `.svc` activation | **Done (03)**: `TimeZoneConverter.svc` and its items removed; `TimeZoneConverter.svc.cs` kept; path `/TimeZoneConverter.svc` | No `.svc` file in src48; POST `/TimeZoneConverter.svc` works at the unchanged URL |
| WCF-003 | Default endpoints + protocolMapping | **Done (03/04)**: explicit E1 `BasicHttpBinding()`. E2 `BasicHttpBinding(Transport){Name="BasicHttpsBinding"}` gated by `TimeZoneConverter:EnableHttpsEndpoint` (auto/true/false) with fail-fast | E1 matrix PASSED; E2-1 wiring PASSED; B8 fail-fast PASSED; E2-2 UNVERIFIED (cert) |
| WCF-004 | Behaviors in config | **Done (03)**: `ServiceMetadataBehavior` HTTP+HTTPS GET; `IncludeExceptionDetailInFaults=false` | M1 served; faults E1-F/X-1 carry no detail; M1-5 HTTPS UNVERIFIED |
| WCF-005 | `[ResponseSerializer]` operation behavior / formatter | **Done (03)**: ported to CoreWCF; the attribute on the implementation method is honored, so no fallback was needed | `<ToDateTime>` with offset is byte-identical in all 4 responses; unit + wire tests pass |
| WCF-006 | Reply Action `"ConvertToOffset"`, request Action `…/ConvertToOffsetRequest` | **Preserved, not "fixed" (03)** | E1-A; request Action dispatches; unknown action faults identically |
| WCF-007 | Generated file mixing server contract + unused client; dead `<client>` config | **Done (03)**: server contract/messages split into CoreWCF types; `TimeZoneConverterClient`/channel and `<client>`/`<bindings>` removed; unchanged copy at `wcf-baseline/contracts-original/` | No `ClientBase`/`TimeZoneConverterClient` in src48; wire identity unchanged (matrix) |
| WCF-008 | Non-SDK WebApplication net481 / IIS Express | **Done (02→03)**: host conversion was folded into 03 per the plan's fallback. `Microsoft.NET.Sdk.Web` net10.0, `TimeZoneConvertor` names kept, stale `None` items/IIS Express props/transforms removed, X-Frame-Options moved to middleware, launchSettings `http://localhost:50355` | B1 0 warnings; B5 publish; E1-H header |
| WCF-009 | Test project non-SDK / MSTest 2.2.7 | **Done (02, 04)**: SDK-style (02, net481) then net10.0, MSTest 4.4.1 (04). Both original tests unchanged | B3: `WriteObject`/`ReadObject` pass (exact XML, incl. xsd/xsi order) |
| WCF-010 | No endpoint tests | **Done (04)**: `EndpointWireTests` with loopback Kestrel and golden-baseline assertions (SHA256-pinned), plus mutation-checked | B3: 22/22 |
| WCF-011 | Unused `RequestResponse*` pair | **Done (03)**: ported as-is to CoreWCF namespaces (code parity, no behavior change) | Both files present and compiling (B1/B2) |
| WCF-012 | Engine baseline unavailable; NuGet feed unreachable | **Partially resolved, remainder deferred.** Packages were verified live through the enabled proxy feed: restore succeeded for every task, `--outdated` shows none, `--vulnerable` shows none, and planning ran `get_supported_package_version` on 2026-10-01. **Deferred:** engine reassessment (`generate_dotnet_upgrade_assessment`) when the tools are available, and verification directly against `api.nuget.org` (TLS failure, disabled in the user NuGet.Config) | B4 |

All 12 findings map to a completed change or a recorded reason/deferral.

## 4. UNVERIFIED checks (prerequisite named)

1. **E2 SOAP exchange over HTTPS** (E2-2) and **M1 metadata over HTTPS** (M1-5). Needs a trusted HTTPS certificate or production HTTPS binding.
2. **E2 WSDL vs a Framework E2 golden** (E2-1 structural parity). Needs the original net481 host with an https site binding.
3. **Real SoapUI application run** (C-1). Needs a SoapUI installation. The payloads were replayed byte-for-byte instead.
4. **Unchanged typed / external WCF clients** (C-2). Needs the owner to identify consumers.
5. **Application-exception fault parity vs net481** (X-1). Needs an original-host capture of that probe. The behavior itself (no detail) is verified.
6. **IIS in-process hosting** of the publish output, incl. `EnableHttpsEndpoint=true` with an IIS https binding (D-1). Needs an IIS + ANCM V2 target.
7. **IIS Express launch profile** (D-2). Needs ANCM in IIS Express. Dev only.
8. **CodeQL autobuild in CI** (D-3). Needs a GitHub Actions run.
9. **Non-Windows / container runtime** (D-4). Needs the deployment OS decision.

## 5. Open owner inputs

- **External consumers**: are there any beyond the SoapUI project? Technology and owner? Can they tolerate the host-level header changes (X-2)?
- **Production HTTPS binding and certificate**, plus the per-environment value of `TimeZoneConverter:EnableHttpsEndpoint`. Set `true` under IIS in-process wherever the original site had an https binding. Under IIS *out-of-process* E2 cannot be served, and `true` fails startup by design.
- **Deployment host and OS**: IIS in-process (publish `web.config` is ready), Kestrel/service, or container.
- **CodeQL autobuild** on the net10.0 solution in CI (`windows-latest`). The repo also contains the out-of-scope `src60` solution, which autobuild may pick up as well.
- **`Cache-Control: private`** (X-2): reinstate via middleware if any intermediary relied on it.
- **Fault `xml:lang`**: the fault wire test compares `en-US`. A CI agent with a different OS culture could differ (noted in task 04).

## 6. Deferred items / follow-ups

- **WCF-012**: engine reassessment and direct `api.nuget.org` package re-verification.
- **Contract file is hand-maintained**: `Contracts/ITimeZoneConverter.cs` + `TimeZoneConverterMessages.cs` replace the SvcUtil-generated file, and `GenerateContracts.ps1` was removed. The original generated file is at `wcf-baseline/contracts-original/ITimeZoneConverter.cs` and in git history.
- **Optional (low)**: B8's real-process failure message comes from CoreWCF and doesn't name `TimeZoneConverter:EnableHttpsEndpoint`. A fix would be to validate before `app.Run()` (for example, `true` combined with no https URL in `urls`/`https_ports`/`Kestrel:Endpoints` → log the switch name), or catch and wrap the startup exception. Fail-fast behavior is already correct, so no source change was made in this validation task.
- **Optional**: a typed-client loopback test using `System.ServiceModel.Http` 10.0.0 with the preserved original proxy.
