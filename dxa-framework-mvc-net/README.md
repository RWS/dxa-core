DXA ASP.NET Core MVC Framework
==============================

Prerequisites
-------------
For building .NET repositories you must have the following installed:
- Visual Studio 2022
- .NET Framework .Net Core 8.0

Build
-----
```
msbuild build.proj /t:Restore
msbuild build.proj

msbuild build.proj /t:SignPackAndPushNuGetPackages /p:BuildConfiguration=Release
dotnet add package Tridion.Dxa.Framework --version 1.0.0-beta1 --source C:\LocalNuGetPackages
.\NuGet.exe setapikey fed9a610-8898-3986-877e-1001ba1f858d -source https://nexus.sdl.com/repository/releases_dotnet/
dotnet add package Tridion.Dxa.Api.Client --version 3.0.0-beta-20250402231954 --source https://nexus.sdl.com/service/local/nuget/releases_dotnet/
dotnet add package Tridion.Dxa.Framework.DataModel --version 3.0.0-beta-20250402224149 --source https://nexus.sdl.com/service/local/nuget/releases_dotnet/
```