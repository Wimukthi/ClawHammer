param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot "..\\ClawHammer\\ClawHammer.vbproj"),
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$PublishDir = "",
    [string]$NotesPath = "",
    [string]$Title = "",
    [string]$Tag = "",
    [string]$AssetName = "",
    [string]$Owner = "Wimukthi",
    [string]$Repo = "ClawHammer",
    [string]$Token = "",
    [switch]$Draft,
    [switch]$Prerelease,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = $env:GITHUB_TOKEN
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    $Token = $env:GH_TOKEN
}
if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "Missing GitHub token. Set GITHUB_TOKEN or GH_TOKEN."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "artifacts\\publish\\$Runtime"
}

if (-not $SkipBuild) {
    $selfContainedArg = if ($SelfContained) { "true" } else { "false" }
    Write-Host "Publishing $Runtime ($Configuration, self-contained=$selfContainedArg)..."
    dotnet publish $ProjectPath -c $Configuration -r $Runtime --self-contained $selfContainedArg -o $PublishDir
}

$exePath = Join-Path $PublishDir "ClawHammer.exe"
if (-not (Test-Path $exePath)) {
    throw "ClawHammer.exe not found in $PublishDir"
}

$version = (Get-Item $exePath).VersionInfo.FileVersion
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Unable to read version from $exePath"
}

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = if ($version.StartsWith("v")) { $version } else { "v$version" }
}
if ([string]::IsNullOrWhiteSpace($Title)) {
    $Title = "ClawHammer $Tag"
}
if ([string]::IsNullOrWhiteSpace($AssetName)) {
    $AssetName = "ClawHammer_${Runtime}_$version.zip"
}

$notes = "Automated release $Tag."
if (-not [string]::IsNullOrWhiteSpace($NotesPath)) {
    if (-not (Test-Path $NotesPath)) {
        throw "Release notes file not found: $NotesPath"
    }
    $notes = Get-Content -Path $NotesPath -Raw
}

$releaseDir = Join-Path $repoRoot "artifacts\\release"
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

$zipPath = Join-Path $releaseDir $AssetName
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Write-Host "Creating package: $zipPath"
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $zipPath -Force

$headers = @{
    Authorization = "Bearer $Token"
    "User-Agent" = "ClawHammerReleasePublisher"
    Accept = "application/vnd.github+json"
}

$payload = @{
    tag_name = $Tag
    name = $Title
    body = $notes
    draft = [bool]$Draft
    prerelease = [bool]$Prerelease
} | ConvertTo-Json -Depth 6

$createUrl = "https://api.github.com/repos/$Owner/$Repo/releases"
Write-Host "Creating GitHub release $Tag..."
$release = Invoke-RestMethod -Method Post -Uri $createUrl -Headers $headers -Body $payload

$uploadUrl = $release.upload_url -replace "\{\?name,label\}", ""
$uploadUri = "$uploadUrl?name=$([uri]::EscapeDataString($AssetName))"

Write-Host "Uploading asset $AssetName..."
Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -InFile $zipPath -ContentType "application/zip" | Out-Null

Write-Host "Release published: $Tag"
