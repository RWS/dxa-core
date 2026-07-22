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
4. Bumps the `Tridion.Dxa.Framework` `<PackageReference>` in each consumer csproj: `dxa-module-core-net`, `dxa-module-dynamicdocumentation-net`, `dxa-module-search-net`, `dxa-web-application-mvc-net`.

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

# Full release to internal Nexus.
.\Release-Dxa.ps1

# After Nexus has been verified, re-publish to public NuGet.org.
.\Release-Dxa.ps1 -NuGetSource https://api.nuget.org/v3/index.json -ApiKey <nuget-org-key>
```

### Parameters

| Parameter | Default | Purpose |
|---|---|---|
| `-Version` | `2.4.1` | Stable version. Becomes `VersionPrefix`; `VersionSuffix` is forced empty. |
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
