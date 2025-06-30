#!/bin/sh

if [[ -z "${Logging__LogLevel__Default}" ]]; then
	export Logging__LogLevel__Default="Warning"    
fi

echo "ASP.NET log level: $Logging__LogLevel__Default"

export URLs="http://*:${serviceport:-80}"
echo "URLs: ${URLs}"

dotnet Tridion.Dxa.Example.WebApp.dll