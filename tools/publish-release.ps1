param(
    [string]$ProjectPath = (Join-Path $PSScriptRoot "..\\ClawHammer\\ClawHammer.vbproj"),
    [string]$NativeProjectPath = (Join-Path $PSScriptRoot "..\\ClawHammer.NativeCore\\ClawHammer.NativeCore.vcxproj"),
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $false,
    [bool]$SingleFile = $false,
    [bool]$EnableSingleFileCompression = $false,
    [ValidateSet("lean", "full")]
    [string]$PackageLayout = "lean",
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
    [switch]$SkipBuild,
    [switch]$SkipNativeBuild,
    [switch]$SkipUpload
)

$ErrorActionPreference = "Stop"

function Resolve-GitHubToken([string]$explicitToken) {
    if (-not [string]::IsNullOrWhiteSpace($explicitToken)) {
        return $explicitToken
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        return $env:GITHUB_TOKEN
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
        return $env:GH_TOKEN
    }

    return ""
}

function TryGetReleaseByTag([string]$owner, [string]$repo, [string]$tag, [hashtable]$headers) {
    $uri = "https://api.github.com/repos/$owner/$repo/releases/tags/$tag"
    try {
        return Invoke-RestMethod -Method Get -Uri $uri -Headers $headers
    }
    catch {
        $message = $_.Exception.Message
        if ($message -like "*404*") {
            return $null
        }
        throw
    }
}

function Copy-DirectoryContent([string]$sourceDir, [string]$destDir) {
    if (-not (Test-Path $sourceDir)) {
        return
    }

    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir | Out-Null
    }

    Get-ChildItem -Path $sourceDir -Force | ForEach-Object {
        $destinationPath = Join-Path $destDir $_.Name
        if ($_.PSIsContainer) {
            Copy-Item -Path $_.FullName -Destination $destinationPath -Recurse -Force
        }
        else {
            Copy-Item -Path $_.FullName -Destination $destinationPath -Force
        }
    }
}

