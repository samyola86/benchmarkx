$principal=New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if(!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',('"'+$PSCommandPath+'"'))
    $process=Start-Process -FilePath powershell.exe -Verb RunAs -ArgumentList $arguments -PassThru
    $process.WaitForExit()
    exit $process.ExitCode
}
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$exe = Join-Path $PSScriptRoot 'dist\BenchMarkX.exe'
$folder = Join-Path $PSScriptRoot 'tests'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
function Test-Worker($kind, $timeout, [string[]]$extra = @()) {
    $out = Join-Path $folder "$kind-result.txt"
    $arguments = @('--worker', $kind, ('"' + $out + '"')) + $extra
    $p = Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$p.WaitForExit($timeout * 1000)) { taskkill.exe /PID $p.Id /T /F | Out-Null; throw "$kind timed out" }
    if ($p.ExitCode -ne 0 -or !(Test-Path -LiteralPath $out)) { if(Test-Path -LiteralPath $out){Get-Content -LiteralPath $out}; throw "$kind failed" }
    Get-Content -LiteralPath $out
}
& cmd.exe /c Build-Windows.cmd
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& tools\diskspd.exe -Rxml -Su -L -W0 -C0 -b4K -o1 -t1 -d1 -w0 $exe | Set-Content -LiteralPath (Join-Path $folder 'diskspd-readonly.xml') -Encoding UTF8
if ($LASTEXITCODE -ne 0) { throw 'Read-only DiskSpd validation failed' }
Test-Worker 'test-suite' 20 @(('"' + $folder + '"'))
$delayOut = Join-Path $folder 'delay-result.txt'
$delayed = Start-Process -FilePath $exe -ArgumentList @('--worker','delay-test',('"'+$delayOut+'"')) -WindowStyle Hidden -PassThru
try { Test-Worker 'basic' 8; if ($delayed.HasExited) { throw 'Blocked worker did not remain independent' }; Write-Output 'PASS responsive CPU worker while another worker is blocked' }
finally { if (!$delayed.HasExited) { taskkill.exe /PID $delayed.Id /T /F | Out-Null } }
foreach ($kind in @('gpu','network','cores','activity','disks')) { Test-Worker $kind 50 }
$diskRows = Get-Content (Join-Path $folder 'disks-result.txt')
foreach($line in $diskRows) {
    if(!$line.StartsWith('Disk #')){continue}
    $number=$line.Split('|')[0].Replace('Disk #','').Trim()
    $identity=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($line))
    Test-Worker 'mapping' 30 @($number,$identity)
    Copy-Item (Join-Path $folder 'mapping-result.txt') (Join-Path $folder "mapping-$number.txt") -Force
    Test-Worker 'health-disk' 30 @($number)
    Copy-Item (Join-Path $folder 'health-disk-result.txt') (Join-Path $folder "health-disk-$number.txt") -Force
}
$snapshot=Join-Path $folder 'sensor-service.json'
foreach($suffix in @('', '.cancel', '.reset', '.interval', '.tmp')) { $file=$snapshot+$suffix; if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file -Force} }
$p=Start-Process -FilePath $exe -ArgumentList @('--worker','sensor-service',('"'+$snapshot+'"'),$PID) -WindowStyle Hidden -PassThru
try {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(!(Test-Path -LiteralPath $snapshot) -and $watch.Elapsed.TotalSeconds -lt 25 -and !$p.HasExited){Start-Sleep -Milliseconds 200}
    $data=Get-Content -LiteralPath $snapshot -Raw | ConvertFrom-Json
    if($data.Sensors.Count -eq 0){throw 'No real sensors collected'}
    Write-Output ('PASS isolated direct library collection: '+$data.Sensors.Count+' sensors')
    Set-Content -LiteralPath ($snapshot+'.reset') -Value reset
    Start-Sleep -Seconds 3
    if(Test-Path -LiteralPath ($snapshot+'.reset')){throw 'Sensor min/max reset not acknowledged'}
    Write-Output 'PASS sensor min/max reset acknowledged'
} finally { Set-Content -LiteralPath ($snapshot+'.cancel') -Value stop; if(!$p.WaitForExit(5000)){taskkill.exe /PID $p.Id /T /F | Out-Null} }
$p=Start-Process -FilePath $exe -ArgumentList @('--ui-test',('"'+$folder+'"')) -PassThru
if(!$p.WaitForExit(30000)){taskkill.exe /PID $p.Id /T /F | Out-Null;throw 'UI navigation timed out'}
if($p.ExitCode -ne 0){throw 'UI navigation failed'}
Get-Content (Join-Path $folder 'ui-results.txt')
Write-Output 'PASS safe verification completed. No SSD write benchmark or GPU stress was executed.'

