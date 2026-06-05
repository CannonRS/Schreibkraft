param(
    [switch]$RemoveUserData
)

$ErrorActionPreference = "Stop"

$target = Join-Path $env:LOCALAPPDATA "Programs\Schreibkraft"
$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Schreibkraft.lnk"
$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$desktopLink = Join-Path $desktop "Schreibkraft.lnk"
$runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$userData = Join-Path $env:LOCALAPPDATA "Schreibkraft"

function Stop-SchreibkraftForUninstall {
    for ($i = 0; $i -lt 8; $i++) {
        $running = Get-Process -Name "Schreibkraft" -ErrorAction SilentlyContinue
        if (-not $running) {
            return
        }

        if ($i -eq 0) {
            Write-Host "Beende laufende Schreibkraft vor der Deinstallation ..."
        }

        $running | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Milliseconds 500
    }

    if (Get-Process -Name "Schreibkraft" -ErrorAction SilentlyContinue) {
        throw "Schreibkraft läuft noch und blockiert die Deinstallation. Bitte App schließen und Uninstall.ps1 erneut ausführen."
    }
}

function Remove-DirectoryRobust {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    Get-ChildItem -LiteralPath $Path -Recurse -Force -ErrorAction SilentlyContinue |
        ForEach-Object { $_.Attributes = $_.Attributes -band (-bnot [System.IO.FileAttributes]::ReadOnly) }

    for ($i = 0; $i -lt 5; $i++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($i -eq 4) {
                throw "Ordner konnte nicht entfernt werden: $Path`n$($_.Exception.Message)`nFalls Schreibkraft gerade geschlossen wurde, einige Sekunden warten oder Windows neu starten und Uninstall.ps1 erneut ausführen."
            }

            Start-Sleep -Milliseconds 700
        }
    }
}

Stop-SchreibkraftForUninstall
Remove-Item -Path $startMenu -Force -ErrorAction SilentlyContinue
Remove-Item -Path $desktopLink -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $runKey -Name "Schreibkraft" -ErrorAction SilentlyContinue

Remove-DirectoryRobust -Path $target

if ($RemoveUserData) {
    Remove-DirectoryRobust -Path $userData
}

Write-Host "Schreibkraft wurde deinstalliert. Nutzerdaten wurden nur mit -RemoveUserData entfernt."
