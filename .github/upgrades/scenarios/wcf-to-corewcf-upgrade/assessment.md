# Assessment — WCF to CoreWCF Upgrade

Scope: `src48/TimeZoneConverter.sln` (2 projects) · Target: `net10.0` · Assessed 2026-10-01

## Contents

- [Engine Baseline](#engine-baseline)
- [WCF Migration Assessment](#wcf-migration-assessment)
- [Detailed Reports](#detailed-reports)

## Engine Baseline

> **Engine baseline unavailable (coverage gap).** The .NET engine tools
> (`generate_dotnet_upgrade_assessment`, `get_projects_info`, `get_project_dependencies`,
> `get_supported_package_version`, `query_dotnet_assessment`) were not available to the
> Assessor or the BreakGlass worker in this session (three tool-search attempts, no match).
> No engine `assessment.json` / `assessment.csv` was produced. This file was therefore
> written by the AI WCF pass; engine API-catalog, package-compatibility and dependency
> metrics are **not measured**. Re-run the engine assessment when the tools are available
> (explicit reassessment decision required) and reconcile with the WCF overlay.

Manually verified project facts (substitute, not engine data):

| Project | Kind | Current TFM | SDK-style | Package mgmt | Proposed |
|---------|------|-------------|-----------|--------------|----------|
| `src48/TimeZoneConverter/TimeZoneConverter.csproj` | ASP.NET WebApplication (WCF `.svc` host) | net481 | No | none (GAC refs) | net10.0, `Microsoft.NET.Sdk.Web` + CoreWCF |
| `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj` | MSTest unit tests | net481 | No | packages.config (MSTest 2.2.7, not restored) | net10.0 SDK-style MSTest |

Dependency order: TimeZoneConverter → TimeZoneConverterTest. No shared props, no `global.json`.
`src60/` is excluded by owner rule and was not read.

## WCF Migration Assessment

*AI WCF findings* (not engine measurements). Full inventory: [assessment/wcf/inventory.md](assessment/wcf/inventory.md).

- **Readiness:** Ready with changes. One SOAP service (`TimeZoneConverterService`, 1 operation `ConvertToOffset`, ns `http://bartjolling.github.io/`), `.svc`-activated with default basicHttp (None) + basicHttps (Transport) endpoints and HTTP(S) GET metadata.
- **Coverage:** 2/2 projects, 1 service, 2 effective endpoints + metadata, 1 (dead) client endpoint, 6 custom extensibility/serializer types; WCF scan 0 parse errors.
- **Blocker resolution:** `WCFSCAN-HOST-ASPNETCOMPAT` (Web.config:34) — no `HttpContext`/`System.Web` code dependency found → remove the setting (WCF-001).
- **Key risks:** custom `IOperationBehavior` attribute on the implementation method replacing the XmlSerializer reply formatter (WCF-005); hard-coded reply action `"ConvertToOffset"` and request Action `…/ConvertToOffsetRequest` must be preserved (WCF-006).
- **Retained-client impact:** no in-repo .NET consumer; generated `TimeZoneConverterClient` unused and `<client>` config unresolvable (contract-name mismatch) → remove from server (WCF-007). SoapUI net48 project is a raw-XML consumer → recommend **Strict XML parity**.
- **Packages:** `CoreWCF.Primitives` + `CoreWCF.Http` 1.9.1 (support matrix 2026-09-30; live NuGet lookup failed — TLS error); no `CoreWCF.ConfigurationManager`.
- **Default decisions (Automatic mode):** In-place host; code-based configuration; Strict XML parity; remove unused client class; Accept unverified for HTTPS/original-host capture if prerequisites are missing.
- **Open owner inputs:** external consumers, production HTTPS binding/certificate, deployment host/OS.

## Detailed Reports

- [WCF inventory](assessment/wcf/inventory.md)
- [WCF scan (scan.json)](assessment/wcf/scan.json)
- [Scenario instructions](scenario-instructions.md)
