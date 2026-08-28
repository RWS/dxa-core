<#
.SYNOPSIS
    Tests TCP ports from CmTestSetup.md against a CM/CD server IP, grouped by section.
    When Discovery is reachable, lists capability URLs/ports and overrides default ports if they differ.

.PARAMETER CmServerIp
    IPv4 or IPv6 address of the combined CM/CD host (not a hostname).

.PARAMETER TimeoutMs
    Connect timeout per port in milliseconds. Default 4000.

.PARAMETER DiscoveryPort
    Default Discovery TCP/HTTP port before capabilities are read. Default 8082.

.PARAMETER ClientId
    OAuth client id for Token Service (appsettings Dxa:OAuth:ClientId). Default cduser.

.PARAMETER ClientSecret
    OAuth client secret for Token Service. Default matches Example WebApp appsettings.

.PARAMETER DxaRuntimeOnly
    Test only the "Required for DXA runtime" section.

.PARAMETER PassThru
    Return $true/$false instead of calling exit. Use when invoked from another script.

.EXAMPLE
    .\test\Test-DxaRuntimePorts.ps1 -CmServerIp 192.0.2.10
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$CmServerIp,

    [int]$TimeoutMs = 4000,

    [int]$DiscoveryPort = 8082,

    [string]$ClientId = 'cduser',

    [string]$ClientSecret = 'CDUserP@ssw0rd',

    [switch]$DxaRuntimeOnly,

    [switch]$PassThru
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$parsedIp = $null
if (-not [System.Net.IPAddress]::TryParse($CmServerIp, [ref]$parsedIp)) {
    throw "CmServerIp must be an IP address. Got: '$CmServerIp'"
}

# Sections match CmTestSetup.md. Required sections affect the exit code; Optional does not.
# Capability names match Discovery OData entity sets (used to override default ports).
$sections = @(
    @{
        Name     = 'Required for DXA runtime'
        Required = $true
        Ports    = @(
            @{ Port = $DiscoveryPort; Service = 'Discovery Service (/discovery.svc)'; Capability = 'DiscoveryService' }
            @{ Port = $DiscoveryPort; Service = 'Token Service (/token.svc)'; Capability = 'TokenService' }
            @{ Port = 8081; Service = 'Content Service (/content.svc, GraphQL)'; Capability = 'ContentService' }
            @{ Port = 8083; Service = 'Session-enabled Content Service (XPM preview)'; Capability = 'PreviewWebService' }
        )
    },
    @{
        Name     = 'Required for publishing (CM on the same box)'
        Required = $true
        Ports    = @(
            @{ Port = 80;   Service = 'IIS (CME, Topology Manager if bound here, Core Service)' }
            @{ Port = 443;  Service = 'IIS HTTPS (CME, Topology Manager, Core Service)' }
            @{ Port = 81;   Service = 'Topology Manager (common alternate IIS binding)' }
            @{ Port = 8084; Service = 'Content Deployer'; Capability = 'Deployer' }
        )
    },
    @{
        Name     = 'Optional (open only if you use the feature)'
        Required = $false
        Ports    = @(
            @{ Port = 8087; Service = 'Context Engine'; Capability = 'ContextService' }
            @{ Port = 8097; Service = 'IQ Query'; Capability = 'IQQuery' }
            @{ Port = 9200; Service = 'Elasticsearch / OpenSearch' }
            @{ Port = 3389; Service = 'RDP' }
            @{ Port = 1433; Service = 'SQL Server' }
        )
    }
)

$capabilityQueries = @(
    @{ Capability = 'DiscoveryService'; Paths = @('DiscoveryServiceCapabilities') }
    @{ Capability = 'TokenService'; Paths = @('TokenServiceCapabilities') }
    @{ Capability = 'ContentService'; Paths = @('ContentServiceCapabilities') }
    @{ Capability = 'PreviewWebService'; Paths = @('PreviewWebServiceCapabilities', 'SessionEnabledContentServiceCapabilities') }
    @{ Capability = 'Deployer'; Paths = @('DeployerCapabilities', 'DeployerCapability') }
    @{ Capability = 'ContextService'; Paths = @('ContextServiceCapabilities') }
    @{ Capability = 'IQQuery'; Paths = @('IQQueryCapabilities') }
)

function Test-TcpPortOpen {
    param(
        [System.Net.IPAddress]$Address,
        [int]$Port,
        [int]$TimeoutMs
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $async = $client.BeginConnect($Address, $Port, $null, $null)
        if (-not $async.AsyncWaitHandle.WaitOne($TimeoutMs, $false)) {
            return $false
        }
        $client.EndConnect($async)
        return $client.Connected
    }
    catch {
        return $false
    }
    finally {
        $client.Close()
    }
}

