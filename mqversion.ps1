param(
    [Parameter(Position = 0)]
    [ValidateSet("get", "set")]
    [string]$Command = "get",

    [Parameter(Position = 1)]
    [string]$Version
)

$ErrorActionPreference = "Stop"

function Test-Version {
    param([string]$Value)

    if ($Value -notmatch '^\d+\.\d+\.\d+$') {
        throw "Version '$Value' ist ungültig. Erwartet wird z. B. 1.2.3."
    }
}

function Set-TextFileVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Pattern,
        [Parameter(Mandatory = $true)]
        [string]$Replacement
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Versionsziel nicht gefunden: $Path"
    }

    $content = Get-Content -LiteralPath $Path -Raw
    if (-not [regex]::IsMatch($content, $Pattern)) {
        throw "Versionsmuster wurde in $Path nicht gefunden: $Pattern"
    }

    $updated = [regex]::Replace($content, $Pattern, $Replacement)
    Set-Content -LiteralPath $Path -Value $updated -NoNewline
}

$propsPath = Join-Path $PSScriptRoot "Directory.Build.props"
$readmePath = Join-Path $PSScriptRoot "README.md"
$installerPath = Join-Path $PSScriptRoot "installer\Schreibkraft.iss"
$clickOnceProfilePath = Join-Path $PSScriptRoot "src\Schreibkraft\Properties\PublishProfiles\ClickOnceProfile.pubxml"

if (-not (Test-Path -LiteralPath $propsPath)) {
    throw "Versionsdatei nicht gefunden: $propsPath"
}

if ($Command -eq "get") {
    [xml]$xml = Get-Content -LiteralPath $propsPath
    $node = $xml.SelectSingleNode("/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='AppVersion']")
    if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) {
        throw "AppVersion wurde in $propsPath nicht gefunden."
    }

    Write-Output $node.InnerText.Trim()
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    throw "Für 'set' muss eine Version angegeben werden: .\mqversion.ps1 set 1.2.3"
}

Test-Version -Value $Version

Set-TextFileVersion `
    -Path $propsPath `
    -Pattern '<AppVersion>[^<]+</AppVersion>' `
    -Replacement "<AppVersion>$Version</AppVersion>"

Set-TextFileVersion `
    -Path $readmePath `
    -Pattern '\*\*Aktuelle Version:\*\*\s+\d+\.\d+\.\d+' `
    -Replacement "**Aktuelle Version:** $Version"

Set-TextFileVersion `
    -Path $installerPath `
    -Pattern '#define AppVersion "\d+\.\d+\.\d+"' `
    -Replacement "#define AppVersion `"$Version`""

Set-TextFileVersion `
    -Path $clickOnceProfilePath `
    -Pattern '<ApplicationVersion>\d+\.\d+\.\d+\.\d+</ApplicationVersion>' `
    -Replacement "<ApplicationVersion>$Version.0</ApplicationVersion>"

Write-Output $Version
