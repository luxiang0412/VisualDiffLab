$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$outDir = Join-Path $root "artifacts\phase-0"
New-Item -ItemType Directory -Force $outDir | Out-Null

$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

& $dotnet --info 2>&1 | Tee-Object (Join-Path $outDir "dotnet-info.txt")
& $dotnet --list-sdks 2>&1 | Tee-Object (Join-Path $outDir "dotnet-sdks.txt")
& $dotnet --list-runtimes 2>&1 | Tee-Object (Join-Path $outDir "dotnet-runtimes.txt")
& $dotnet new list 2>&1 | Tee-Object (Join-Path $outDir "dotnet-templates.txt")

Get-ComputerInfo |
    Select-Object WindowsProductName, WindowsVersion, OsBuildNumber, OsArchitecture |
    Format-List |
    Out-File (Join-Path $outDir "windows-info.txt")

Get-Command msbuild -ErrorAction SilentlyContinue |
    Format-List * |
    Out-File (Join-Path $outDir "msbuild-command.txt")

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (Test-Path $vswhere) {
    & $vswhere -all -format json | Out-File (Join-Path $outDir "visual-studio.json")
    & $vswhere -products * -format json -include packages |
        Out-File (Join-Path $outDir "visual-studio-packages.json")
}

Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\Include" -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Name |
    Out-File (Join-Path $outDir "windows-sdk-include-versions.txt")

git --version 2>&1 | Out-File (Join-Path $outDir "git-version.txt")
node --version 2>&1 | Out-File (Join-Path $outDir "node-version.txt")

$packages = Join-Path $env:USERPROFILE ".nuget\packages"
$win2d = Get-ChildItem (Join-Path $packages "microsoft.graphics.win2d") -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Name
$wasdk = Get-ChildItem (Join-Path $packages "microsoft.windowsappsdk") -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Name
"Microsoft.Graphics.Win2D installed versions: $($win2d -join ', ')" |
    Out-File (Join-Path $outDir "nuget-render-packages.txt")
"Microsoft.WindowsAppSDK installed versions: $($wasdk -join ', ')" |
    Add-Content (Join-Path $outDir "nuget-render-packages.txt")
"Win2D 1.4.0 license URL: https://aka.ms/win2d-license (NuGet metadata: Microsoft.Graphics.Win2D 1.4.0)" |
    Add-Content (Join-Path $outDir "nuget-render-packages.txt")

$process = Get-Process -Id $PID
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
@(
    "User: $env:USERNAME",
    "Interactive session: $([Environment]::UserInteractive)",
    "Administrator: $isAdmin",
    "Session name: $env:SESSIONNAME",
    "Processor: $((Get-CimInstance Win32_Processor | Select-Object -First 1).Name)",
    "Logical processors: $((Get-CimInstance Win32_Processor | Select-Object -First 1).NumberOfLogicalProcessors)",
    "Total physical memory bytes: $((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory)",
    "GPU: $((Get-CimInstance Win32_VideoController | Where-Object { $_.Name -like '*Radeon*' } | Select-Object -First 1).Name)",
    "GPU driver: $((Get-CimInstance Win32_VideoController | Where-Object { $_.Name -like '*Radeon*' } | Select-Object -First 1).DriverVersion)"
) | Out-File (Join-Path $outDir "session-info.txt")

Write-Output "Phase 0 environment evidence written to $outDir"
