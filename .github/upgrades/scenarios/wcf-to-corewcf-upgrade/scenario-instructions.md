# WCF to CoreWCF Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Scope**: src48/TimeZoneConverter.sln (all projects under src48/ only)
- **Target Framework**: .NET 10 (LTS) — net10.0 (recommended)

## Source Control
- **Source Branch**: deepchoudhery-fantastic-goggles
- **Working Branch**: deepchoudhery-fantastic-goggles
- **Commit Strategy**: After Each Task
- **Branch Sync**: Disabled

## User Preferences

### Technical Preferences
- Only upgrade projects under `src48/`. Do NOT read, copy from, or use `src60/` as a reference; `src60/` is an existing hand-written port and must stay untouched.
- Use absolute paths for any project-path tool arguments; call option-lookup tools one at a time, never in parallel.

### Execution Style
- Use the full scenario workflow (assessment, planning, options, task list, execution) so each stage can be reviewed.
- When the scenario asks for a decision, choose the recommended/default option and record the choice in the workflow artifacts.

## Decisions
- Target framework: net10.0 (recommended LTS) — chosen by default per user rule.
- Flow mode: Automatic (user asked for defaults to be chosen without pausing).
- Working branch: stay on the existing session worktree branch `deepchoudhery-fantastic-goggles` (no new branch). Branch Sync disabled because source and working branch are the same.
- Commit strategy: After Each Task (default).

## Upgrade Options

### Strategy
- Upgrade Strategy: Bottom-Up

### Project Structure
- Project Approach: In-place (non-WCF projects: TimeZoneConverterTest)
- WCF Host Approach: In-place

### Compatibility
- Unsupported API Handling: Fix Inline
- WCF Wire Compatibility: Strict XML parity

### Modernization
- Nullable Reference Types: Leave Disabled
- WCF Configuration: Code and application configuration (CoreWCF.ConfigurationManager not used)

### Reliability
- WCF Validation Environment: Owner provides
  Addendum (Orchestrator): session runs autonomously; prerequisites already present on the machine (VS 18 MSBuild, WebApplication targets, .NET 10 SDK) may be used. Any check whose prerequisite is still missing when reached is recorded as UNVERIFIED with the missing prerequisite named and reported at completion — neither silently waived nor indefinitely blocking.

Not offered (triggers not met): Test Coverage, System.Web Adapters, Package Management, Configuration Migration.

## Strategy
**Selected**: Bottom-Up
**Rationale**: 2 net481 projects, 2-tier graph (TimeZoneConverterTest → TimeZoneConverter); Framework→Core boundary with different mechanics per project.

### Execution Constraints
- Order: 01 prerequisites + golden baseline → 02 SDK-style conversion (net481) → 03 CoreWCF host (Tier 1) → 04 test project net10.0 (Tier 2) → 05 final validation. Tier 1 must validate before Tier 2 starts.
- SDK-style conversion stays on net481 and is separate from TFM/hosting changes; if the WebApplication host can't convert on net481, revert only the host conversion and fold its format change into 03 (record why).
- Between-tier exception: the net481 test project can't reference the net10.0 host, so after 03 it is expected not to build; 03 is validated by host build + runtime wire checks, and 04 follows immediately.
- Runtime parity is a completion gate, not only a clean build: every endpoint check is compared with the golden baseline from 01 (Strict XML parity: Action, namespaces, element names/order, xsi/xsd prefixes, ToDateTime value).
- Checks needing a missing prerequisite are UNVERIFIED (prerequisite named), never passed or skipped; no certificate creation, trust-store changes, URL reservations or global installs.

### Package Versions (get_supported_package_version, 2026-10-01)
- TimeZoneConverter: CoreWCF.Primitives 1.9.1, CoreWCF.Http 1.9.1 (no CoreWCF.ConfigurationManager)
- TimeZoneConverterTest: MSTest 4.4.1 (metapackage); Microsoft.AspNetCore.Mvc.Testing 10.0.12 if WebApplicationFactory is used; System.ServiceModel.* 10.0.0 only for client-role tests

### WCF Invariants and Decisions
- Endpoints: E1 BasicHttpBinding (Security None) at `/TimeZoneConverter.svc`; E2 BasicHttpBinding Transport (only where an HTTPS binding exists); M1 metadata HTTP+HTTPS GET; includeExceptionDetailInFaults=false. Dev URL `http://localhost:50355` kept in launchSettings.json.
- Preserve exactly: reply Action `"ConvertToOffset"` (WCF-006), request Action `http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest`, namespace `http://bartjolling.github.io/`, XmlSerializerFormat + wrapped message contracts, custom `<ToDateTime>` serializer output (WCF-005). Do not "fix".
- Keep AssemblyName/RootNamespace `TimeZoneConvertor`.
- Remove aspNetCompatibilityEnabled (WCF-001), `.svc` activation file (WCF-002), dead `<client>`/`<bindings>` config and unused `TimeZoneConverterClient` from the server (WCF-007, D-4; unchanged baseline copy saved in 01). Contract file becomes hand-maintained.
- Preserved clients: SoapUI net48 project (raw XML, unchanged) is the validation oracle; external consumers unknown (owner input).
- Scope exclusions: `src60/` and `soapui/*net60*` not read or changed. Not applicable: System.Web Adapters, YARP, cookie sharing.
- Owner inputs at completion: external consumers, production HTTPS binding/certificate, deployment host/OS, CodeQL autobuild in CI; deferred: engine reassessment (WCF-012).

## Build Tool Decisions
- **TimeZoneConverter.csproj** (legacy WebApplication, net481): msbuild.exe (VS 18 MSBuild with WebApplication targets) — still non-SDK-style after task 02 (host SDK conversion fell back to task 03); re-evaluate in task 03 (`Microsoft.NET.Sdk.Web` net10.0 → `dotnet build`).
- **TimeZoneConverterTest.csproj** (SDK-style since task 02, net481, PackageReference MSTest 2.2.7, no Microsoft.NETFramework.ReferenceAssemblies): msbuild.exe `/restore` (solution build, because it references the legacy WebApplication host); tests via vstest.console.exe on `bin\Debug\net481\TimeZoneConverterTest.dll` (no Microsoft.NET.Test.Sdk, so not `dotnet test`) — re-evaluate in task 04 (net10.0).
- NuGet: api.nuget.org disabled/unreachable on this machine; restores go through the enabled `azure-default` proxy feed (packagefeedproxy.microsoft.io).
