# 01-validation-environment-and-wire-baseline: Confirm prerequisites, baseline build/tests and capture the golden wire baseline

Confirm the validation prerequisites the WCF Validation Environment option lists before anything changes: .NET 10 SDK (10.0.203/10.0.401 present; no `global.json`), VS 18 MSBuild with `Microsoft.WebApplication.targets` and the .NET Framework 4.8.1 targeting pack, NuGet feed reachability (`api.nuget.org` failed with a TLS error during assessment, WCF-012), IIS Express for the original host on `http://localhost:50355`, and any HTTPS dev certificate that already exists for E2. Do not create certificates, change trust stores, add URL reservations or install anything globally. For each prerequisite, record whether it is available. Any check that depends on a missing prerequisite is marked **UNVERIFIED** with the prerequisite named. It is not passed, and the session does not wait on it.

Build `src48/TimeZoneConverter.sln` on net481 and run the 2 MSTest tests (needs MSTest 2.2.7 restore; `src48/packages` does not exist yet). Capture the golden wire baseline from the original host: `?wsdl`, `?singleWsdl`, every `?xsd=xsdN`, plus the request and response envelopes for each of the 4 SoapUI samples in `soapui/TimeZoneConverter-net48-soapui-project.xml`. The baseline must include the reply Action `ConvertToOffset` (WCF-006), request Action `http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest`, the `xsi`/`xsd` prefixes and the `<ToDateTime>` offset format (WCF-005). If the Framework host cannot run, record the reduced-fidelity offline baseline the inventory defines: the SoapUI cached WSDL/XSD, `contracts/*.wsdl|xsd`, and the expected XML in `ConversionReponseSerializerTest.cs:18`. Also save an unchanged copy of `Contracts/ITimeZoneConverter.cs` (server contract + generated client, D-4) under the workflow folder. #skill:migrating-wcf-to-corewcf

**Done when**: A prerequisite checklist (available / missing → UNVERIFIED) and the baseline artifacts (net481 build result, test result, golden metadata and envelopes, or the labeled offline baseline) are stored under the workflow folder. No source files are changed.

---

## Research findings (TaskExecutor, 2026-10-01, before any action)

### Confirmed scope (actual repo)
- `src48/TimeZoneConverter.sln` → 2 projects + solution folder `contracts` (`../contracts/TimeZoneConverter.wsdl|xsd`).
- `src48/TimeZoneConverter/TimeZoneConverter.csproj`: legacy WebApplication (`{349c5851…}`), `TargetFrameworkVersion v4.8.1`, `AssemblyName`/`RootNamespace` `TimeZoneConvertor`, `OutputPath bin\` (Debug+Release), imports `$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets`, IIS Express `IISUrl http://localhost:50355/`, `IISExpressSSLPort` empty (no HTTPS binding in dev → E2 not active in original dev host). GAC refs only, no NuGet. 9 `.cs` compile items + `.svc`, `Web.config` (+Debug/Release transforms).
- `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj`: legacy MSTest, v4.8.1, `packages.config` MSTest.TestAdapter/TestFramework **2.2.7**, `OutputPath bin\Debug\`; `EnsureNuGetPackageBuildImports` errors if `src48/packages` missing (it is missing today).
- Tests: `ConversionReponseSerializerTest.WriteObject` / `.ReadObject` (2). Expected XML at line 18 (offline oracle).
- Wire-relevant source: `ResponseDispatchFormatter.cs:29` reply Action `"ConvertToOffset"`; `ResponseSerializerAttribute` on `TimeZoneConverter.svc.cs:9`; `ConvertToOffsetResponseSerializer` adds `xsi`,`xsd` prefixes and emits `ToDateTimeOffset` as `<ToDateTime>`.
- SoapUI oracle `soapui/TimeZoneConverter-net48-soapui-project.xml`: endpoint `http://localhost:50355/TimeZoneConverter.svc`, SOAP 1.1, soapAction `http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest`, WS-A not enabled (`wsaVersion=NONE`). 4 calls, all `FromDateTime 2022-09-24T10:23:45.678`, `FromOffset 120`: ConvertToLowerOffset (To 60), ConvertToHigherOffset (420), ConvertToSameOffset (120), ConvertToNegativeOffset (-360). 3 of 4 CDATA bodies contain the SoapUI line-end marker literal `\r` before each newline (SoapUI's own CR encoding) → replay normalizes `\r`+LF to CRLF, as SoapUI sends; ConvertToHigherOffset is LF-only. SoapUI cached `?singleWsdl` is in the project file (cross-check source).