function Resolve-MSBuildPath() {
    if (-not [string]::IsNullOrWhiteSpace($env:MSBUILD_EXE_PATH) -and (Test-Path $env:MSBUILD_EXE_PATH)) {
        return $env:MSBUILD_EXE_PATH
    }

    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\\Installer\\vswhere.exe"
    if (Test-Path $vswhere) {
        $path = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\\**\\Bin\\amd64\\MSBuild.exe" | Select-Object -First 1
        if (-not [string]::IsNullOrWhiteSpace($path) -and (Test-Path $path)) {
            return $path
        }
    }

    $fallbacks = @(
        "C:\\Program Files\\Microsoft Visual Studio\\18\\Insiders\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\amd64\\MSBuild.exe"
    )

    return $fallbacks | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function Resolve-NativePlatform([string]$runtime) {
    switch -Regex ($runtime) {
        "x64$" { return "x64" }
        "x86$" { return "Win32" }
        default { return "" }
    }
}

function Resolve-ManagedPlatform([string]$runtime) {
    switch -Regex ($runtime) {
        "x64$" { return "x64" }
        "x86$" { return "x86" }
        default { return "" }
    }
}
function Copy-LeanPublishContent([string]$sourceDir, [string]$destDir) {
    if (-not (Test-Path $sourceDir)) {
        return
    }

    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir | Out-Null
    }

    $excludedExtensions = @(".pdb", ".xml", ".md", ".map", ".iobj", ".ipdb", ".exp", ".lib")
    $excludedFileNames = @("createdump.exe", "clawhammer.defaultplugins.dll")

    Get-ChildItem -Path $sourceDir -File | ForEach-Object {
        $ext = [System.IO.Path]::GetExtension($_.Name).ToLowerInvariant()
        if ($excludedExtensions -contains $ext) {
            return
        }
        if ($excludedFileNames -contains $_.Name.ToLowerInvariant()) {
            return
        }

        Copy-Item -Path $_.FullName -Destination (Join-Path $destDir $_.Name) -Force
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$nativeProjectFullPath = Resolve-Path $NativeProjectPath
$nativePlatform = Resolve-NativePlatform -runtime $Runtime
$managedPlatform = Resolve-ManagedPlatform -runtime $Runtime

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "artifacts\\publish\\$Runtime"
}

$packageDir = Join-Path $repoRoot "artifacts\\package\\$Runtime"
$releaseDir = Join-Path $repoRoot "artifacts\\release"

if (-not $SkipBuild) {
    if (Test-Path $PublishDir) {
        Remove-Item -Path (Join-Path $PublishDir "*") -Force -Recurse -ErrorAction SilentlyContinue
    }

    if (-not $SkipNativeBuild) {
        if ([string]::IsNullOrWhiteSpace($nativePlatform)) {
            throw "No native platform mapping is defined for runtime '$Runtime'."
        }

        $msbuildPath = Resolve-MSBuildPath
        if ([string]::IsNullOrWhiteSpace($msbuildPath)) {
            throw "MSBuild.exe was not found. Install Visual Studio with C++ build tools, or set MSBUILD_EXE_PATH."
        }

        Write-Host "Building native core ($Configuration|$nativePlatform)..."
        & $msbuildPath $nativeProjectFullPath /t:Build /p:Configuration=$Configuration /p:Platform=$nativePlatform /v:minimal
        if ($LASTEXITCODE -ne 0) {
            throw "Native core build failed."
        }
    }

    $selfContainedArg = if ($SelfContained) { "true" } else { "false" }
    $publishArgs = @(
        $ProjectPath,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", $selfContainedArg,
        "-o", $PublishDir,
        "/p:Platform=$managedPlatform",
        "/p:CopyOutputSymbolsToPublishDirectory=false"
    )

    if ($SingleFile) {
        $publishArgs += "/p:PublishSingleFile=true"
        $publishArgs += "/p:IncludeNativeLibrariesForSelfExtract=true"

        if ($EnableSingleFileCompression) {
            $publishArgs += "/p:EnableCompressionInSingleFile=true"
        }
    }

    Write-Host "Publishing $Runtime ($Configuration, self-contained=$selfContainedArg, single-file=$SingleFile, layout=$PackageLayout)..."
    dotnet publish @publishArgs
}

if (-not [string]::IsNullOrWhiteSpace($nativePlatform)) {
    $nativeOutputDir = Join-Path $repoRoot "artifacts\\native\\$Configuration\\$nativePlatform"
    $nativeDll = Join-Path $nativeOutputDir "ClawHammer.NativeCore.dll"
    if (-not (Test-Path $nativeDll)) {
        throw "Native core DLL not found: $nativeDll"
    }
    Copy-Item -Path $nativeDll -Destination (Join-Path $PublishDir "ClawHammer.NativeCore.dll") -Force
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

if (Test-Path $packageDir) {
    Remove-Item -Path $packageDir -Force -Recurse
}
New-Item -ItemType Directory -Path $packageDir | Out-Null

if ($PackageLayout -eq "full") {
    Copy-DirectoryContent -sourceDir $PublishDir -destDir $packageDir
}
else {
    Copy-LeanPublishContent -sourceDir $PublishDir -destDir $packageDir


    $iconsSourceDir = Join-Path $PublishDir "icons"
    if (Test-Path $iconsSourceDir) {
        Copy-Item -Path $iconsSourceDir -Destination (Join-Path $packageDir "icons") -Recurse -Force
    }

    $pluginsDestDir = Join-Path $packageDir "plugins"
    if (-not (Test-Path $pluginsDestDir)) {
        New-Item -ItemType Directory -Path $pluginsDestDir | Out-Null
    }

    $defaultPluginCandidates = @(
        (Join-Path $PublishDir "plugins\\ClawHammer.DefaultPlugins.dll"),
        (Join-Path $PublishDir "ClawHammer.DefaultPlugins.dll"),
        (Join-Path $repoRoot "ClawHammer.DefaultPlugins\\bin\\$Configuration\\net10.0\\ClawHammer.DefaultPlugins.dll")
    )

    $defaultPluginPath = $defaultPluginCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($defaultPluginPath)) {
        throw "Unable to locate ClawHammer.DefaultPlugins.dll to include in package."
    }
    Copy-Item -Path $defaultPluginPath -Destination (Join-Path $pluginsDestDir "ClawHammer.DefaultPlugins.dll") -Force

    $contractsCandidates = @(
        (Join-Path $PublishDir "ClawHammer.PluginContracts.dll"),
        (Join-Path $repoRoot "ClawHammer.PluginContracts\\bin\\$Configuration\\net10.0\\ClawHammer.PluginContracts.dll")
    )
    $contractsPath = $contractsCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not [string]::IsNullOrWhiteSpace($contractsPath)) {
        Copy-Item -Path $contractsPath -Destination (Join-Path $packageDir "ClawHammer.PluginContracts.dll") -Force
    }

    $publishedPluginsDir = Join-Path $PublishDir "plugins"
    if (Test-Path $publishedPluginsDir) {
        Get-ChildItem -Path $publishedPluginsDir -Filter "*.dll" | ForEach-Object {
            $destPath = Join-Path $pluginsDestDir $_.Name
            if (-not (Test-Path $destPath)) {
                Copy-Item -Path $_.FullName -Destination $destPath -Force
            }
        }
    }
}

