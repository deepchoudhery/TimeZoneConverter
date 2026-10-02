# WCF Golden Wire Baseline — TimeZoneConverter (net481, original host)

Task: `01-validation-environment-and-wire-baseline` · Captured 2026-10-01 (local time −07:00) · **Baseline type: LIVE GOLDEN** (original .NET Framework 4.8.1 host running under IIS Express). The offline baseline was not needed; offline references are listed below only as cross-checks.

Source commit: `02bf9304bce143f69d4062d3d01a6980103d2bf5` (no source files changed by this task).

## 1. Validation prerequisites checklist

| # | Prerequisite | Status | Evidence |
|---|---|---|---|
| P1 | .NET 10 SDK | **Available** | `dotnet --list-sdks`: 8.0.425, 10.0.203, 10.0.400-preview.0.26356.102, 10.0.401; no `global.json` in repo (latest 10.0.401 resolves) |
| P2 | VS MSBuild | **Available** | `C:\Program Files\Microsoft Visual Studio\18\Main\MSBuild\Current\Bin\MSBuild.exe` (vswhere `-latest -prerelease -products "*"`) |
| P3 | `Microsoft.WebApplication.targets` | **Available** | `...\18\Main\MSBuild\Microsoft\VisualStudio\v18.0\WebApplications\Microsoft.WebApplication.targets` (also in 18\IntPreview) |
| P4 | .NET Framework 4.8.1 targeting pack | **Available** | `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8.1`; used by csc in the baseline build |
| P5 | NuGet feed | **Available (via proxy feed)** | `api.nuget.org` → TLS handshake failure (WCF-012) and it is **disabled** in `%APPDATA%\NuGet\NuGet.Config`. Enabled `azure-default` = `https://packagefeedproxy.microsoft.io/nuget/v3/index.json` → HTTP 200; MSTest 2.2.7 restored from it. Restores in later tasks depend on this proxy feed carrying CoreWCF 1.9.1 / MSTest 4.4.1 (to be verified at first restore). |
| P6 | IIS Express (original host) | **Available** | `C:\Program Files\IIS Express\iisexpress.exe`; ran the original host at `http://localhost:50355/` (see §3) |
| P7 | vstest (net481 tests) | **Available** | `...\18\Main\Common7\IDE\Extensions\TestPlatform\vstest.console.exe` (18.10.0) |
| P8 | HTTPS certificate for E2 (existing only) | **Present but not usable for an original-host E2 capture → E2 golden UNVERIFIED** | Existing certs (not created/modified): CurrentUser\My `CN=localhost` ASP.NET Core HTTPS development certificate (valid to 2027-05-18, **not trusted** — `dotnet dev-certs https --check --trust` exit 7); LocalMachine\My `CN=localhost` IIS Express Development Certificate (valid to 2031-05-12). The original project has **no HTTPS binding** (`IISExpressSSLPort` empty), so the original dev host never served E2; no binding was added. |
| P9 | SoapUI application | **Not used** | Not required: the 4 SoapUI requests were replayed verbatim (after SoapUI CR-marker normalization) by `Capture-WcfWireBaseline.ps1`. Running SoapUI itself is a manual/owner check. |

### UNVERIFIED baseline checks
- **E2 (basicHttpsBinding / Transport) golden envelopes and `soap:address` over https** — UNVERIFIED. Missing prerequisite: an HTTPS site binding on the original host (none configured in the project; production HTTPS binding/certificate is an owner input). The ASP.NET Core dev cert exists but is untrusted; trust-store changes are prohibited.
- **M1 metadata over HTTPS (`httpsGetEnabled=true`)** — UNVERIFIED, same prerequisite.
- **Real SoapUI client run** — UNVERIFIED (SoapUI not run; payloads replayed by script instead).

## 2. net481 build and test baseline

| Check | Result | Artifact |
|---|---|---|
| Restore (packages.config) | MSTest.TestAdapter 2.2.7, MSTest.TestFramework 2.2.7 restored to `src48/packages` (gitignored) from `packagefeedproxy.microsoft.io`. nupkg SHA256: TestAdapter `AB4EF1E7…0509`, TestFramework `B9B63068…ADB6` | `build/msbuild-net481.build-log.txt` |
| Build `src48/TimeZoneConverter.sln` (Debug, MSBuild 18) | **Succeeded — 0 warnings, 0 errors**; outputs `src48/TimeZoneConverter/bin/TimeZoneConvertor.dll` (SHA256 `93B1B36C…58D8`), `src48/TimeZoneConverterTest/bin/Debug/TimeZoneConverterTest.dll` | `build/msbuild-net481.build-log.txt` |
| Tests (vstest 18.10.0) | **2 discovered, 2 executed, 2 passed** (`WriteObject`, `ReadObject`) | `build/net481-baseline.trx` |

