# Progress — 02-sdk-style-conversion

## Result (2026-10-01)

**Outcome: done with a recorded host fallback.**
- `TimeZoneConverterTest.csproj` → **SDK-style on net481** (landed here).
- `TimeZoneConverter.csproj` (WebApplication `.svc` host) → **conversion reverted; project-format change moved to task 03** (`Microsoft.NET.Sdk.Web` net10.0). WCF-008 stale-item cleanup applied to the legacy file.

Decomposition verdict: atomic (execution.md + migrating-wcf-to-corewcf/converting-to-sdk-style decomposition sections evaluated; two pinned project files, fallback is the plan's own deferral to task 03).

## Host conversion attempt and fallback reason

`convert_project_to_sdk_style` (topological order: host first) produced:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web"> … <OutputType>Exe</OutputType> … <TargetFramework>net481</TargetFramework>
<GenerateAssemblyInfo>false</GenerateAssemblyInfo> <OutputPath>bin\</OutputPath>
```
with Web.config Content, the WebApplication.targets import, ProjectTypeGuids and all IIS Express settings (`UseIISExpress`, `IISUrl http://localhost:50355/`, `DevelopmentServerPort 50355`) dropped.

Evidence against the preservation checkpoint (MSBuild 18, Debug):
1. As emitted: **CS5001** "Program does not contain a static 'Main' method" — the tool changed `OutputType` Library → Exe.
2. Probe with `/p:OutputType=Library /p:AppendTargetFrameworkToOutputPath=false` (command-line only, not committed): builds to `bin\TimeZoneConvertor.dll`, but evaluated items no longer match the checkpoint:
   - `TimeZoneConverter.svc` is **both Content and None**; `Web.Debug.config`/`Web.Release.config` are **both Content (Sdk.Web glob) and None** — conflicting item types for `.svc` and transforms (safeguards: "must not gain conflicting item types").
   - `Web.config` and both transforms gain `CopyToOutputDirectory/CopyToPublishDirectory=PreserveNewest` (copied into `bin\`).
   - WebApplication.targets publish (Web.config transform, dll in `bin\`) replaced by the ASP.NET Core Web SDK publish pipeline (dll at publish root) → IIS `.svc` activation of a published site would not find the assembly; IIS Express F5 settings lost.
3. Restoring parity would require hand-authoring an SDK-style System.Web/WCF WebApplication project (switch SDK, re-import WebApplication.targets, item Remove/Update fixes, launch settings) — an unsupported project type, outside "use the conversion tool, no manual XML rewrite" (converting-to-sdk-style), and discarded by task 03 anyway.

→ Per task.md risk clause ("cannot build on net481 with its .svc content intact … revert only the host's conversion"), reverted with `git checkout -- src48/TimeZoneConverter/TimeZoneConverter.csproj`, deleted the host's (gitignored) `bin/` and `obj/` (they contained the probe's `project.assets.json`), then applied only the WCF-008 item cleanup. **Task 03 owns the host's format change to `Microsoft.NET.Sdk.Web` (net10.0)**; it must keep `AssemblyName`/`RootNamespace` = `TimeZoneConvertor` and should not re-add the removed stale items.

## Files modified
| File | Change |
|---|---|
| `src48/TimeZoneConverterTest/TimeZoneConverterTest.csproj` | Converted by `convert_project_to_sdk_style` to `<Project Sdk="Microsoft.NET.Sdk">`, `TargetFramework net481`, `LangVersion latest`, `GenerateAssemblyInfo false`, `Reference System.Xml.Serialization`, `ProjectReference ..\TimeZoneConverter\TimeZoneConverter.csproj`, `PackageReference MSTest.TestAdapter 2.2.7` + `MSTest.TestFramework 2.2.7`. Removed (WCF-009): HintPaths, MSTest props/targets imports, TeamTest `Microsoft.TestTools.targets` import, `EnsureNuGetPackageBuildImports`. **Manual deviation:** removed the tool-added `PackageReference Microsoft.NET.Test.Sdk Version="16.*"` (new + floating package = drift vs packages.config; not needed for vstest.console). |
| `src48/TimeZoneConverterTest/packages.config` | Deleted by the conversion tool. |
| `src48/TimeZoneConverter/TimeZoneConverter.csproj` | Still legacy. Only removed the stale `None` items `Contracts\TimeZoneConverter.wsdl` and `Contracts\TimeZoneConverter.xsd` (missing on disk, WCF-008). |
| `.github/upgrades/.../scenario-instructions.md` | Updated `## Build Tool Decisions` for both projects. |
| `.github/upgrades/.../tasks/02-sdk-style-conversion/task.md` | Research findings + preservation checkpoint (before edits), outcome note. |
| `tasks/02-sdk-style-conversion/msbuild-net481-sdkstyle.build-log.txt`, `net481-sdkstyle.trx` | Evidence. |

`src60/` not read or touched. `git status`: only the three `src48` entries above plus workflow artifacts.

## Preservation checkpoint — after
| Surface | Before | After | Disposition |
|---|---|---|---|
| Test OutputType/AssemblyName/RootNamespace | Library / TimeZoneConverterTest ×2 | same | preserved |
| Test TFM | .NETFramework v4.8.1 | same (`net481`) | preserved |
| Test TargetPath | `bin\Debug\TimeZoneConverterTest.dll` | `bin\Debug\net481\TimeZoneConverterTest.dll` | accepted SDK default (test assembly, not IIS-sensitive; task 04 retargets) |
| Test LangVersion / DefineConstants / AutoGenerateBindingRedirects | latest / DEBUG;TRACE / true | same | preserved |
| Test DebugType | full | portable | accepted SDK default (pdb format only) |
| Test Compile | ConversionReponseSerializerTest.cs, Properties\AssemblyInfo.cs | same (glob), `GenerateAssemblyInfo=false` → no duplicate attributes | preserved |
| Test references | System, System.Core, System.Runtime.Serialization, System.Xml, System.Xml.Serialization + MSTest 2.2.7 HintPaths | implicit SDK netfx refs (superset) + System.Xml.Serialization + PackageReference MSTest 2.2.7 (assets resolved: MSTest.TestAdapter/2.2.7, MSTest.TestFramework/2.2.7, no others) | preserved |
| Test output contents | adapter dlls + TestFramework dlls + TimeZoneConvertor.dll(.config) | same set (adapter dlls via package build props; `TimeZoneConvertor.dll.config` = host Web.config copy, same as baseline) | preserved |
| Host TargetPath / items | `bin\TimeZoneConvertor.dll`; Content .svc, Web.config; None ps1, transforms, 2 stale | `bin\TimeZoneConvertor.dll`; Content .svc, Web.config; None ps1, transforms | stale items removed (WCF-008), rest unchanged |

## Build / test (net481)
- Restore source: `https://packagefeedproxy.microsoft.io/nuget/v3/index.json` (+ offline SDK folders); restore succeeded.
- `MSBuild.exe src48\TimeZoneConverter.sln /restore /t:Rebuild /p:Configuration=Debug /v:minimal` (VS 18 MSBuild, after deleting both projects' bin/obj) → **0 Warning(s), 0 Error(s)**. Log: `msbuild-net481-sdkstyle.build-log.txt`.
- `vstest.console.exe src48\TimeZoneConverterTest\bin\Debug\net481\TimeZoneConverterTest.dll /logger:trx` (VSTest 18.10.0) → **2 discovered / 2 executed / 2 passed** (`WriteObject`, `ReadObject`). TRX: `net481-sdkstyle.trx`.
- Host source/output unchanged apart from removed stale None items, so the task 01 golden wire baseline remains valid for the host; no runtime re-capture needed.

## Processes owned at return
None (no IIS Express or other background process started).
