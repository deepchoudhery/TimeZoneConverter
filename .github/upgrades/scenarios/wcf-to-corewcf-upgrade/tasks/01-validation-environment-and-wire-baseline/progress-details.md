# Progress — 01-validation-environment-and-wire-baseline

## Checkpoint 1 (2026-10-01, before net481 build)
- Last verified: prerequisite probes (see task.md research); task.md enriched.
- Pending: `MSBuild.exe src48\TimeZoneConverter.sln /restore /p:RestorePackagesConfig=true /p:Configuration=Debug`, log `wcf-baseline/build/msbuild-net481.log`.
- Processes owned: none.

## Checkpoint 2 (2026-10-01 16:37, golden capture running)
- Last verified: net481 build 0 warnings/0 errors; vstest 2/2 passed.
- Live host: IIS Express **PID 50336** (`C:\Program Files\IIS Express\iisexpress.exe`, private config `%TEMP%\tzc-wcf-baseline-iisx\config\applicationhost.config`, site `TimeZoneConverter`, `http://localhost:50355/`). Must be stopped by `Stop-Process -Id 50336`.
- Pending: capture script writing to `wcf-baseline/metadata` and `wcf-baseline/envelopes`.

## Final result (2026-10-01)

**Outcome: done — LIVE golden baseline captured from the original net481 host.** Full detail: [`../../wcf-baseline/README.md`](../../wcf-baseline/README.md).

### Prerequisite checklist
| Prerequisite | Status |
|---|---|
| .NET 10 SDK (10.0.203/10.0.401, no global.json) | Available |
| VS 18 MSBuild + Microsoft.WebApplication.targets | Available |
| .NET Framework 4.8.1 targeting pack | Available |
| NuGet feed | Available via enabled `azure-default` proxy (`packagefeedproxy.microsoft.io`, HTTP 200); `api.nuget.org` TLS failure + disabled in user NuGet.Config (WCF-012 still true for nuget.org) |
| IIS Express | Available (used for golden capture) |
| vstest.console (VS 18) | Available |
| HTTPS cert for E2 | Existing certs only: ASP.NET Core dev cert (untrusted), IIS Express dev cert. Original project has no HTTPS binding → **E2 golden + M1-over-HTTPS = UNVERIFIED** (missing prerequisite: HTTPS site binding on the original host / owner-provided production binding) |
| SoapUI application | Not run → real-SoapUI-client check **UNVERIFIED**; payloads replayed by script |

### Build / tests (net481)
- `MSBuild.exe src48\TimeZoneConverter.sln /restore /p:RestorePackagesConfig=true /p:Configuration=Debug` → **0 warnings, 0 errors**. MSTest 2.2.7 restored into `src48/packages` (gitignored) from the proxy feed; nupkg hashes recorded.
- vstest → **2 discovered / 2 executed / 2 passed** (`WriteObject`, `ReadObject`). TRX: `wcf-baseline/build/net481-baseline.trx`.

### Golden capture
- Original host: IIS Express with private config in `%TEMP%` (since deleted), `http://localhost:50355/`, PID 50336 — **stopped** (`Stop-Process -Id 50336`), port refuses connections afterwards, no `iisexpress` process remains.
- Metadata: `?wsdl`, `?wsdl=wsdl0`, `?xsd=xsd0` (only schema), `?singleWsdl`, help page — all 200.
- Envelopes: 4 SoapUI samples → 200 with expected `<ToDateTime>` values; supplementary unknown-action fault probe → 500 `ActionNotSupported`, no detail.
- Unchanged `Contracts/ITimeZoneConverter.cs` copied to `wcf-baseline/contracts-original/` (SHA256 matches source).
- Reusable capture script: `wcf-baseline/Capture-WcfWireBaseline.ps1` (for tasks 03/05; use a different `-OutputRoot`).

### Evidence that refines the plan (for orchestrator / later tasks)
1. **WCF-006 reply Action is not on the wire for E1**: BasicHttpBinding (SOAP 1.1, AddressingNone) responses have no `<s:Header>` and no Action header. Strict wire parity for the reply action = "no header/no Action"; the `"ConvertToOffset"` literal remains a code-parity item only (checkable by code review or a message inspector, not on the wire).
2. **SoapUI cached WSDL is stale** in one place: `ToDateTimeOffset` is `tns:DateTimeOffset minOccurs=1` in the cache vs `xs:string minOccurs=0` live. Live capture is authoritative.
3. `?wsdl` is split (service WSDL in `http://tempuri.org/` importing `?wsdl=wsdl0`); `?singleWsdl` is flat in `http://bartjolling.github.io/`. CoreWCF metadata layout must be compared against both.
4. Response headers include `X-Frame-Options: SAMEORIGIN` (must be preserved); `Server`/`X-AspNet-Version`/`X-Powered-By`/`X-SourceFiles` are IIS-host-specific.

### Files created/modified (no source files changed)
- `tasks/01-…/task.md` (research enrichment), `tasks/01-…/progress-details.md`
- `scenario-instructions.md` (appended `## Build Tool Decisions`)
- `wcf-baseline/README.md`, `wcf-baseline/Capture-WcfWireBaseline.ps1`, `wcf-baseline/capture-manifest.json`
- `wcf-baseline/build/msbuild-net481.build-log.txt` (renamed from `.log` because `*.log` is gitignored), `wcf-baseline/build/net481-baseline.trx`
- `wcf-baseline/metadata/{wsdl.xml,wsdl-wsdl0.xml,xsd-xsd0.xsd,singleWsdl.xml,service-help-page.html}`
- `wcf-baseline/envelopes/{ConvertToLowerOffset,ConvertToHigherOffset,ConvertToSameOffset,ConvertToNegativeOffset,FaultProbe-UnknownAction}.{request,response}.xml`
- `wcf-baseline/contracts-original/ITimeZoneConverter.cs`
- Untracked/ignored build side effects: `src48/packages/`, `src48/*/bin`, `src48/*/obj` (all gitignored).
- `git status`: no changes to tracked files; `src60/` not touched or read.

### Processes owned at return
None. IIS Express PID 50336 stopped and verified gone.
