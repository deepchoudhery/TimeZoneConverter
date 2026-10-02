# 05-final-validation: Solution-wide validation, compatibility matrix and completion report

Validate the whole migrated scope: a clean build of `src48/TimeZoneConverter.sln`, all tests, and `dotnet publish` of the host producing a runnable ASP.NET Core output (Sdk.Web `web.config` for IIS in-process included). Re-run the endpoint/client compatibility matrix against the golden baseline. That covers E1 with all 4 SoapUI samples, M1 metadata over HTTP (and HTTPS when bound), E2 Transport where an existing certificate allows, and SoapUI pointed unchanged at `http://localhost:50355/TimeZoneConverter.svc`. Check that `src60/` is unchanged. #skill:migrating-wcf-to-corewcf

The completion report lists every check as passed, accepted change, or **UNVERIFIED (missing prerequisite named)**. It also lists the open owner inputs: external consumers, the production HTTPS binding and certificate, the deployment host and OS (IIS in-process / Kestrel / container), and whether CodeQL autobuild works on the net10.0 solution in CI. Deferred items are listed too: engine reassessment and live package re-verification (WCF-012), and the fact that the contract file is now hand-maintained.

**Done when**: Build, tests and publish succeed. Every assessed finding WCF-001…WCF-012 maps to a completed change or a recorded reason. The compatibility matrix shows Strict XML parity for E1/M1 (E2 passed or UNVERIFIED). No check is silently waived. The completion report records the UNVERIFIED checks and the owner inputs.

---

## Research (2026-10-01, written before any validation command or edit)

### Skills / stage guidance loaded
- Scenario `SKILL.md` + `execution.md` (§ Validation and Completion: endpoint/client matrix, publish output/config, startup/shutdown/ports, process ownership, passed/failed/unverified distinction, reconcile every finding). Scenario folder has no separate Breakdown Hints files (`assessment.md, execution.md, options.md, planning.md, SKILL.md, support-matrix.md, scripts/Get-WcfInventory.ps1`). Scenario extensions at Execution scope: none apply.
- `migrating-wcf-to-corewcf` SKILL.md + `ref/build-and-conversion-safeguards.md` (final solution build/test gate, no NU1608/downgrade, dependency provenance: runtime/publish graph has no stubs; `-r` is not restore).
- `building-projects` (Build Tool Decisions cached in scenario-instructions: `dotnet build`/`dotnet test`; full solution build is the final gate; clean build = `--no-incremental`; all warnings fixed, none suppressed; output under `net10.0/`).

### Decomposition verdict: atomic
`execution.md` small-cohesive-wcf exception: one host, one service/operation, a known validation-only scope (no source change planned), no open design/security decision (E2 switch and WSDL xmlns-order decision already recorded), SDK checkpoint already done in 02/03. Validation checkpoints are sequential but don't need persisted subtasks.

### Current repo state (confirmed)
- HEAD `8f87ac3` (task 04). Pre-upgrade base `02bf930`. Working tree: only workflow `scenario.json`/`tasks.md` modified (orchestrator).
- Tracked `src48/` files (39): host `TimeZoneConverter.csproj` (`Microsoft.NET.Sdk.Web`, net10.0, Exe, AssemblyName/RootNamespace `TimeZoneConvertor`, `GenerateAssemblyInfo=false`, CoreWCF.Primitives/Http 1.9.1), `Program.cs`, `HttpsEndpointStartupCheck.cs`, `appsettings.json` (`TimeZoneConverter:EnableHttpsEndpoint=auto`), `Properties/launchSettings.json` (`http://localhost:50355`), contracts (`ITimeZoneConverter.cs` hand-maintained server contract, `TimeZoneConverterMessages.cs`, `TimeZoneConversionResponse.cs`), 4 extensibility classes, 2 serializers, `TimeZoneConverter.svc.cs`. Test `TimeZoneConverterTest.csproj` (net10.0, MSTest 4.4.1, Mvc.Testing 10.0.12, FrameworkReference Microsoft.AspNetCore.App), `ConversionReponseSerializerTest.cs`, `EndpointWireTests.cs`, `WireBaseline/` (golden copies).
- `git diff --stat 02bf930 HEAD -- src60` → empty; `git status --porcelain --ignored -- src60` → empty. `soapui/`, `contracts/`, `.github/workflows/` also unchanged since base. SoapUI net48 endpoint (all 4 calls) = `http://localhost:50355/TimeZoneConverter.svc`, action `…/ConvertToOffsetRequest`.
- `.gitignore` covers `publish/`, `bin/obj`, `*.log`, `TestResults`. CodeQL: `windows-latest` `codeql-action/autobuild@v4` (no explicit build steps).
- SDKs: 8.0.425, 10.0.203, 10.0.401 (+10.0.400 preview); no global.json → 10.0.401.
- Ports 50355/44355 free; no `TimeZoneConvertor`/`iisexpress`/`testhost` process running.