Command lines (single-line, from `src48/`):
- `"C:\Program Files\Microsoft Visual Studio\18\Main\MSBuild\Current\Bin\MSBuild.exe" TimeZoneConverter.sln /restore /p:RestorePackagesConfig=true /p:Configuration=Debug /v:minimal`
- `"C:\Program Files\Microsoft Visual Studio\18\Main\Common7\IDE\Extensions\TestPlatform\vstest.console.exe" TimeZoneConverterTest\bin\Debug\TimeZoneConverterTest.dll /logger:trx`

## 3. Original host used for capture

IIS Express `C:\Program Files\IIS Express\iisexpress.exe`, private config (copy of the PersonalWebServer template) in `%TEMP%\tzc-wcf-baseline-iisx` (`/config:… /site:TimeZoneConverter /userhome:… /systray:false`), site physicalPath `src48\TimeZoneConverter`, binding `http/*:50355:localhost`, `Clr4IntegratedAppPool`. Same URL as the project's `IISUrl` and the SoapUI endpoint. No user-profile/global IIS Express config, URL reservation, or cert binding was created. Process PID 50336 was started and **stopped** after capture (port 50355 refuses connections afterwards).

## 4. Golden artifacts (index)

All raw bytes as received; URLs, capture times, HTTP status, headers and SHA256 per file in [`capture-manifest.json`](capture-manifest.json). Re-run against any host with [`Capture-WcfWireBaseline.ps1`](Capture-WcfWireBaseline.ps1) `-BaseUrl … -SoapUiProject … -OutputRoot <new dir>` (do **not** point `-OutputRoot` at this folder after migration).

| Kind | File | URL / sample |
|---|---|---|
| WSDL (service, tempuri.org) | `metadata/wsdl.xml` | `…/TimeZoneConverter.svc?wsdl` |
| WSDL (contract, imported) | `metadata/wsdl-wsdl0.xml` | `…?wsdl=wsdl0` |
| XSD | `metadata/xsd-xsd0.xsd` | `…?xsd=xsd0` (the only schema referenced) |
| Single WSDL | `metadata/singleWsdl.xml` | `…?singleWsdl` |
| Service help page (supplementary) | `metadata/service-help-page.html` | GET `…/TimeZoneConverter.svc` |
| Envelopes (request+response) | `envelopes/ConvertToLowerOffset.*` | To 60 → `2022-09-24T09:23:45.6780000+01:00` |
| | `envelopes/ConvertToHigherOffset.*` | To 420 → `2022-09-24T15:23:45.6780000+07:00` |
| | `envelopes/ConvertToSameOffset.*` | To 120 → `2022-09-24T10:23:45.6780000+02:00` |
| | `envelopes/ConvertToNegativeOffset.*` | To −360 → `2022-09-24T02:23:45.6780000-06:00` |
| Fault probe (supplementary) | `envelopes/FaultProbe-UnknownAction.*` | SOAPAction `…/UnknownOperation` → HTTP 500 `a:ActionNotSupported` |
| Unchanged contract (D-4) | `contracts-original/ITimeZoneConverter.cs` | SHA256 `D90A5D48…CE20` = `src48/TimeZoneConverter/Contracts/ITimeZoneConverter.cs` at commit above (git blob `1033fe0d`) |

All requests: `POST http://localhost:50355/TimeZoneConverter.svc`, `Content-Type: text/xml;charset=UTF-8`, `SOAPAction: "http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest"`, body UTF-8 without BOM. SoapUI stores CR as a literal `\r` marker in 3 of the 4 CDATA bodies; the script converts `\r`+LF to CRLF (what SoapUI sends). ConvertToHigherOffset has LF-only line ends.

## 5. Strict XML parity reference (what later tasks compare)