function Get-HttpStatusCode {
    param($ErrorRecord)

    $ex = $ErrorRecord.Exception
    if ($ex.Response -and $ex.Response.StatusCode) {
        return [int]$ex.Response.StatusCode
    }
    if ($ex.InnerException -and $ex.InnerException.Response) {
        return [int]$ex.InnerException.Response.StatusCode
    }
    return 0
}

function Get-ODataUriProperty {
    param($Item)

    if ($null -eq $Item) {
        return $null
    }
    foreach ($name in @('Uri', 'uri', 'URL', 'url')) {
        if ($Item.PSObject.Properties.Name -contains $name) {
            $text = [string]$Item.$name
            if (-not [string]::IsNullOrWhiteSpace($text)) {
                return $text
            }
        }
    }
    return $null
}

function Invoke-DiscoveryRequest {
    param(
        [string]$Url,
        [string]$Method = 'GET',
        [string]$Token,
        [string]$Body
    )

    $headers = @{
        Accept           = 'application/json;odata.metadata=minimal'
        'OData-Version'  = '4.0'
        'OData-MaxVersion' = '4.0'
    }
    if ($Token) {
        $headers['Authorization'] = "Bearer $Token"
    }

    $params = @{
        Uri             = $Url
        Method          = $Method
        Headers         = $headers
        TimeoutSec      = [Math]::Max(1, [Math]::Ceiling($TimeoutMs / 1000.0))
        UseBasicParsing = $true
        ErrorAction     = 'Stop'
    }
    if ($Method -eq 'POST') {
        $params['ContentType'] = 'application/x-www-form-urlencoded'
        $params['Body'] = $Body
    }

    return Invoke-WebRequest @params
}

function Get-DiscoveryCapabilities {
    param(
        [string]$CmServerIp,
        [int]$DiscoveryPort,
        [string]$ClientId,
        [string]$ClientSecret
    )

    $result = @{
        Connected    = $false
        Capabilities = @()
    }

    $discoveryBase = "http://${CmServerIp}:${DiscoveryPort}/discovery.svc"
    $tokenUrl = "http://${CmServerIp}:${DiscoveryPort}/token.svc"

    try {
        $null = Invoke-DiscoveryRequest -Url $discoveryBase
        $result.Connected = $true
    }
    catch {
        $status = Get-HttpStatusCode $_
        if ($status -ge 200) {
            $result.Connected = $true
        }
        else {
            Write-Host ("Discovery HTTP call failed: {0}" -f $_.Exception.Message) -ForegroundColor DarkYellow
            return $result
        }
    }

    $token = $null
    try {
        $tokenBody = "grant_type=client_credentials&client_id=$ClientId&client_secret=$ClientSecret"
        $tokenResponse = Invoke-DiscoveryRequest -Url $tokenUrl -Method POST -Body $tokenBody
        $tokenJson = $tokenResponse.Content | ConvertFrom-Json
        $tokenProp = $tokenJson.PSObject.Properties | Where-Object { $_.Name -eq 'access_token' -or $_.Name -eq 'accessToken' } | Select-Object -First 1
        if ($tokenProp) {
            $token = [string]$tokenProp.Value
        }
    }
    catch {
        Write-Host 'Could not obtain an OAuth token from Token Service. Capability URLs may be unavailable.' -ForegroundColor DarkYellow
    }

    foreach ($query in $capabilityQueries) {
        $uriText = $null
        foreach ($path in $query.Paths) {
            $url = "$discoveryBase/$path" + '?$top=1'
            try {
                $response = Invoke-DiscoveryRequest -Url $url -Token $token
                $json = $response.Content | ConvertFrom-Json
                $items = @($json.value)
                if ($items.Count -gt 0) {
                    $uriText = Get-ODataUriProperty $items[0]
                    if ($uriText) {
                        break
                    }
                }
            }
            catch {
                $status = Get-HttpStatusCode $_
                if ($status -eq 401 -or $status -eq 403) {
                    Write-Host ("Discovery returned {0} for {1}" -f $status, $path) -ForegroundColor DarkYellow
                    break
                }
            }
        }

        if (-not $uriText) {
            continue
        }

        try {
            $uri = [Uri]$uriText
        }
        catch {
            continue
        }

        $port = $uri.Port
        if ($port -lt 0) {
            if ($uri.Scheme -eq 'https') { $port = 443 } else { $port = 80 }
        }

        $result.Capabilities += @{
            Capability = $query.Capability
            Url        = $uriText
            Port       = $port
            Host       = $uri.Host
        }
    }

    return $result
}

