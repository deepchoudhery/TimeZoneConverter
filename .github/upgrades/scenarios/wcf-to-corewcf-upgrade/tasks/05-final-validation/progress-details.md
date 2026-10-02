# Progress — 05-final-validation

## Result (2026-10-01)

**Outcome: done. Validation only, no source files changed.** Full report: [completion-report.md](completion-report.md).

Decomposition verdict: **atomic**. I evaluated scenario `execution.md` (small-cohesive-wcf exception: one host, validation-only scope, no open decision). The scenario folder has no Breakdown Hints files, and no scenario extensions apply at Execution scope. task.md was enriched (section "Research") before the first command.

### Results
- **Build**: `dotnet clean` → `dotnet restore --force` → `dotnet build src48\TimeZoneConverter.sln -c Release --no-incremental` gave **0 warnings, 0 errors**. VS 18 `MSBuild.exe … /restore /t:Rebuild /p:Configuration=Release` (local stand-in for CodeQL autobuild) exited 0 with no warnings.
- **Tests**: `dotnet test … -c Release` gave **22/22 passed** (22 discovered and executed, 0 failed, 0 skipped). TRX: [final-release.trx](final-release.trx). `src48/TimeZoneConverterTest/TestResults` was removed.
- **Packages**: `--vulnerable --include-transitive` found none; `--outdated` found none (proxy feed); restore produced no NU1608.
- **Publish**: `dotnet publish … -c Release -o %TEMP%\tzc-final-publish` succeeded. Contents: the Sdk.Web `web.config` (AspNetCoreModuleV2, `.\TimeZoneConvertor.dll`, `hostingModel="inprocess"`), runtimeconfig for NETCore + AspNetCore 10.0, and CoreWCF 1.9.1.31. There are no System.ServiceModel/System.Web/stub assemblies. The publish folder was **deleted** after validation; nothing was written to tracked paths.
- **Compatibility matrix** (published output on `http://localhost:50355`, capture script unchanged, compared with the task 03 comparator), evidence in `runtime-capture-published-e1/`:
  - 4 SoapUI responses and the fault probe are **BYTES-EQUAL**; status, Content-Type and X-Frame-Options match the golden manifest.
  - `xsd0` and the help page are **BYTES-EQUAL**.
  - `wsdl`, `wsdl0` and `singleWsdl` are **NSDECL-ORDER** (accepted decision).
- **E2 wiring** (`runtime-capture-published-e1e2-http/`, existing untrusted dev cert, all requests over HTTP): E1 is unchanged. The WSDL adds the `BasicHttpsBinding_ITimeZoneConverter` port, identical to the task 03 capture. The E2/M1 HTTPS exchange is **UNVERIFIED**: no trusted certificate (`dev-certs --check --trust` exit 7).
- **Launch profile** `TimeZoneConverter` → `http://localhost:50355`: the 4 SoapUI replays are byte-identical to golden.
- **Fail-fast** (`EnableHttpsEndpoint=true`, HTTP only, published binary): "Hosting failed to start" and a non-zero exit. The message comes from CoreWCF and does not name the switch (see the low-severity follow-up in the report). No source change was made.
- **Supplementary**: an app-exception fault (out-of-range offset) returns `a:InternalServiceFault` with no detail. There is no golden capture for it, so its parity is UNVERIFIED. MEX did not exist originally, so it is N/A.
- **src60 / out of scope**: `git diff --stat 02bf930 HEAD -- src60` is empty, `git log 02bf930..HEAD -- src60` is empty, and `git status --porcelain --ignored -- src60` is empty. `soapui`, `contracts` and `.github/workflows` are unchanged. I didn't open any src60 contents.
- **WCF-001…012**: all mapped (report §3). WCF-012 is partly resolved through the proxy feed; engine reassessment and nuget.org-direct verification are deferred.

### Files created/modified (workflow folder only)
- `tasks/05-final-validation/task.md` (research section), `progress-details.md`, `completion-report.md`, `final-release.trx`
- `tasks/05-final-validation/runtime-capture-published-e1/` and `runtime-capture-published-e1e2-http/` (manifest + metadata + envelopes evidence)
- No changes under `src48/` source, `src60/`, `soapui/` or `contracts/`. `bin/obj` are gitignored. Nothing was committed.

### Processes owned and stopped
| PID | What | Stopped / verified |
|---|---|---|
| 56404 | published host, `--urls http://localhost:50355` (shell `pubhost-e1`) | stop_powershell; PID gone, port free |
| 34736 | published host, http + https:44355 (shell `pubhost-e1e2`) | stop_powershell; PID gone, ports free |
| 41952, 49208 | published host fail-fast runs (`Start-Process`) | exited by themselves (non-zero), verified gone |
| 49696 | `TimeZoneConvertor.exe` via `dotnet run --launch-profile` (shell `profilehost`) | stop_powershell; PID gone, port free |
| 15084 | VBCSCompiler started by my MSBuild.exe build | `Stop-Process -Id 15084`, verified gone |

Pre-existing build-server processes that I did not start were left alone: dotnet MSBuild node 57256 (4:57 PM) and VBCSCompiler 35208 (5:17 PM). No IIS/IIS Express was started. No certificates, trust stores or URL reservations were touched. The env var `TimeZoneConverter__EnableHttpsEndpoint` was set only inside one short-lived shell.
