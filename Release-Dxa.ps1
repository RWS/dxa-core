<#
.SYNOPSIS
    Releases DXA 2.x packages to Nexus (and optionally NuGet.org) in dependency order.

.DESCRIPTION
    Bypasses build.proj's SignPackAndPushNuGetPackages target (which auto-stamps a
    beta-{timestamp} suffix) so we get clean, stable version packages - e.g. 2.4.1
    instead of 2.4.1-beta-20260514153012.

    With -Preview, packs prerelease packages as 2.4.1-preview-{yyyyMMddHHmmss} and
    pushes them to internal Nexus only (nuget.org is refused).

    Release order:
      1. Tridion.Dxa.Framework.DataModel   (from dxa-framework-datamodel)
      2. Tridion.Dxa.Api.Client            (from dxa-pca-client-net)
      3. (update Tridion.Dxa.Framework.csproj package refs to new version)
      4. Tridion.Dxa.Framework             (from dxa-framework-mvc-net)
      5. Tridion.Dxa.Module.Core / Search / DynamicDocumentation
         (Framework package refs auto-bumped before each module builds)
      6. (update Example WebApp Framework package ref to new version)

    Each repo: msbuild /t:Build -> msbuild /t:SignAssemblies -> dotnet pack -> dotnet nuget push.

.PARAMETER Version
    Version prefix, default 2.4.1. Becomes VersionPrefix. For stable releases VersionSuffix
    is forced empty; with -Preview a preview-{timestamp} suffix is appended.

.PARAMETER NuGetSource
    Target NuGet feed URL. Defaults to internal Nexus.

.PARAMETER ApiKey
    Push API key. Defaults to the key already in build.proj.

.PARAMETER SkipSign
    Skip the SignAssemblies step (useful on machines without signing cert).

.PARAMETER SkipPush
    Build and pack only; don't push to the feed.

.PARAMETER DryRun
    Print the commands that would run, execute nothing.

.PARAMETER NonInteractive
    Skip the per-stage confirmation prompts.

.PARAMETER Preview
    Pack and push prerelease packages as {Version}-preview-{yyyyMMddHHmmss} to Nexus only.

.EXAMPLE
    .\Release-Dxa.ps1 -DryRun
    Show every command without running anything.

.EXAMPLE
    .\Release-Dxa.ps1 -SkipPush
    Local build + pack only, no push (smoke test).

.EXAMPLE
    .\Release-Dxa.ps1
    Full release of 2.4.1 to internal Nexus, with confirmation between stages.

.EXAMPLE
    .\Release-Dxa.ps1 -Preview -NonInteractive
    Pack 2.4.1-preview-{timestamp} packages and push to internal Nexus only.