function Set-PortsFromDiscovery {
    param($Sections, $Capabilities)

    $byName = @{}
    foreach ($cap in $Capabilities) {
        $byName[$cap.Capability] = $cap
    }

    foreach ($section in $Sections) {
        foreach ($entry in $section.Ports) {
            if (-not $entry.ContainsKey('Capability')) {
                continue
            }
            if (-not $byName.ContainsKey($entry.Capability)) {
                continue
            }
            $cap = $byName[$entry.Capability]
            $defaultPort = $entry.Port
            if ($cap.Port -ne $defaultPort) {
                Write-Host ("  Override {0}: default TCP {1} -> {2} ({3})" -f $entry.Capability, $defaultPort, $cap.Port, $cap.Url) -ForegroundColor Cyan
                $entry.Port = $cap.Port
            }
            $entry.Service = '{0} [{1}]' -f $entry.Service, $cap.Url
        }
    }
}

if ($DxaRuntimeOnly) {
    $sections = @($sections | Where-Object { $_.Name -eq 'Required for DXA runtime' })
}

Write-Host "Testing CmTestSetup ports on $CmServerIp (timeout ${TimeoutMs}ms)" -ForegroundColor Cyan
Write-Host "DXA web app listen port 8080 is local and is not tested against the CM server." -ForegroundColor DarkGray
Write-Host ""

$discoveryTcpOpen = Test-TcpPortOpen -Address $parsedIp -Port $DiscoveryPort -TimeoutMs $TimeoutMs
if ($discoveryTcpOpen) {
    Write-Host ("Discovery TCP {0} is open. Reading capability URLs..." -f $DiscoveryPort) -ForegroundColor Cyan
    $discovered = Get-DiscoveryCapabilities -CmServerIp $CmServerIp -DiscoveryPort $DiscoveryPort -ClientId $ClientId -ClientSecret $ClientSecret
    if ($discovered.Connected -and $discovered.Capabilities.Count -gt 0) {
        Write-Host ""
        Write-Host 'Discovery configured URLs and ports:' -ForegroundColor Yellow
        foreach ($cap in $discovered.Capabilities) {
            $hostNote = ''
            if ($cap.Host -eq 'localhost' -or $cap.Host -eq '127.0.0.1') {
                $hostNote = '  (localhost - DXA on another machine cannot use this host)'
            }
            Write-Host ("  {0,-20} {1,-6} {2}{3}" -f $cap.Capability, $cap.Port, $cap.Url, $hostNote)
        }
        Write-Host ""
        Write-Host 'Applying Discovery ports over CmTestSetup defaults where they differ...' -ForegroundColor Cyan
        Set-PortsFromDiscovery -Sections $sections -Capabilities $discovered.Capabilities
        Write-Host ""
    }
    elseif ($discovered.Connected) {
        Write-Host 'Discovery responded but returned no capability URLs. Using CmTestSetup default ports.' -ForegroundColor DarkYellow
        Write-Host ""
    }
    else {
        Write-Host 'Discovery TCP is open but the HTTP service was not usable. Using CmTestSetup default ports.' -ForegroundColor DarkYellow
        Write-Host ""
    }
}
else {
    Write-Host ("Discovery TCP {0} is closed. Using CmTestSetup default ports." -f $DiscoveryPort) -ForegroundColor DarkYellow
    Write-Host ""
}

$requiredFailed = 0
$optionalClosed = 0

foreach ($section in $sections) {
    Write-Host $section.Name -ForegroundColor Yellow
    foreach ($entry in $section.Ports) {
        $open = Test-TcpPortOpen -Address $parsedIp -Port $entry.Port -TimeoutMs $TimeoutMs
        if ($open) {
            Write-Host ("  OPEN   {0,-5}  {1}" -f $entry.Port, $entry.Service) -ForegroundColor Green
        }
        else {
            Write-Host ("  CLOSED {0,-5}  {1}" -f $entry.Port, $entry.Service) -ForegroundColor Red
            if ($section.Required) {
                $requiredFailed++
            }
            else {
                $optionalClosed++
            }
        }
    }
    Write-Host ""
}

$success = $requiredFailed -eq 0
if ($success) {
    Write-Host 'All required ports are reachable from this machine.' -ForegroundColor Green
    if ($optionalClosed -gt 0) {
        Write-Host ("{0} optional port(s) are closed (expected if those features are unused)." -f $optionalClosed) -ForegroundColor DarkYellow
    }
}
else {
    Write-Host ("{0} required port(s) are not reachable. Check AWS security group DXA-CmTest and Windows Firewall." -f $requiredFailed) -ForegroundColor Red
    if ($optionalClosed -gt 0) {
        Write-Host ("{0} optional port(s) are also closed." -f $optionalClosed) -ForegroundColor DarkYellow
    }
}

if ($PassThru) {
    return $success
}

exit $(if ($success) { 0 } else { 1 })
