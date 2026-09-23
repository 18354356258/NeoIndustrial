$csPath = Join-Path $PSScriptRoot 'Utils\IndustrialVocabulary.cs'
if (-not (Test-Path $csPath)) {
    # Try alternate path
    $csPath = 'C:\Users\Administrator\Desktop\C#\IndustrialDataCollection' + [char]0x2161 + '\IndustrialDataCollection\Utils\IndustrialVocabulary.cs'
}
Write-Host "Checking: $csPath"
Write-Host "Exists: $(Test-Path $csPath)"
$content = Get-Content $csPath -Raw
$pattern = '\{"([^"]+)",\s*"([^"]+)"\}'
$matches = [regex]::Matches($content, $pattern)
$seen = @{}
$dupes = @()
foreach ($m in $matches) {
    $key = $m.Groups[1].Value
    $val = $m.Groups[2].Value
    if ($seen.ContainsKey($key)) {
        $dupes += "DUPLICATE: '$key' => first='$($seen[$key])', second='$val'"
    } else {
        $seen[$key] = $val
    }
}
if ($dupes.Count -eq 0) {
    Write-Host "NO duplicates found. Total unique keys: $($seen.Count)"
} else {
    Write-Host "DUPLICATES FOUND:"
    $dupes | ForEach-Object { Write-Host $_ }
    Write-Host "Total unique keys: $($seen.Count)"
}
