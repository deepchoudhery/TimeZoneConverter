# 02-sdk-style-conversion: Convert both projects to SDK-style on net481

Convert `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj` and `src48/TimeZoneConverter/TimeZoneConverter.csproj` to SDK-style while **staying on net481**. Keep this separate from the TFM, hosting and API migration. For the test project: migrate `packages.config` to `PackageReference` at the same MSTest 2.2.7 versions, and remove the TeamTest import, the HintPaths and the `EnsureNuGetPackageBuildImports` target (WCF-009). For the host: keep `AssemblyName`/`RootNamespace` = `TimeZoneConvertor` and remove the stale `None` items `Contracts\TimeZoneConverter.wsdl|xsd`, which are missing on disk (WCF-008). The `.svc`, `Web.config` and transforms stay in place on net481 for now. #skill:migrating-wcf-to-corewcf

Risk: SDK-style conversion of a classic WebApplication (`{349c5851…}`) `.svc` project on net481 may not be supported by the conversion tooling, or may lose IIS Express/.svc build behavior. If the converted host cannot build on net481 with its `.svc` content intact, revert only the host's conversion, record why, and move the host's project-format change into task 03, where it becomes `Microsoft.NET.Sdk.Web`. The test project conversion still lands here.

**Done when**: Each converted project is SDK-style on net481. The solution builds and the 2 unit tests pass on net481, or are recorded UNVERIFIED with the missing prerequisite named (e.g. NuGet restore). Any host-conversion fallback is recorded with its reason.


---

## Research findings (task executor, 2026-10-01, before any source edit)

### Skills / stage loaded
- Scenario Execution stage `execution.md` (step 4: SDK-style conversion on original TFM, sequential `convert_project_to_sdk_style`, preservation checkpoint from `migrating-wcf-to-corewcf/ref/build-and-conversion-safeguards.md`); scenario-extension (Execution): none apply.
- `converting-to-sdk-style` (tool-based conversion only, no TFM change, no package drift, one project at a time in topological order, packages.config removed), `migrating-wcf-to-corewcf` (+ `ref/build-and-conversion-safeguards.md`), `building-projects`, `managing-package-references`, `managing-legacy-dotnet-packages`.
- Decomposition verdict: **atomic** — two small project files with fully pinned scope; the host fallback only defers the host's format change to task 03 (already planned there), it does not change the remaining work of this task. No Breakdown-Hints section in the scenario files matches (execution.md "small-cohesive" rule; format-only SDK checkpoint is this task).

### Confirmed scope (actual repo, commit f2bfc85)
- Topological order (`get_projects_in_topological_order`): 1) `src48/TimeZoneConverter/TimeZoneConverter.csproj`, 2) `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj`. Solution `src48/TimeZoneConverter.sln` also has a `contracts` solution folder (items `..\contracts\TimeZoneConverter.wsdl|xsd`, exist on disk) — untouched.
- No `Directory.Build.*`, `Directory.Packages.props` or repo `nuget.config`; user NuGet.Config: `azure-default` proxy feed enabled, nuget.org disabled (task 01). `src48/packages` contains MSTest.TestAdapter/TestFramework 2.2.7 (from task 01 restore). `bin/`, `obj/` gitignored.
- **Host** `TimeZoneConverter.csproj`: legacy ToolsVersion 15, WebApplication flavor `{349c5851…};{fae04ec0…}`, `TargetFrameworkVersion v4.8.1`, `LangVersion latest`, imports `Microsoft.CSharp.targets` + `$(VSToolsPath)\WebApplications\Microsoft.WebApplication.targets`; IIS Express props (`UseIISExpress=true`, `IISUrl http://localhost:50355/`, `DevelopmentServerPort 50355`, empty SSL port/auth props) in `ProjectExtensions`; no packages; 18 GAC `<Reference>`s (System.ServiceModel, System.ServiceModel.Web, System.Web*, …).
  - Compile (10, all exist): Contracts\ITimeZoneConverter.cs, Contracts\TimeZoneConversionResponse.cs, Properties\AssemblyInfo.cs, RequestResponseDispatchFormatter.cs, RequestResponseSerializerAttribute.cs, ResponseDispatchFormatter.cs, ResponseSerializerAttribute.cs, Serializers\ConvertToOffset{Request,Response}Serializer.cs, TimeZoneConverter.svc.cs (DependentUpon TimeZoneConverter.svc).
  - Content: TimeZoneConverter.svc, Web.config. None: GenerateContracts.ps1, Web.Debug.config / Web.Release.config (DependentUpon Web.config), and **stale** `Contracts\TimeZoneConverter.wsdl` / `Contracts\TimeZoneConverter.xsd` — confirmed **missing on disk** (WCF-008; the real files live in repo-root `contracts/`).
  - `Properties\AssemblyInfo.cs` exists with full assembly attributes → SDK-style must set `GenerateAssemblyInfo=false` (avoid CS0579 duplicates).
