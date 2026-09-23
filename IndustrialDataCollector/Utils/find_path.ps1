$basePath = "C:\Users\Administrator\Desktop\C#\"
$items = Get-ChildItem -Path $basePath -Directory
foreach ($item in $items) {
    if ($item.Name -like "*Industrial*") {
        Write-Host $item.FullName
    }
}
