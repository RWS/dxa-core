# Builds the public content api from a graphQL endpoint
Param (
    # endpoint
    [Parameter(Mandatory=$true, HelpMessage="url of graphQL endpoint")]
    [string]$url
)

if(!$url.StartsWith("http://")) {
    $url = "http://" + $url
}

if(!$url.EndsWith("/cd/api")) {
    $url = $url + "/cd/api";
}

Write-Host "using endpoint $url to build content api..."


# build the code generator first
& dotnet build ..\src\Tridion.CodeGen --configuration Release

# remove existing generated api
Remove-Item ..\src\Tridion.Dxa.Api.Client\Api\* -Force

# run code generator against endpoint
& dotnet ..\src\Tridion.CodeGen\bin\Release\netcoreapp2.1\Tridion.CodeGen.dll $url -namespace "Tridion.Api" -types -builders -outdir "..\src\Tridion.Dxa.Api.Client\Api"

# rebuild client
& dotnet build ..\src\Tridion.Dxa.Api.Client --configuration Debug
& dotnet build ..\src\Tridion.Dxa.Api.Client --configuration Release