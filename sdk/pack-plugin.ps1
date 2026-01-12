[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PluginDll,

    [string]$OutputPath,

    [switch]$IncludePdb
)

if (-not (Test-Path -Path $PluginDll)) {
    throw "Plugin DLL not found: $PluginDll"
}

$pluginDll = Resolve-Path -Path $PluginDll
$baseName = [IO.Path]::GetFileNameWithoutExtension($pluginDll)
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path -Path (Split-Path -Path $pluginDll) -ChildPath ($baseName + ".zip")
}

$files = @($pluginDll)
if ($IncludePdb) {
    $pdbPath = [IO.Path]::ChangeExtension($pluginDll, ".pdb")
    if (Test-Path -Path $pdbPath) {
        $files += $pdbPath
    }
}

if (Test-Path -Path $OutputPath) {
    Remove-Item -Path $OutputPath -Force
}

Compress-Archive -Path $files -DestinationPath $OutputPath
Write-Host "Created plugin package: $OutputPath"