### Validation plan (commands, single-line)
1. Clean build: `dotnet clean src48\TimeZoneConverter.sln -c Release` then `dotnet build src48\TimeZoneConverter.sln -c Release --no-incremental` (expect 0W/0E). Extra CodeQL-autobuild proxy: VS 18 `msbuild.exe src48\TimeZoneConverter.sln /restore /t:Rebuild /p:Configuration=Release /v:minimal`.
2. Tests: `dotnet test src48\TimeZoneConverterTest\TimeZoneConverterTest.csproj -c Release --no-build --logger "trx;LogFileName=…"` → expect 22 discovered/executed/passed; TRX copied into this task folder; `src48/**/TestResults` removed.
3. Package health: `dotnet list … package --vulnerable --include-transitive` and `--outdated` (partial live re-verification for WCF-012 via the proxy feed).
4. Publish: `dotnet publish src48\TimeZoneConverter\TimeZoneConverter.csproj -c Release -o %TEMP%\tzc-final-publish` (outside repo). Inspect: `web.config` (ANCM `aspNetCore` handler, `hostingModel="inprocess"`, `.\TimeZoneConvertor.dll`), `TimeZoneConvertor.dll/.exe`, `appsettings.json`, `runtimeconfig.json` (Microsoft.AspNetCore.App 10.0), CoreWCF 1.9.1 dlls, **no** `System.ServiceModel.*`/`System.Web*` dlls.
5. Compatibility matrix from the **published output** (`dotnet TimeZoneConvertor.dll --urls http://localhost:50355`, content root = publish dir): `wcf-baseline/Capture-WcfWireBaseline.ps1 -BaseUrl http://localhost:50355/TimeZoneConverter.svc -SoapUiProject soapui\TimeZoneConverter-net48-soapui-project.xml -OutputRoot tasks\05-final-validation\runtime-capture-published-e1` → `tasks\03-…\Compare-WcfWireCapture.ps1 -GoldenRoot wcf-baseline -CaptureRoot …`. Expect envelopes/xsd/help BYTES-EQUAL, wsdl ×3 NSDECL-ORDER (accepted decision). Also check `X-Frame-Options` and status codes against the manifest.
6. E2: published output with `--urls "http://localhost:50355;https://localhost:44355"` (existing dev cert only; no creation/trust change) → capture over HTTP; WSDL must add the BasicHttpsBinding port. Real HTTPS exchange stays UNVERIFIED unless `dotnet dev-certs https --check --trust` reports trusted (read-only check; never bypass TLS validation). Also confirm the fail-fast `EnableHttpsEndpoint=true` + http-only path on the published binary.
7. Launch profile sanity: `dotnet run --no-build -c Release --launch-profile TimeZoneConverter` binds `http://localhost:50355` (SoapUI URL unchanged).
8. Every host process is started in an owned async shell, its PID recorded, then stopped via `stop_powershell`/`Stop-Process -Id <pid>` and verified gone (port free).
9. Map WCF-001…WCF-012 → change/evidence; write `completion-report.md` (this folder) + `progress-details.md`.

### Expected no-source-change
Only fix source if a check reveals a real defect; any such fix is reported separately.
