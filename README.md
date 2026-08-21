# dxa-core
RWS Digital Experience Accelerator .NET Core MVC web application
===
Build status
------------
- Develop: [![Build](https://github.com/RWS/dxa-core/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/RWS/dxa-core/actions/workflows/build.yml?query=branch%3Adevelop)

Prerequisites
-------------
For building .NET 8 and .NET 10 repositories you must have the following installed:
- Visual Studio 2022 or higher
- .NET 8 and .NET 10 SDKs

Build (single component)
------------------------
Each component has its own `build.proj` under `<component>/dotnet/build/`. To build a single component locally:

```
cd <component>/dotnet/build
msbuild build.proj /t:Restore
msbuild build.proj /t:Build /p:BuildConfiguration=Release
```

Release (all components, dependency-ordered)
--------------------------------------------
The `Release-Dxa.ps1` script at the repo root drives a full multi-package release in the correct order:

1. **Tridion.Dxa.Framework.DataModel** (from `dxa-framework-datamodel`)
2. **Tridion.Dxa.Api.Client** (from `dxa-pca-client-net`)
3. **Tridion.Dxa.Framework** (from `dxa-framework-mvc-net`) — package refs for #1 and #2 are auto-bumped before this builds.
4. **Tridion.Dxa.Module.Core**, **Tridion.Dxa.Module.Search**, **Tridion.Dxa.Module.DynamicDocumentation** — Framework package refs are auto-bumped before each module builds.
5. Bumps the `Tridion.Dxa.Framework` `<PackageReference>` in the Example WebApp (`dxa-web-application-mvc-net`).

Each package step:
- `msbuild build.proj /t:Build` (uses the existing per-component build target)
- `msbuild build.proj /t:SignAssemblies` (skip with `-SkipSign` on machines without a code-signing cert)
- `dotnet pack` with `/p:VersionSuffix=""` to produce a clean stable `2.4.1.nupkg` (the default `SignPackAndPushNuGetPackages` target stamps a `beta-{timestamp}` suffix; we bypass it)
- `dotnet nuget push` to the target feed (skip with `-SkipPush`)
- Copies the produced `.nupkg` into the shared `LocalNugetStorage/` at the repo root, and the next component's restore picks it up via `/p:RestoreAdditionalProjectSources` (no Nexus propagation delay)

### Usage

```powershell
# Dry-run: prints every command, executes nothing.
.\Release-Dxa.ps1 -DryRun

# Local smoke test: builds & packs all components, doesn't push to any feed.
.\Release-Dxa.ps1 -SkipPush

# Full stable release of 2.4.1 to internal Nexus.
.\Release-Dxa.ps1

# Preview release to internal Nexus only (e.g. 2.4.1-preview-20260821122600).
.\Release-Dxa.ps1 -Preview -NonInteractive

# After Nexus has been verified, re-publish a stable build to public NuGet.org.
.\Release-Dxa.ps1 -NuGetSource https://api.nuget.org/v3/index.json -ApiKey <nuget-org-key>
```

### Parameters

| Parameter | Default | Purpose |
|---|---|---|
| `-Version` | `2.4.1` | Version prefix (`VersionPrefix`). Stable packs with empty suffix; `-Preview` appends `preview-{timestamp}`. |
| `-Preview` | `false` | Pack/push `{Version}-preview-{yyyyMMddHHmmss}` to Nexus only (refuses nuget.org). |
| `-NuGetSource` | Internal Nexus URL | Target feed for `dotnet nuget push`. |
| `-ApiKey` | `(from build.proj)` | API key for push. |
| `-SkipSign` | `false` | Skip `SignAssemblies` target. |
| `-SkipPush` | `false` | Build & pack only; do not push. |
| `-DryRun` | `false` | Print commands without executing. |
| `-NonInteractive` | `false` | Skip per-stage confirmation prompts (used by CI). |

### After a successful release

1. Smoke-test a clean restore in a downstream consumer (e.g. `dxa-web-application-mvc-net`) to confirm `2.4.1` resolves from the target feed.
2. Commit the csproj reference bumps the script made:
   ```
   git add -- '**/*.csproj'
   git commit -m 'Release 2.4.1'
   git tag v2.4.1
   git push origin develop --tags
   ```
3. Update release notes.

Docker (Example WebApp)
-----------------------
The Example WebApp multi-targets `net8.0` and `net10.0`. Build **one image per TFM** from the repo root (after packing packages into `LocalNugetStorage/` via `.\Release-Dxa.ps1 -SkipPush -SkipSign -NonInteractive`):

```powershell
# .NET 8
docker build -f dxa-web-application-mvc-net/dotnet/src/Tridion.Dxa.Example.WebApp/Dockerfile -t dxa-example-webapp:net8 .

# .NET 10
docker build -f dxa-web-application-mvc-net/dotnet/src/Tridion.Dxa.Example.WebApp/Dockerfile.net10.0 -t dxa-example-webapp:net10 .
```

ARM runtime images (publish output copied in separately):

- `ARM.Dockerfile` — ASP.NET 8.0 Alpine arm64v8
- `ARM.Dockerfile.net10.0` — ASP.NET 10.0 Alpine arm64v8

Folder publish profiles:

- `Properties/PublishProfiles/FolderProfile.pubxml` → `net8.0`
- `Properties/PublishProfiles/FolderProfile.net10.0.pubxml` → `net10.0`

Migrating from .NET 8 to .NET 10
-------------------------------

### For consumers using published NuGet packages

Use this path if your site references `Tridion.Dxa.*` from Nexus or NuGet.org and you do not build DXA from this repository.

**Prerequisites**
- Install the [.NET 10 SDK](https://dotnet.microsoft.com/download) for builds; install the ASP.NET Core 10 runtime on deploy hosts.
- Use DXA packages **2.4.1 or later**. Those packages multi-target `net8.0` and `net10.0` (`lib/net8.0` and `lib/net10.0`). Packages built only for `net8.0` will not provide a net10 asset.

**Steps**
1. In your web application `.csproj`, change the target framework to .NET 10:
   ```xml
   <TargetFramework>net10.0</TargetFramework>
   ```
   (Or multi-target `net8.0;net10.0` if you must support both during a transition.)
2. Update all `Tridion.Dxa.*` package references to **2.4.1+**, for example:
   ```xml
   <PackageReference Include="Tridion.Dxa.Framework" Version="2.4.1" />
   <PackageReference Include="Tridion.Dxa.Module.Core" Version="2.4.1" />
   ```
   Repeat for any other DXA modules you use (`Module.Search`, `Module.DynamicDocumentation`, etc.). Prefer:
   ```powershell
   dotnet add package Tridion.Dxa.Framework --version 2.4.1
   ```
3. Bump Microsoft ASP.NET Core / Extensions packages your app references to **10.0.x** (match the shared framework). Do not keep AspNetCore **8.0.x** packages on a `net10.0` app.
4. Restore, build, and publish for net10:
   ```powershell
   dotnet restore
   dotnet build -c Release -f net10.0
   dotnet publish -c Release -f net10.0 -o ./publish
   ```
5. Deploy onto hosts or containers that run the **ASP.NET Core 10** runtime (not the .NET 8 runtime image).

NuGet resolves `lib/net10.0` from the DXA packages automatically when your project targets `net10.0`—no extra package IDs or TFM suffixes are required.

**Smoke checklist**
- Restore succeeds against your feed (Nexus / NuGet.org)
- App builds and starts on ASP.NET Core 10
- Pages render; DXA modules initialize as before

**Known warnings (non-blocking)**  
Building against net10 may surface ASP.NET deprecation warnings (for example `ASPDEPR003` Razor runtime compilation, `ASPDEPR005` `KnownNetworks`, `ASPDEPR006` `IActionContextAccessor`). They do not fail the build unless warnings-as-errors is enabled.

### Building or deploying this repository

For people working in `dxa-core` itself (not required for NuGet-only consumers):

- Folder publish: `-f net10.0` or `FolderProfile.net10.0.pubxml`
- x64 Docker: `Dockerfile.net10.0`
- ARM: `ARM.Dockerfile.net10.0`
- Shared Microsoft package bands for net10 live in the root `Directory.Build.props`

About
-----
The RWS Digital Experience Accelerator (DXA) is a reference implementation of RWS Tridion Sites 10+ intended to help you create, design and publish an RWS Tridion/Web-based website quickly.

DXA .NET Core is available only for .NET web applications. Its modular architecture consists of a framework and example web application, which includes all core RWS Tridion/Web functionality as well as separate Modules for additional, optional functionality.

This repository contains the source code of the DXA Framework and an example .NET web application. 

The full DXA distribution (including Content Manager-side items and installation support) is downloadable from the [RWS AppStore](https://appstore.rws.com/?q=dxa) 
or the [Releases in GitHub](https://github.com/rws/dxa-core/releases).
Furthermore, the DXA Framework is available on [NuGet.org](https://www.nuget.org/packages?q=dxa). 

To facilitate upgrades, we strongly recommend that you use official, compiled DXA artifacts from Maven Central instead of a custom build.
If you really must modify the DXA framework, we kindly request that you submit your changes as a Contribution (see the Branches and Contributions section below). 

Support
-------
At RWS we take your investment in Digital Experience very seriously, if you encounter any issues with the Digital Experience Accelerator, please use one of the following channels:

- Report issues directly in [this repository](https://github.com/rws/dxa-core/issues)
- Ask questions 24/7 on the RWS Tridion Community at https://tridion.stackexchange.com
- Contact RWS Professional Services for DXA release management support packages to accelerate your support requirements

Documentation
-------------
Documentation can be found online in the RWS documentation portal: https://docs.rws.com/sdldxa


Repositories
------------
You can find all the DXA related repositories [here](https://github.com/rws/?q=dxa&type=source&language=)

Branches and Contributions
--------------------------
We are using the following branching strategy:

 - `develop` - Represents the latest development version.
 - `release/x.y` - Represents the x.y Release. If hotfixes are applicable, they will be applied to the appropriate release branch so that the branch actually represents the initial release plus hotfixes.

All releases (including pre-releases and hotfix releases) are tagged. 

If you wish to submit a Pull Request, it should normally be submitted on the `develop` branch so that it can be incorporated in the upcoming release.

Fixes for severe/urgent issues (that qualify as hotfixes) should be submitted as Pull Requests on the appropriate release branch.

Always submit an issue for the problem, and indicate whether you think it qualifies as a hotfix. Pull Requests on release branches will only be accepted after agreement on the severity of the issue.
Furthermore, Pull Requests on release branches are expected to be extensively tested by the submitter.

Of course, it is also possible (and appreciated) to report an issue without associated Pull Requests.


License
-------
Copyright (c) 2014-2025 RWS Group.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and limitations under the License.