### Prerequisite probe results (read-only)
| Prerequisite | Result |
|---|---|
| .NET SDK | 8.0.425, 10.0.203, 10.0.400-preview, 10.0.401; no `global.json` anywhere in repo → available |
| VS MSBuild | `C:\Program Files\Microsoft Visual Studio\18\Main\MSBuild\Current\Bin\MSBuild.exe` (vswhere -latest -prerelease) → available |
| WebApplication targets | present in VS 18 Main (`v18.0\WebApplications\Microsoft.WebApplication.targets`) and IntPreview → available |
| .NET Framework 4.8.1 targeting pack | `Reference Assemblies\...\.NETFramework\v4.8.1` present → available |
| NuGet feeds | `api.nuget.org`: TLS handshake failure (same as WCF-012) and **disabled** in user NuGet.Config; `azure-default` `https://packagefeedproxy.microsoft.io/nuget/v3/index.json` reachable (HTTP 200) and enabled; VS offline source has mstest.testadapter/testframework folders → restore expected to work through azure-default |
| IIS Express | `C:\Program Files\IIS Express\iisexpress.exe` (+x86) present; PersonalWebServer applicationhost template present; no iisexpress process running before the task; port 50355 free |
| HTTPS dev certs (existing, not created) | CurrentUser\My `CN=localhost` ASP.NET Core HTTPS dev cert (valid to 2027-05-18, **not trusted** per `dotnet dev-certs https --check --trust`); LocalMachine\My `CN=localhost` IIS Express Development Certificate (to 2031-05-12) |
| vstest | `...\18\Main\Common7\IDE\Extensions\TestPlatform\vstest.console.exe` |

### Execution approach (no source edits)
1. Build: `MSBuild.exe src48\TimeZoneConverter.sln /restore /p:RestorePackagesConfig=true /p:Configuration=Debug /v:minimal` with file logger into `wcf-baseline/build/`. Restore writes only `src48/packages` (gitignored `**/[Pp]ackages/*`). Record package source/hash provenance for MSTest 2.2.7.
2. Tests: `vstest.console.exe TimeZoneConverterTest\bin\Debug\TimeZoneConverterTest.dll /logger:trx` → `wcf-baseline/build/`; require 2 discovered/2 executed.
3. Original host: IIS Express with a **private** `/config:` copy of the PersonalWebServer template placed under `%TEMP%` (site physicalPath `src48\TimeZoneConverter`, binding `http/*:50355:localhost`, Clr4IntegratedAppPool) — no user-profile/global IIS Express config, no URL reservation, no cert binding. Track the PID and stop it with `Stop-Process -Id`.
4. Capture to `wcf-baseline/metadata/`: `?wsdl`, `?singleWsdl`, every `?xsd=xsdN` referenced by `?wsdl` (raw bytes + URL/time/status/headers manifest). Capture to `wcf-baseline/envelopes/`: exact request bytes + response bytes/status/headers per SoapUI sample (Content-Type `text/xml; charset=UTF-8`, `SOAPAction: "…/ConvertToOffsetRequest"`), plus a supplementary unknown-SOAPAction fault probe (faults baseline, `includeExceptionDetailInFaults=false`). Cross-check live `?singleWsdl` against the SoapUI cache.
5. E2/HTTPS: original dev host has no HTTPS binding (`IISExpressSSLPort` empty) → E2 golden capture recorded UNVERIFIED (prerequisite: original HTTPS site binding; production binding is owner input). No binding is added.
6. Save unchanged `Contracts/ITimeZoneConverter.cs` (+ SHA256) to `wcf-baseline/contracts-original/` (D-4). Fallback if host fails: offline baseline (SoapUI cache, `contracts/*`, test line 18) labeled reduced fidelity.

### Decomposition verdict
Loaded `execution.md` (Decomposition Rules; no separate Breakdown Hints files in this scenario) and skill decomposition sections → **atomic**: single validation/baseline unit, no source edits, scope fully pinned, no internal decision point.