#>
[CmdletBinding()]
param(
    [string]$Version       = "2.4.1",
    [string]$NuGetSource   = "https://nexus.sdl.com/service/local/nuget/releases_dotnet/",
    [string]$ApiKey        = "fed9a610-8898-3986-877e-1001ba1f858d",
    [switch]$SkipSign,
    [switch]$SkipPush,
    [switch]$DryRun,
    [switch]$NonInteractive,
    [switch]$Preview
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = $PSScriptRoot
if (-not $RepoRoot) { $RepoRoot = (Get-Location).Path }

if ($Preview -and ($NuGetSource -match 'nuget\.org')) {
    throw "-Preview pushes are Nexus-only. Do not combine -Preview with a nuget.org NuGetSource."
}

if ($Preview) {
    $VersionSuffix  = "preview-$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))"
    $PackageVersion = "$Version-$VersionSuffix"
} else {
    $VersionSuffix  = ""
    $PackageVersion = $Version
}

# ---- Release manifest -------------------------------------------------------
# Each entry describes one publishable package and how to build it.
$releases = @(
    @{
        Package      = 'Tridion.Dxa.Framework.DataModel'
        RepoDir      = 'dxa-framework-datamodel'
        SolutionPath = 'dotnet\Tridion.Dxa.Framework.DataModel.sln'
        ProjectPath  = 'dotnet\src\Tridion.Dxa.Framework.DataModel\Tridion.Dxa.Framework.DataModel.csproj'
        BuildProj    = 'dotnet\build\build.proj'
    },
    @{
        Package      = 'Tridion.Dxa.Api.Client'
        RepoDir      = 'dxa-pca-client-net'
        SolutionPath = 'dotnet\Tridion.Dxa.Api.Client.sln'
        ProjectPath  = 'dotnet\src\Tridion.Dxa.Api.Client\Tridion.Dxa.Api.Client.csproj'
        BuildProj    = 'dotnet\build\build.proj'
    },
    @{
        Package      = 'Tridion.Dxa.Framework'
        RepoDir      = 'dxa-framework-mvc-net'
        SolutionPath = 'dotnet\Tridion.Dxa.Framework.sln'
        ProjectPath  = 'dotnet\src\Tridion.Dxa.Framework\Tridion.Dxa.Framework.csproj'
        BuildProj    = 'dotnet\build\build.proj'
        # Before building this one, bump the package refs it consumes:
        UpdateRefsBefore = @(
            'Tridion.Dxa.Framework.DataModel',
            'Tridion.Dxa.Api.Client'
        )
    },
    @{
        Package      = 'Tridion.Dxa.Module.Core'
        RepoDir      = 'dxa-module-core-net'
        SolutionPath = 'dotnet\Tridion.Dxa.Module.Core.sln'
        ProjectPath  = 'dotnet\src\Tridion.Dxa.Module.Core\Tridion.Dxa.Module.Core.csproj'
        BuildProj    = 'dotnet\build\build.proj'
        UpdateRefsBefore = @('Tridion.Dxa.Framework')
    },
    @{
        Package      = 'Tridion.Dxa.Module.Search'
        RepoDir      = 'dxa-module-search-net'
        SolutionPath = 'dotnet\Tridion.Dxa.Module.Search.sln'
        ProjectPath  = 'dotnet\src\Tridion.Dxa.Module.Search\Tridion.Dxa.Module.Search.csproj'
        BuildProj    = 'dotnet\build\build.proj'
        UpdateRefsBefore = @('Tridion.Dxa.Framework')
    },
    @{
        Package      = 'Tridion.Dxa.Module.DynamicDocumentation'
        RepoDir      = 'dxa-module-dynamicdocumentation-net'
        SolutionPath = 'dotnet\Tridion.Dxa.Module.DynamicDocumentation.sln'
        ProjectPath  = 'dotnet\src\Tridion.Dxa.Module.DynamicDocumentation\Tridion.Dxa.Module.DynamicDocumentation.csproj'
        BuildProj    = 'dotnet\build\build.proj'
        UpdateRefsBefore = @('Tridion.Dxa.Framework')
    }
)

# Example WebApp - Framework ref updated AFTER all packages (including modules) are published.
$consumerProjects = @(
    'dxa-web-application-mvc-net\dotnet\src\Tridion.Dxa.Example.WebApp\Tridion.Dxa.Example.WebApp.csproj'
)

# Shared local NuGet source. Every freshly-built nupkg is copied here and we pass this folder
# to MSBuild as RestoreAdditionalProjectSources so downstream restores pick up the new versions
# locally (no Nexus propagation delay, works fully offline with -SkipPush).
#
# We do this instead of relying on each repo's Directory.Build.props "<LocalNugetStorage>../LocalNugetStorage</LocalNugetStorage>"
# because that path is resolved relative to the consuming .csproj's directory at evaluation time
# (so it points to "<repo>\dotnet\src\LocalNugetStorage", not "<repo>\LocalNugetStorage") -- and the
# Exists() check in the props condition fails, silently skipping the local source.
$SharedLocalStorage = Join-Path $RepoRoot 'LocalNugetStorage'

# ---- Helpers ----------------------------------------------------------------
function Write-Stage($msg) {
    Write-Host ""
    Write-Host "==== $msg ====" -ForegroundColor Cyan
}

function Invoke-Cmd($commandLine, [string]$workingDir = $null) {
    Write-Host "PS> $commandLine" -ForegroundColor DarkGray
    if ($workingDir) { Write-Host "    (in $workingDir)" -ForegroundColor DarkGray }
    if ($DryRun) { return }

    $prev = Get-Location
    try {
        if ($workingDir) { Set-Location $workingDir }
        & cmd.exe /c $commandLine
        if ($LASTEXITCODE -ne 0) { throw "Command failed (exit $LASTEXITCODE): $commandLine" }
    } finally {
        Set-Location $prev
    }
}

function Confirm-Continue($prompt) {
    if ($NonInteractive -or $DryRun) { return }
    $reply = Read-Host "$prompt  [Y/n]"
    if ($reply -and $reply -notmatch '^(y|yes)$') { throw "Aborted by user." }
}

function Update-PackageRef([string]$csprojRelPath, [string]$packageId, [string]$version) {
    $csproj = Join-Path $RepoRoot $csprojRelPath
    if (-not (Test-Path $csproj)) {
        Write-Host "  (skip - not found: $csprojRelPath)" -ForegroundColor Yellow
        return
    }
    Write-Host "  -> ${csprojRelPath}  |  ${packageId} ${version}" -ForegroundColor Gray
    if ($DryRun) { return }

    # dotnet add package will update an existing PackageReference's Version attribute idempotently.
    $proj = Split-Path $csproj
    Invoke-Cmd "dotnet add `"$csproj`" package $packageId --version $version --no-restore" $proj
}

function Get-NupkgPath($release, $version) {
    $projectDir = Join-Path $RepoRoot (Join-Path $release.RepoDir (Split-Path $release.ProjectPath))
    return Join-Path $projectDir "bin\Release\$($release.Package).$version.nupkg"
}

function Publish-LocalNupkg($nupkgPath) {
    Write-Host "PS> copy $(Split-Path -Leaf $nupkgPath) -> $SharedLocalStorage" -ForegroundColor DarkGray
    if ($DryRun) { return }
    if (-not (Test-Path $SharedLocalStorage)) {
        New-Item -ItemType Directory -Path $SharedLocalStorage -Force | Out-Null
    }
    Copy-Item -Path $nupkgPath -Destination $SharedLocalStorage -Force
}

# ---- Per-release pipeline ---------------------------------------------------
function Release-Package($release) {
    $repo      = Join-Path $RepoRoot $release.RepoDir
    $buildDir  = Join-Path $repo (Split-Path $release.BuildProj)
    $slnPath   = Join-Path $repo $release.SolutionPath
    $nupkgPath = Get-NupkgPath $release $PackageVersion

    if (-not (Test-Path $repo))     { throw "Repo dir not found: $repo" }
    if (-not (Test-Path $slnPath))  { throw "Solution not found: $slnPath" }
    if (-not (Test-Path $buildDir)) { throw "Build dir not found: $buildDir" }

    Write-Stage "Releasing $($release.Package) $PackageVersion  (from $($release.RepoDir))"

    # 1. Bump references this project consumes (only relevant for framework-mvc / modules).
    if ($release.ContainsKey('UpdateRefsBefore')) {
        Write-Host "Updating package references in $($release.ProjectPath) -> $PackageVersion" -ForegroundColor Yellow
        foreach ($depPkg in $release.UpdateRefsBefore) {
            Update-PackageRef -csprojRelPath (Join-Path $release.RepoDir $release.ProjectPath) `
                              -packageId     $depPkg `
                              -version       $PackageVersion
        }
    }

    # 2. Build (uses existing build.proj target - produces a-local-... nupkg for sanity).
    #    /p:RestoreAdditionalProjectSources points NuGet at our shared LocalNugetStorage so
    #    refs to freshly-published packages resolve locally.
    #    /p:RestoreForce=true /p:RestoreNoHttpCache=true bypasses NuGet's HTTP cache from any earlier failed restore.
    Invoke-Cmd "dotnet msbuild build.proj /t:Build /p:BuildConfiguration=Release /p:VersionPrefix=$Version /p:RestoreAdditionalProjectSources=`"$SharedLocalStorage`" /p:RestoreForce=true /p:RestoreNoHttpCache=true" $buildDir

    # 3. Sign the built assemblies (uses the existing target).
    if (-not $SkipSign) {
        Invoke-Cmd "dotnet msbuild build.proj /t:SignAssemblies /p:BuildConfiguration=Release" $buildDir
    } else {
        Write-Host "(SignAssemblies skipped)" -ForegroundColor Yellow
    }

    # 4. Pack: empty VersionSuffix for stable; preview-{timestamp} for -Preview.
    #    --no-build reuses what we just compiled. Overrides Directory.Build.props defaults.
    $suffixArg = if ($null -eq $VersionSuffix -or $VersionSuffix -eq '') { '""' } else { "`"$VersionSuffix`"" }
    Invoke-Cmd "dotnet pack `"$slnPath`" --configuration Release --no-build /p:VersionSuffix=$suffixArg /p:VersionPrefix=$Version /p:RestoreAdditionalProjectSources=`"$SharedLocalStorage`"" $repo

    # 5. Verify the package exists where we expect it.
    if (-not $DryRun) {
        if (-not (Test-Path $nupkgPath)) {
            throw "Expected package not found after pack: $nupkgPath"
        }
        Write-Host "Produced: $nupkgPath" -ForegroundColor Green
    }

    # 5b. Copy the nupkg into the shared LocalNugetStorage so the next project's
    #     restore picks it up via /p:RestoreAdditionalProjectSources (works fully offline with
    #     -SkipPush, and avoids latency-induced "package not found" on real Nexus runs).
    Publish-LocalNupkg $nupkgPath

    # 6. Push.
    if (-not $SkipPush) {
        Invoke-Cmd "dotnet nuget push `"$nupkgPath`" --api-key $ApiKey --source $NuGetSource --skip-duplicate"
    } else {
        Write-Host "(Push skipped)" -ForegroundColor Yellow
    }

    Confirm-Continue "Released $($release.Package) $PackageVersion. Continue?"
}

# ---- Main flow --------------------------------------------------------------
$releaseKind = if ($Preview) { "preview" } else { "stable" }
Write-Stage "DXA $PackageVersion $releaseKind release"
Write-Host "Repo root      : $RepoRoot"
Write-Host "Package version: $PackageVersion"
Write-Host "Version prefix : $Version"
Write-Host "Version suffix : $(if ($VersionSuffix) { $VersionSuffix } else { '(none)' })"
Write-Host "NuGet source   : $NuGetSource"
Write-Host "Dry run        : $DryRun"
Write-Host "Skip sign      : $SkipSign"
Write-Host "Skip push      : $SkipPush"
Write-Host "Preview        : $Preview"
Write-Host "Non-interactive: $NonInteractive"

# Pre-create the shared LocalNugetStorage so the very first msbuild restore (which has
# /p:RestoreAdditionalProjectSources pointing here) doesn't fail with NU1301 on a fresh
# checkout (e.g. on CI runners, where the folder is gitignored and absent until first pack).
if (-not (Test-Path $SharedLocalStorage)) {
    Write-Host "Creating shared local source: $SharedLocalStorage" -ForegroundColor DarkGray
    if (-not $DryRun) { New-Item -ItemType Directory -Path $SharedLocalStorage -Force | Out-Null }
}

Confirm-Continue "Proceed with release of ${PackageVersion}?"

foreach ($r in $releases) {
    Release-Package $r
}

# Update Example WebApp to reference the new Tridion.Dxa.Framework
Write-Stage "Updating Example WebApp to Tridion.Dxa.Framework $PackageVersion"
foreach ($csproj in $consumerProjects) {
    Update-PackageRef -csprojRelPath $csproj -packageId 'Tridion.Dxa.Framework' -version $PackageVersion
}

Write-Stage "Release complete"
Write-Host @"
Manual follow-ups:
  1. Smoke-test a clean restore of a consumer project to confirm $PackageVersion resolves from Nexus.
     dotnet restore  (in dxa-web-application-mvc-net\dotnet\src\Tridion.Dxa.Example.WebApp)
  2. If pushing a stable release to public NuGet.org, re-run without -Preview:
       .\Release-Dxa.ps1 -NuGetSource https://api.nuget.org/v3/index.json -ApiKey <nuget.org-key>
  3. Commit the csproj reference bumps:
       git -C <each-repo> add -- '*.csproj'
       git -C <each-repo> commit -m 'Release $PackageVersion'
       git -C <each-repo> tag    'v$PackageVersion'
       git -C <each-repo> push origin develop --tags
  4. Update release notes in each repo.
"@ -ForegroundColor Cyan