Golden success response (identical shape for all 4 samples, 397 bytes, single line, **no XML declaration**):

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><ConvertToOffsetResponse xmlns="http://bartjolling.github.io/" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"><TimeZoneConversionResponse><ToDateTime>2022-09-24T09:23:45.6780000+01:00</ToDateTime></TimeZoneConversionResponse></ConvertToOffsetResponse></s:Body></s:Envelope>
```

| Item | Golden value |
|---|---|
| HTTP status / Content-Type | `200`, `text/xml; charset=utf-8` |
| Envelope | SOAP 1.1, prefix `s`, **no `<s:Header>`** |
| Request Action (dispatch) | `http://bartjolling.github.io/ITimeZoneConverter/ConvertToOffsetRequest` via `SOAPAction` header — accepted (200); unknown action → 500 `ActionNotSupported` fault |
| Reply Action (WCF-006) | **Not observable on the wire for E1.** BasicHttpBinding = SOAP 1.1 + AddressingNone, so the formatter's hard-coded `"ConvertToOffset"` reply action is emitted neither as `<a:Action>` nor as an HTTP header. Wire parity therefore = "no SOAP header, no Action". Code parity (`Message.CreateMessage(…, "ConvertToOffset", …)`) must still be preserved and can only be checked at code/inspector level. |
| Body root | `ConvertToOffsetResponse` in default ns `http://bartjolling.github.io/`, then `xmlns:xsd` **before** `xmlns:xsi` |
| Element order | `ConvertToOffsetResponse` › `TimeZoneConversionResponse` › `ToDateTime` (only child; no `ToDateTimeOffset` element) |
| `ToDateTime` format (WCF-005) | round-trip `"o"` with 7 fractional digits and explicit offset, e.g. `2022-09-24T02:23:45.6780000-06:00` |
| Response headers | `X-Frame-Options: SAMEORIGIN` (Web.config, must be preserved). Host-specific, not SOAP parity: `Server: Microsoft-IIS/10.0`, `X-AspNet-Version`, `X-Powered-By: ASP.NET`, `Cache-Control: private`, `X-SourceFiles` (IIS Express debug only) |
| Fault (no detail) | `s:Fault` with `faultcode a:ActionNotSupported` (`xmlns:a="http://schemas.microsoft.com/ws/2005/05/addressing/none"`) and an English `faultstring xml:lang="en-US"`; no `<detail>` (includeExceptionDetailInFaults=false). Wording is runtime/culture-dependent — compare code/structure, not text. |

Metadata facts:
- `?wsdl` is split: service WSDL `targetNamespace="http://tempuri.org/"` (no `ServiceBehavior.Namespace`) with `<wsdl:import namespace="http://bartjolling.github.io/" location="…?wsdl=wsdl0"/>`; contract WSDL imports `…?xsd=xsd0`. `?singleWsdl` is flattened with `targetNamespace="http://bartjolling.github.io/"`.
- Service `TimeZoneConverterService`, port `BasicHttpBinding_ITimeZoneConverter`, binding `BasicHttpBinding_ITimeZoneConverter`, `soap:address location="http://localhost:50355/TimeZoneConverter.svc"`; only the http port (no https port because no https binding).
- portType `ITimeZoneConverter`, operation `ConvertToOffset`, input `wsaw:Action=…/ConvertToOffsetRequest`, output `wsaw:Action=…/ConvertToOffsetResponse`, `soapAction=…/ConvertToOffsetRequest`, document/literal.
- XSD: `TimeZoneConversionResponse` = `ToDateTime xs:dateTime minOccurs=1` + `ToDateTimeOffset xs:string minOccurs=0`.

## 6. Offline references (cross-checks only)

| Source | SHA256 | Relation to live golden |
|---|---|---|
| SoapUI cached `?singleWsdl` (`soapui/TimeZoneConverter-net48-soapui-project.xml`) | file `E90BC5DC…0408` | Identical to live `singleWsdl.xml` after whitespace normalization **except one stale delta**: cache has `ToDateTimeOffset type="tns:DateTimeOffset" minOccurs="1"` plus empty `complexType DateTimeOffset`; live has `xs:string minOccurs="0"`. Live is authoritative. |
| `contracts/TimeZoneConverter.wsdl` | `952145E4…FF7A` | Design contract; soapAction differs from runtime (WCF-006) — not a parity oracle |
| `contracts/TimeZoneConverter.xsd` | `9DBAABAE…50E3` | Design schema — not a parity oracle |
| `ConversionReponseSerializerTest.cs:18` expected XML | file `59770B8B…FCB1` | Matches the live body content (same ns declaration order, `<ToDateTime>` format); passes on net481 |
