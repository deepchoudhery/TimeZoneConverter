<#
.SYNOPSIS
  Compares a wire capture (Capture-WcfWireBaseline.ps1 output) against the golden baseline.
  For every golden file it reports:
    BYTES-EQUAL      raw bytes identical
    XML-EQUAL        not byte-identical, but the XML infoset is identical including the ORDER of
                     namespace declarations (only insignificant formatting differs)
    NSDECL-ORDER     identical element/attribute/text content and identical SET of namespace
                     declarations per element; only the order of xmlns declarations differs
    DIFF             any other difference (first difference path printed)
  Host-specific values can be normalised with -Replace "old=>new" pairs (applied to the new capture).
#>
param(
    [Parameter(Mandatory)] [string] $GoldenRoot,
    [Parameter(Mandatory)] [string] $CaptureRoot,
    [string[]] $Replace = @()
)
$ErrorActionPreference = 'Stop'

function Get-Decls([System.Xml.XmlElement] $e) { @($e.Attributes | Where-Object { $_.Name -eq 'xmlns' -or $_.Prefix -eq 'xmlns' } | ForEach-Object { "$($_.Name)=$($_.Value)" }) }
function Get-Attrs([System.Xml.XmlElement] $e) { @($e.Attributes | Where-Object { -not ($_.Name -eq 'xmlns' -or $_.Prefix -eq 'xmlns') } | ForEach-Object { "{$($_.NamespaceURI)}$($_.LocalName)=$($_.Value)" } | Sort-Object) }

function Compare-Node($a, $b, [string] $path, [ref] $declOrderOnly) {
    if ($a.NodeType -ne $b.NodeType) { return "$path : node type $($a.NodeType) vs $($b.NodeType)" }
    if ($a.NodeType -eq 'Element') {
        if ($a.NamespaceURI -ne $b.NamespaceURI -or $a.LocalName -ne $b.LocalName) { return "$path : element {$($a.NamespaceURI)}$($a.LocalName) vs {$($b.NamespaceURI)}$($b.LocalName)" }
        if ($a.Prefix -ne $b.Prefix) { return "$path/$($a.Name) : prefix '$($a.Prefix)' vs '$($b.Prefix)'" }
        $here = "$path/$($a.Name)"
        $aa = (Get-Attrs $a) -join '|'; $ba = (Get-Attrs $b) -join '|'
        if ($aa -ne $ba) { return "$here : attributes`n      golden: $aa`n      new   : $ba" }
        $ad = Get-Decls $a; $bd = Get-Decls $b
        if ((($ad | Sort-Object) -join '|') -ne (($bd | Sort-Object) -join '|')) { return "$here : namespace declaration set`n      golden: $($ad -join ' ')`n      new   : $($bd -join ' ')" }
        if (($ad -join '|') -ne ($bd -join '|')) { $declOrderOnly.Value = $true }
        $ac = @($a.ChildNodes | Where-Object { $_.NodeType -ne 'Whitespace' }); $bc = @($b.ChildNodes | Where-Object { $_.NodeType -ne 'Whitespace' })
        if ($ac.Count -ne $bc.Count) { return "$here : child count $($ac.Count) vs $($bc.Count)" }
        for ($i = 0; $i -lt $ac.Count; $i++) { $r = Compare-Node $ac[$i] $bc[$i] $here $declOrderOnly; if ($r) { return $r } }
        return $null
    }
    if ($a.Value -ne $b.Value) { return "$path : text '$($a.Value)' vs '$($b.Value)'" }
    return $null
}

$results = @()
foreach ($f in Get-ChildItem -Recurse -File (Join-Path $GoldenRoot 'metadata'), (Join-Path $GoldenRoot 'envelopes')) {
    $rel = $f.FullName.Substring((Resolve-Path $GoldenRoot).Path.Length + 1)
    $other = Join-Path $CaptureRoot $rel
    if (-not (Test-Path $other)) { $results += [pscustomobject]@{ File = $rel; Result = 'MISSING'; Detail = '' }; continue }
    $ga = [IO.File]::ReadAllBytes($f.FullName); $nb = [IO.File]::ReadAllBytes($other)
    $nText = [Text.Encoding]::UTF8.GetString($nb)
    foreach ($pair in $Replace) { $p = $pair -split '=>', 2; $nText = $nText.Replace($p[0], $p[1]) }
    $nb2 = [Text.Encoding]::UTF8.GetBytes($nText)
    if ($ga.Length -eq $nb2.Length -and -not (Compare-Object $ga $nb2 -SyncWindow 0)) { $results += [pscustomobject]@{ File = $rel; Result = 'BYTES-EQUAL'; Detail = $(if ($Replace) { 'after -Replace' } else { '' }) }; continue }
    if ($f.Extension -eq '.html') { $results += [pscustomobject]@{ File = $rel; Result = 'DIFF'; Detail = 'html differs (not XML-compared)' }; continue }
    $gx = New-Object Xml.XmlDocument; $gx.PreserveWhitespace = $false; $gx.LoadXml([Text.Encoding]::UTF8.GetString($ga).TrimStart([char]0xFEFF))
    $nx = New-Object Xml.XmlDocument; $nx.PreserveWhitespace = $false; $nx.LoadXml($nText.TrimStart([char]0xFEFF))
    $flag = $false
    $r = Compare-Node $gx.DocumentElement $nx.DocumentElement '' ([ref]$flag)
    if ($r) { $results += [pscustomobject]@{ File = $rel; Result = 'DIFF'; Detail = $r } }
    elseif ($flag) { $results += [pscustomobject]@{ File = $rel; Result = 'NSDECL-ORDER'; Detail = 'same infoset; xmlns declaration order differs' } }
    else { $results += [pscustomobject]@{ File = $rel; Result = 'XML-EQUAL'; Detail = 'formatting only' } }
}
$results | Format-Table -AutoSize -Wrap | Out-String -Width 250
