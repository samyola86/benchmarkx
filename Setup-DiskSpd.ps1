$ErrorActionPreference = 'Stop'
$base = Split-Path -Parent $MyInvocation.MyCommand.Path
$target = Join-Path $base 'tools'
New-Item -ItemType Directory -Path $target -Force | Out-Null
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ('overclock-' + [guid]::NewGuid())
New-Item -ItemType Directory -Path $temp | Out-Null
try {
    Invoke-WebRequest -Uri 'https://github.com/microsoft/diskspd/releases/latest/download/DiskSpd.zip' -OutFile (Join-Path $temp 'DiskSpd.zip')
    Expand-Archive (Join-Path $temp 'DiskSpd.zip') (Join-Path $temp 'unpacked')
    $exe = Get-ChildItem (Join-Path $temp 'unpacked') -Filter diskspd.exe -Recurse | Where-Object {$_.DirectoryName -match 'amd64|x64'} | Select-Object -First 1
    if (-not $exe) { throw 'No x64 DiskSpd binary found; install manually from Microsoft.' }
    Copy-Item -LiteralPath $exe.FullName -Destination (Join-Path $target 'diskspd.exe') -Force
    Write-Host 'DiskSpd installed in Overclock tools.'
} finally {
    Remove-Item -LiteralPath $temp -Recurse -Force
}
