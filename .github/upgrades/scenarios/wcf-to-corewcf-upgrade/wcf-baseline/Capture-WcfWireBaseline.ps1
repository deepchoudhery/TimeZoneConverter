<#
.SYNOPSIS
  Captures WCF wire evidence (HTTP GET metadata + SOAP envelopes for the SoapUI net48 samples)
  from a running TimeZoneConverter host. Used for the golden baseline (task 01) and can be
  re-run against the migrated CoreWCF host (tasks 03/05) with a different -OutputRoot.
.EXAMPLE
  powershell -NoProfile -ExecutionPolicy Bypass -File Capture-WcfWireBaseline.ps1 -BaseUrl http://localhost:50355/TimeZoneConverter.svc -SoapUiProject <repo>\soapui\TimeZoneConverter-net48-soapui-project.xml -OutputRoot <dir>
#>
param(
    [Parameter(Mandatory)] [string] $BaseUrl,
    [Parameter(Mandatory)] [string] $SoapUiProject,
    [Parameter(Mandatory)] [string] $OutputRoot,
    [string] $Label = 'capture'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$metaDir = Join-Path $OutputRoot 'metadata'
$envDir  = Join-Path $OutputRoot 'envelopes'
New-Item -ItemType Directory -Force $metaDir, $envDir | Out-Null

$handler = New-Object System.Net.Http.HttpClientHandler
$handler.AllowAutoRedirect = $false
$handler.UseProxy = $false
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(60)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$manifest = [ordered]@{ label = $Label; baseUrl = $BaseUrl; capturedBy = 'Capture-WcfWireBaseline.ps1'; started = (Get-Date).ToString('o'); metadata = @(); envelopes = @() }

function Get-Sha256([byte[]] $bytes) { ($([System.Security.Cryptography.SHA256]::Create()).ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '' }
function Get-Headers($resp) {
    $h = [ordered]@{}
    foreach ($kv in $resp.Headers) { $h[$kv.Key] = ($kv.Value -join ', ') }
    if ($resp.Content) { foreach ($kv in $resp.Content.Headers) { $h[$kv.Key] = ($kv.Value -join ', ') } }
    $h
}

function Save-Get([string] $url, [string] $fileName) {
    $t = (Get-Date).ToString('o')
    $resp = $client.GetAsync($url).GetAwaiter().GetResult()
    $bytes = $resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    $path = Join-Path $metaDir $fileName
    [System.IO.File]::WriteAllBytes($path, $bytes)
    $entry = [ordered]@{ url = $url; file = "metadata/$fileName"; capturedAt = $t; status = [int]$resp.StatusCode; bytes = $bytes.Length; sha256 = (Get-Sha256 $bytes); headers = (Get-Headers $resp) }
    $script:manifest.metadata += $entry
    return $utf8NoBom.GetString($bytes)
}

# --- Metadata -------------------------------------------------------------
$wsdl = Save-Get "$BaseUrl`?wsdl" 'wsdl.xml'
$seen = @{}
$queue = New-Object System.Collections.Queue
$queue.Enqueue($wsdl)
while ($queue.Count -gt 0) {
    $doc = $queue.Dequeue()
    foreach ($m in [regex]::Matches($doc, '(?:schemaLocation|location)="([^"]*\?(?:xsd=xsd\d+|wsdl=wsdl\d+))"')) {
        $u = [System.Net.WebUtility]::HtmlDecode($m.Groups[1].Value)
        if ($seen.ContainsKey($u)) { continue }
        $seen[$u] = $true
        $q = ($u -split '\?')[1]
        $name = if ($q -like 'xsd=*') { ($q -replace '=', '-') + '.xsd' } else { ($q -replace '=', '-') + '.xml' }
        $queue.Enqueue((Save-Get $u $name))
    }
}
[void](Save-Get "$BaseUrl`?singleWsdl" 'singleWsdl.xml')
[void](Save-Get "$BaseUrl" 'service-help-page.html')

# --- Envelopes ------------------------------------------------------------
[xml] $proj = Get-Content -Raw -LiteralPath $SoapUiProject
$ns = New-Object System.Xml.XmlNamespaceManager($proj.NameTable)
$ns.AddNamespace('con', 'http://eviware.com/soapui/config')
$op = $proj.SelectSingleNode('//con:operation[@name="ConvertToOffset"]', $ns)
$soapAction = $op.GetAttribute('action')
$calls = @()
foreach ($call in $op.SelectNodes('con:call', $ns)) {
    $raw = $call.SelectSingleNode('con:request', $ns).InnerText
    # SoapUI stores CR as the literal two-char marker "\r" before LF; SoapUI sends a real CR.
    $body = $raw.Replace("\r`n", "`r`n")
    $calls += [pscustomobject]@{ name = $call.GetAttribute('name'); body = $body; action = $soapAction; rawHadCrMarker = $raw.Contains("\r`n"); supplementary = $false }
}
# Supplementary fault probe: unknown SOAPAction (ActionNotSupported, includeExceptionDetailInFaults=false)
$calls += [pscustomobject]@{ name = 'FaultProbe-UnknownAction'; body = $calls[0].body; action = 'http://bartjolling.github.io/ITimeZoneConverter/UnknownOperation'; rawHadCrMarker = $calls[0].rawHadCrMarker; supplementary = $true }

foreach ($c in $calls) {
    $reqBytes = $utf8NoBom.GetBytes($c.body)
    $reqFile = "$($c.name).request.xml"; $respFile = "$($c.name).response.xml"
    [System.IO.File]::WriteAllBytes((Join-Path $envDir $reqFile), $reqBytes)
    $msg = New-Object System.Net.Http.HttpRequestMessage([System.Net.Http.HttpMethod]::Post, $BaseUrl)
    $msg.Content = New-Object System.Net.Http.ByteArrayContent(, $reqBytes)
    [void]$msg.Content.Headers.TryAddWithoutValidation('Content-Type', 'text/xml;charset=UTF-8')
    [void]$msg.Headers.TryAddWithoutValidation('SOAPAction', '"' + $c.action + '"')
    $t = (Get-Date).ToString('o')
    $resp = $client.SendAsync($msg).GetAwaiter().GetResult()
    $respBytes = $resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
    [System.IO.File]::WriteAllBytes((Join-Path $envDir $respFile), $respBytes)
    $script:manifest.envelopes += [ordered]@{
        name = $c.name; supplementary = $c.supplementary; url = $BaseUrl; capturedAt = $t
        requestHeaders = [ordered]@{ 'Content-Type' = 'text/xml;charset=UTF-8'; SOAPAction = '"' + $c.action + '"' }
        requestFile = "envelopes/$reqFile"; requestSha256 = (Get-Sha256 $reqBytes); soapUiCrMarkerNormalized = $c.rawHadCrMarker
        status = [int]$resp.StatusCode; responseFile = "envelopes/$respFile"; responseBytes = $respBytes.Length; responseSha256 = (Get-Sha256 $respBytes)
        responseHeaders = (Get-Headers $resp)
    }
}
$manifest.finished = (Get-Date).ToString('o')
[System.IO.File]::WriteAllText((Join-Path $OutputRoot 'capture-manifest.json'), ($manifest | ConvertTo-Json -Depth 6), $utf8NoBom)
"Captured $($manifest.metadata.Count) metadata documents and $($manifest.envelopes.Count) envelopes into $OutputRoot"
