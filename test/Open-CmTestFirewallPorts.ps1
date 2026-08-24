<#
.SYNOPSIS
    Opens inbound TCP ports in Windows Defender Firewall for CmTestSetup.md sections.

.PARAMETER Section
    One or more unique section IDs: DxaRuntime, Publishing, Optional, WebApp.

.EXAMPLE
    .\test\Open-CmTestFirewallPorts.ps1 -Section DxaRuntime

.EXAMPLE
    .\test\Open-CmTestFirewallPorts.ps1 -Section DxaRuntime,Publishing
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('DxaRuntime', 'Publishing', 'Optional', 'WebApp')]
    [string[]]$Section
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    throw "This script must be run in an elevated PowerShell session (Run as administrator)."
}

# Sections match CmTestSetup.md (same ports as Test-DxaRuntimePorts.ps1).
$allSections = @(
    @{
        Id    = 'DxaRuntime'
        Name  = 'Required for DXA runtime'
        Ports = @(
            @{ Port = 8082; Service = 'Discovery Service (/discovery.svc) and Token Service (/token.svc)' }
            @{ Port = 8081; Service = 'Content Service (/content.svc, GraphQL)' }
            @{ Port = 8083; Service = 'Session-enabled Content Service (XPM preview)' }
        )
    },
    @{
        Id    = 'Publishing'
        Name  = 'Required for publishing (CM on the same box)'
        Ports = @(
            @{ Port = 80;   Service = 'IIS (CME, Topology Manager if bound here, Core Service)' }
            @{ Port = 443;  Service = 'IIS HTTPS (CME, Topology Manager, Core Service)' }
            @{ Port = 81;   Service = 'Topology Manager (common alternate IIS binding)' }
            @{ Port = 8084; Service = 'Content Deployer' }
        )
    },
    @{
        Id    = 'Optional'
        Name  = 'Optional (open only if you use the feature)'
        Ports = @(
            @{ Port = 8087; Service = 'Context Engine' }
            @{ Port = 8097; Service = 'IQ Query' }
            @{ Port = 9200; Service = 'Elasticsearch / OpenSearch' }
            @{ Port = 3389; Service = 'RDP' }
            @{ Port = 1433; Service = 'SQL Server' }
        )
    },
    @{
        Id    = 'WebApp'
        Name  = 'DXA web app listen port (local, not AWS)'
        Ports = @(
            @{ Port = 8080; Service = 'DXA Example WebApp (appsettings URLs)' }
        )
    }
)

Import-Module NetSecurity

$selected = $allSections | Where-Object { $Section -contains $_.Id }
$created = 0
$skipped = 0

foreach ($sec in $selected) {
    Write-Host ("{0} - {1}" -f $sec.Id, $sec.Name) -ForegroundColor Yellow
    foreach ($entry in $sec.Ports) {
        $displayName = "DXA CmTest - $($sec.Id) - TCP $($entry.Port)"
        $existing = Get-NetFirewallRule -DisplayName $displayName -ErrorAction SilentlyContinue
        if ($existing) {
            Write-Host ("  EXISTS {0,-5}  {1}" -f $entry.Port, $entry.Service) -ForegroundColor DarkGray
            $skipped++
            continue
        }

        New-NetFirewallRule `
            -DisplayName $displayName `
            -Description $entry.Service `
            -Direction Inbound `
            -Action Allow `
            -Protocol TCP `
            -LocalPort $entry.Port `
            -Profile Any | Out-Null

        Write-Host ("  OPENED {0,-5}  {1}" -f $entry.Port, $entry.Service) -ForegroundColor Green
        $created++
    }
    Write-Host ""
}

Write-Host ("Created {0} inbound rule(s); {1} already present." -f $created, $skipped) -ForegroundColor Cyan
Write-Host 'This only updates Windows Defender Firewall on this machine. Also open the same ports on the AWS security group.'