$packageFileCount = (Get-ChildItem -Path $packageDir -Recurse -File | Measure-Object).Count
Write-Host "Package layout '$PackageLayout' prepared with $packageFileCount file(s)."

if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

$zipPath = Join-Path $releaseDir $AssetName
if (Test-Path $zipPath) {
    Remove-Item -Path $zipPath -Force
}

Write-Host "Creating package: $zipPath"
Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zipPath -Force

if ($SkipUpload) {
    Write-Host "SkipUpload enabled. Package created at $zipPath"
    return
}

$Token = Resolve-GitHubToken -explicitToken $Token
if ([string]::IsNullOrWhiteSpace($Token)) {
    throw "Missing GitHub token. Set GITHUB_TOKEN or GH_TOKEN, or pass -Token."
}

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

$release = TryGetReleaseByTag -owner $Owner -repo $Repo -tag $Tag -headers $headers
if ($null -eq $release) {
    $createUrl = "https://api.github.com/repos/$Owner/$Repo/releases"
    Write-Host "Creating GitHub release $Tag..."
    $release = Invoke-RestMethod -Method Post -Uri $createUrl -Headers $headers -Body $payload
}
else {
    $updateUrl = "https://api.github.com/repos/$Owner/$Repo/releases/$($release.id)"
    Write-Host "Updating existing GitHub release $Tag..."
    $release = Invoke-RestMethod -Method Patch -Uri $updateUrl -Headers $headers -Body $payload
}

$existingAsset = $null
if ($release.assets) {
    $existingAsset = $release.assets | Where-Object { $_.name -eq $AssetName } | Select-Object -First 1
}
if ($null -ne $existingAsset) {
    $deleteUrl = "https://api.github.com/repos/$Owner/$Repo/releases/assets/$($existingAsset.id)"
    Write-Host "Removing existing asset $AssetName..."
    Invoke-RestMethod -Method Delete -Uri $deleteUrl -Headers $headers | Out-Null
}

$uploadUrl = $release.upload_url -replace "\{\?name,label\}", ""
$uploadUri = "${uploadUrl}?name=$([uri]::EscapeDataString($AssetName))"

Write-Host "Uploading asset $AssetName..."
Invoke-RestMethod -Method Post -Uri $uploadUri -Headers $headers -InFile $zipPath -ContentType "application/zip" | Out-Null

Write-Host "Release published: $Tag"
Write-Host "Asset URL: https://github.com/$Owner/$Repo/releases/download/$Tag/$AssetName"