- **Test** `TimeZoneConverterTest.csproj`: legacy, MSTest project type GUID `{3AC096D0…}`, v4.8.1, `LangVersion latest`; packages.config = MSTest.TestAdapter 2.2.7 + MSTest.TestFramework 2.2.7 (`targetFramework=net481`); HintPath references to TestFramework(.Extensions) 2.2.7 `lib\net45`; MSTest.TestAdapter props/targets imports; TeamTest `Microsoft.TestTools.targets` import; `EnsureNuGetPackageBuildImports` target; GAC refs System, System.Core, System.Runtime.Serialization, System.Xml, System.Xml.Serialization; ProjectReference → host. Compile: ConversionReponseSerializerTest.cs, Properties\AssemblyInfo.cs (full attributes → `GenerateAssemblyInfo=false`).

### Preservation checkpoint (evaluated, MSBuild 18, Debug, pre-conversion)
| Surface | Host (TimeZoneConvertor) | Test |
|---|---|---|
| OutputType | Library | Library |
| AssemblyName / RootNamespace | TimeZoneConvertor / TimeZoneConvertor | TimeZoneConverterTest / TimeZoneConverterTest |
| OutputPath / TargetPath | `bin\` → `TimeZoneConverter\bin\TimeZoneConvertor.dll` (IIS layout, no TFM suffix) | `bin\Debug\` → `bin\Debug\TimeZoneConverterTest.dll` |
| TFM | .NETFramework,Version=v4.8.1 | .NETFramework,Version=v4.8.1 |
| GenerateAssemblyInfo | (n/a — AssemblyInfo.cs) | (n/a — AssemblyInfo.cs) |
| LangVersion / DebugType / DefineConstants | latest / full / DEBUG;TRACE | latest / full / DEBUG;TRACE |
| AutoGenerateBindingRedirects | true | true |
| Web/WCF | WcfConfigValidationEnabled=True, UseIISExpress=true, IIS URL http://localhost:50355/, WebProjectOutputDir = project dir | — |
| Items | Compile ×10, Content ×2 (.svc, Web.config), None ×5 (incl. 2 stale) | Compile ×2, None packages.config + 4 MSTest adapter dlls (CopyToOutput) |

### Planned approach
1. Convert host first with `convert_project_to_sdk_style` (sequential). Accept only if, on net481 with MSBuild 18: builds 0W/0E, `TimeZoneConvertor.dll` still lands in `TimeZoneConverter\bin\` (IIS layout), `.svc`/`Web.config` stay Content, transforms stay None/DependentUpon, AssemblyName/RootNamespace unchanged, IIS Express launch settings preserved, no duplicate assembly attributes. Remove the stale wsdl/xsd `None` items. If any of that cannot be met (e.g. tool refuses WebApplication flavor or web targets/.svc build behavior is lost), **revert the host project file only** (git checkout) and record fallback to task 03 (`Microsoft.NET.Sdk.Web`).
2. Convert test project with the tool: PackageReference MSTest.TestAdapter 2.2.7 + MSTest.TestFramework 2.2.7 (same versions, no drift), remove packages.config, HintPaths, TeamTest import, EnsureNuGetPackageBuildImports; keep GAC refs needed for net481, ProjectReference to host, `GenerateAssemblyInfo=false`. Do not add Microsoft.NET.Test.Sdk (new package = drift) unless required for vstest discovery — vstest.console discovers MSTest 2.2.7 via the adapter copied to output.
3. Validate: MSBuild 18 `/restore` solution build Debug (0 warnings target), vstest.console on the test dll → 2 discovered / 2 passed; compare evaluated properties/items to the checkpoint above.

### Outcome (see progress-details.md)
- Test project: converted to SDK-style net481 (MSTest 2.2.7 PackageReference; tool-added floating `Microsoft.NET.Test.Sdk 16.*` removed as package drift). Build 0W/0E, tests 2/2 passed via vstest.console.
- Host: **fallback taken** — tool emitted `Microsoft.NET.Sdk.Web` + `OutputType=Exe` (CS5001) and, even with Library/no-TFM-suffix fixes, `.svc` and Web.config transforms became conflicting Content+None items, Web.config copied to bin, WebApplication publish/IIS Express settings lost. Host conversion reverted; format change moves to task 03 (`Microsoft.NET.Sdk.Web` net10.0). Stale wsdl/xsd `None` items removed from the legacy host file (WCF-008).
