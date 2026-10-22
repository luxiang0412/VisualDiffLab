param(
    [int]$X,
    [int]$Y,
    [string]$Keys,
    [string]$ProcessName = "VisualDiffLab",
    [string]$OutputPath,
    [int]$WaitMilliseconds = 1200
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class UiDriver {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    public const uint LEFTDOWN = 0x0002;
    public const uint LEFTUP = 0x0004;
}
"@

$proc = Get-Process -Name $ProcessName -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { throw "Process $ProcessName has no window." }

$wshell = New-Object -ComObject WScript.Shell
$wshell.AppActivate($proc.Id) | Out-Null
[UiDriver]::SetForegroundWindow($proc.MainWindowHandle) | Out-Null
Start-Sleep -Milliseconds 700

[UiDriver]::SetCursorPos($X, $Y) | Out-Null
Start-Sleep -Milliseconds 200
[UiDriver]::mouse_event([UiDriver]::LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 80
[UiDriver]::mouse_event([UiDriver]::LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 350

if ($Keys) {
    [System.Windows.Forms.SendKeys]::SendWait($Keys)
}

Start-Sleep -Milliseconds $WaitMilliseconds

if ($OutputPath) {
    & "$PSScriptRoot\capture-window.ps1" -ProcessName $ProcessName -OutputPath $OutputPath -Foreground | Out-Null
    Write-Output "Saved $OutputPath"
}
