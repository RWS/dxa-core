<#
.SYNOPSIS
    Starts the CM test DXA Docker image and checks that the site is up and can talk to CD.

.PARAMETER Framework
    Image TFM: net8 (dxa-example-webapp:net8-cmtest) or net10 (dxa-example-webapp:net10-cmtest).

.PARAMETER CmServerIp
    CM/CD IP used for Docker --add-host entries. Default: parsed from the image Dxa__Services__Discovery ENV.

.PARAMETER WebsiteUrl
    Topology website Base URL used for Host/Origin on page requests. Default http://dxa.tridiondemo.com (port 80). Not the CD host dxd.tridiondemo.com and not Docker port 8080.

.PARAMETER HostPort
    Host port published to container 8080. Default is the Topology website port (80 for http://dxa.tridiondemo.com). Must match Topology or the browser will fail localization.

.PARAMETER StartupTimeoutSec
    Seconds to wait for /system/health. Default 90.

.PARAMETER RemoveWhenDone
    Also remove the container after a successful run. On failure the container is always removed.

.EXAMPLE
    .\test\Start-CmTestWebApp.ps1 -Framework net8

.EXAMPLE
    .\test\Start-CmTestWebApp.ps1 -Framework net10 -WebsiteUrl http://dxa.tridiondemo.com
#>
[CmdletBinding()]
param(
    [ValidateSet('net8', 'net10')]
    [string]$Framework,

    [string]$CmServerIp,

    [string]$WebsiteUrl = 'http://dxa.tridiondemo.com',

    [int]$HostPort,

    [int]$StartupTimeoutSec = 90,

    [switch]$RemoveWhenDone
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$frameworkValues = @('net8', 'net10')
if ([string]::IsNullOrWhiteSpace($Framework)) {
    Write-Host ("Available Framework values: {0}" -f ($frameworkValues -join ', ')) -ForegroundColor Cyan
    $Framework = Read-Host 'Framework'
}
if ($frameworkValues -notcontains $Framework) {
    throw ("Framework must be one of: {0}. Got: '{1}'" -f ($frameworkValues -join ', '), $Framework)
}

if ($Framework -eq 'net8') {
    $imageTag = 'dxa-example-webapp:net8-cmtest'
}
else {
    $imageTag = 'dxa-example-webapp:net10-cmtest'
}

$containerName = 'dxa-cmtest'

$websiteUri = $null
$browseUrl = $null
if (-not [string]::IsNullOrWhiteSpace($WebsiteUrl)) {
    try {
        $websiteUri = [Uri]$WebsiteUrl
    }
    catch {
        throw "WebsiteUrl is not a valid URL: '$WebsiteUrl'"
    }
    if (-not $websiteUri.IsAbsoluteUri) {
        throw "WebsiteUrl must be an absolute URL. Got: '$WebsiteUrl'"
    }
}

if (-not $PSBoundParameters.ContainsKey('HostPort')) {
    if ($null -ne $websiteUri -and $websiteUri.Port -gt 0) {
        $HostPort = $websiteUri.Port
    }
    else {
        $HostPort = 80
    }
}

$baseUrl = "http://127.0.0.1:$HostPort"
$browseUrl = $baseUrl
if ($null -ne $websiteUri) {
    $browseBuilder = New-Object System.UriBuilder($websiteUri)
    $browseBuilder.Path = '/'
    $browseBuilder.Query = ''
    $browseUrl = $browseBuilder.Uri.AbsoluteUri
}

function Get-ImageDiscoveryUri {
    param([string]$ImageTag)

    $inspectJson = & docker inspect $ImageTag
    if ($LASTEXITCODE -ne 0) {
        return $null
    }
    $inspect = $inspectJson | ConvertFrom-Json
    foreach ($entry in @($inspect[0].Config.Env)) {
        if ($entry -like 'Dxa__Services__Discovery=*') {
            $raw = $entry.Substring('Dxa__Services__Discovery='.Length)
            try {
                return [Uri]$raw
            }
            catch {
                return $null
            }
        }
    }
    return $null
}

function Get-DiscoveryCapabilityInfo {
    param(
        [string]$DiscoveryBaseUrl,
        [string]$TokenUrl,
        [string]$ClientId = 'cduser',
        [string]$ClientSecret = 'CDUserP@ssw0rd'
    )

    $info = @{
        Hosts              = @()
        Token              = $null
        ContentServiceUrl  = $null
    }

    $capabilityHostSet = New-Object System.Collections.Generic.HashSet[string]
    try {
        $tokenResponse = Invoke-WebRequest -Uri $TokenUrl -Method POST -UseBasicParsing -TimeoutSec 15 `
            -ContentType 'application/x-www-form-urlencoded' `
            -Body "grant_type=client_credentials&client_id=$ClientId&client_secret=$ClientSecret"
        $tokenJson = $tokenResponse.Content | ConvertFrom-Json
        $tokenProp = $tokenJson.PSObject.Properties | Where-Object { $_.Name -eq 'access_token' -or $_.Name -eq 'accessToken' } | Select-Object -First 1
        if ($tokenProp) {
            $info.Token = [string]$tokenProp.Value
        }
    }
    catch {
        Write-Host 'Could not read Token Service; Docker extra hosts may be incomplete.' -ForegroundColor DarkYellow
    }

    $headers = @{
        Accept             = 'application/json;odata.metadata=minimal'
        'OData-Version'    = '4.0'
        'OData-MaxVersion' = '4.0'
    }
    if ($info.Token) {
        $headers['Authorization'] = "Bearer $($info.Token)"
    }

    $paths = @(
        'TokenServiceCapabilities',
        'ContentServiceCapabilities',
        'PreviewWebServiceCapabilities',
        'DiscoveryServiceCapabilities',
        'DeployerCapabilities',
        'IQQueryCapabilities',
        'ContextServiceCapabilities'
    )

    foreach ($path in $paths) {
        $url = "$DiscoveryBaseUrl/$path" + '?$top=1'
        try {
            $response = Invoke-WebRequest -Uri $url -Headers $headers -UseBasicParsing -TimeoutSec 15
            $json = $response.Content | ConvertFrom-Json
            $items = @($json.value)
            if ($items.Count -eq 0) {
                continue
            }
            $item = $items[0]
            $uriText = $null
            foreach ($name in @('Uri', 'uri', 'URL', 'url')) {
                if ($item.PSObject.Properties.Name -contains $name) {
                    $uriText = [string]$item.$name
                    break
                }
            }
            if ($uriText) {
                $uri = [Uri]$uriText
                [void]$capabilityHostSet.Add($uri.Host)
                if ($path -eq 'ContentServiceCapabilities') {
                    $info.ContentServiceUrl = $uriText
                }
            }
        }
        catch {
        }
    }

    $info.Hosts = @($capabilityHostSet)
    return $info
}

function Get-PublicationMapping {
    param(
        [string]$ContentServiceUrl,
        [string]$CmServerIp,
        [string]$Token,
        [string[]]$CandidateHosts
    )

    if (-not $ContentServiceUrl -or -not $Token) {
        return $null
    }

    try {
        $contentUri = [Uri]$ContentServiceUrl
    }
    catch {
        return $null
    }

    $builder = New-Object System.UriBuilder($contentUri)
    $builder.Host = $CmServerIp
    $graphQlUrl = $builder.Uri.AbsoluteUri.Replace('content.svc', 'cd/api')

    $siteUrls = New-Object System.Collections.Generic.List[string]
    foreach ($hostName in $CandidateHosts) {
        if ([string]::IsNullOrWhiteSpace($hostName)) {
            continue
        }
        [void]$siteUrls.Add("http://${hostName}/")
        [void]$siteUrls.Add("https://${hostName}/")
        [void]$siteUrls.Add("http://${hostName}:80/")
        [void]$siteUrls.Add("https://${hostName}:443/")
    }

    $headers = @{
        Authorization = "Bearer $Token"
        Accept        = 'application/json'
    }

    foreach ($siteUrl in $siteUrls) {
        $payload = @{
            query     = 'query($namespaceId: Int!, $siteUrl: String!) { publicationMapping(namespaceId: $namespaceId, siteUrl: $siteUrl) { publicationId protocol domain port path } }'
            variables = @{
                namespaceId = 1
                siteUrl     = $siteUrl
            }
        }
        $jsonBody = $payload | ConvertTo-Json -Compress -Depth 6
        try {
            $response = Invoke-WebRequest -Uri $graphQlUrl -Method POST -Headers $headers `
                -ContentType 'application/json; charset=utf-8' -Body $jsonBody -UseBasicParsing -TimeoutSec 20
            $data = $response.Content | ConvertFrom-Json
            $mapping = $null
            if ($data.PSObject.Properties.Name -contains 'data' -and $data.data -and $data.data.publicationMapping) {
                $mapping = $data.data.publicationMapping
            }
            if ($mapping -and $mapping.domain) {
                Write-Host ("Topology publication mapping: {0}://{1}:{2}{3} (probed {4})" -f $mapping.protocol, $mapping.domain, $mapping.port, $mapping.path, $siteUrl) -ForegroundColor Cyan
                return $mapping
            }
        }
        catch {
        }
    }

    Write-Host 'Could not read a Topology publication mapping from Content Service GraphQL.' -ForegroundColor DarkYellow
    return $null
}

function Get-HeadersFromWebsiteUrl {
    param([Uri]$WebsiteUri)

    $hostHeader = $WebsiteUri.Host
    $defaultPort = 80
    if ($WebsiteUri.Scheme -eq 'https') {
        $defaultPort = 443
    }
    if ($WebsiteUri.IsDefaultPort -eq $false -and $WebsiteUri.Port -gt 0 -and $WebsiteUri.Port -ne $defaultPort) {
        $hostHeader = '{0}:{1}' -f $WebsiteUri.Host, $WebsiteUri.Port
    }
    return @{
        HostHeader   = $hostHeader
        OriginHeader = $WebsiteUri.GetLeftPart([System.UriPartial]::Authority)
    }
}

function Test-PageLocalizationOk {
    param($HttpResult)

    if ($null -eq $HttpResult) {
        return $false
    }
    $searchTied = Test-IsSearchModuleError -Html $HttpResult.Content
    $statusOk = $HttpResult.StatusCode -eq 200 -or ($searchTied -and $HttpResult.StatusCode -ge 400)
    $looksHtml = $statusOk -and (
        $HttpResult.MediaType -match 'html' -or $HttpResult.Content -match '(?i)<html'
    )
    $noLocalization = $HttpResult.Content -match '(?i)No matching Localization'
    return ($looksHtml -and -not $noLocalization)
}

function Test-HomePageSuccess {
    param($HttpResult)

    if (-not (Test-PageLocalizationOk -HttpResult $HttpResult)) {
        return $false
    }
    return (-not (Test-HasFatalSectionRenderError -Html $HttpResult.Content))
}

function Test-HtmlHasSectionRenderError {
    param([string]$Html)

    if ([string]::IsNullOrWhiteSpace($Html)) {
        return $false
    }
    return ($Html -match '(?i)A problem occurred while rendering this section')
}

function Test-IsSearchModuleError {
    param([string]$Html)

    if ([string]::IsNullOrWhiteSpace($Html)) {
        return $false
    }
    return ($Html -match '(?i)Search(?::|&#58;)Entity(?::|&#58;)SearchBox' -or $Html -match "(?i)View in the 'Search' area")
}

function Get-SectionRenderErrorDetails {
    param([string]$Html)

    $details = New-Object System.Collections.Generic.List[string]
    if (-not (Test-HtmlHasSectionRenderError -Html $Html)) {
        return $details
    }
    $preMatches = [regex]::Matches($Html, '(?is)<pre[^>]*>(.*?)</pre>')
    foreach ($pre in $preMatches) {
        $text = $pre.Groups[1].Value
        $text = [System.Net.WebUtility]::HtmlDecode($text)
        $text = ($text -replace '\s+', ' ').Trim()
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            [void]$details.Add($text)
        }
    }
    return $details
}

function Test-HasFatalSectionRenderError {
    param(
        [string]$Html,
        [switch]$BareSectionErrorIsSearchBox
    )

    if (-not (Test-HtmlHasSectionRenderError -Html $Html)) {
        return $false
    }
    $details = Get-SectionRenderErrorDetails -Html $Html
    if (Test-IsSearchModuleError -Html $Html) {
        if ($null -eq $details -or $details.Count -eq 0) {
            return $false
        }
        foreach ($detail in $details) {
            $tiedToSearchBox = $detail -match '(?i)Search(?::|&#58;)Entity(?::|&#58;)SearchBox' -or $detail -match '(?i)SearchBox' -or $detail -match "(?i)View in the 'Search' area"
            if (-not $tiedToSearchBox) {
                return $true
            }
        }
        return $false
    }
    if ($BareSectionErrorIsSearchBox -and ($null -eq $details -or $details.Count -eq 0)) {
        return $false
    }
    return $true
}

function Test-ContainerSearchBoxErrorsOnly {
    param([string]$ContainerName)

    if ([string]::IsNullOrWhiteSpace($ContainerName)) {
        return $false
    }
    $raw = ''
    try {
        $raw = (& docker logs --tail 200 $ContainerName 2>&1 | Out-String)
    }
    catch {
        return $false
    }
    if ($raw -notmatch 'Search:Entity:SearchBox') {
        return $false
    }
    $errorLines = [regex]::Matches($raw, '(?m)^.*\|Error\|.*$')
    foreach ($match in $errorLines) {
        $line = $match.Value
        if ($line -notmatch 'Search:Entity:SearchBox' -and $line -notmatch "(?i)Search' area") {
            return $false
        }
    }
    return $true
}

function Write-SectionRenderErrorLog {
    param(
        [string]$PageUrl,
        [string]$Html,
        [string]$ContainerName
    )

    Write-Host ("  Section render error on {0}" -f $PageUrl) -ForegroundColor Red
    $details = Get-SectionRenderErrorDetails -Html $Html
    if ($null -ne $details -and $details.Count -gt 0) {
        Write-Host '  Page error details:' -ForegroundColor DarkYellow
        foreach ($detail in $details) {
            Write-Host ("    {0}" -f $detail) -ForegroundColor DarkYellow
        }
    }
    else {
        Write-Host '  No ExceptionEntity <pre> details in HTML (shown only when the site runs in Development).' -ForegroundColor DarkYellow
    }
    if (-not [string]::IsNullOrWhiteSpace($ContainerName)) {
        Write-Host '  Container error logs:' -ForegroundColor DarkYellow
        try {
            & docker logs --tail 80 $ContainerName 2>&1 | Where-Object { $_ -match '(?i)(\|Error\||\|Warn\||Exception)' }
        }
        catch {
        }
    }
}

function Get-HrefUrlsFromHtml {
    param([string]$Html)

    $urls = New-Object System.Collections.Generic.List[string]
    if ([string]::IsNullOrWhiteSpace($Html)) {
        return @()
    }
    $hrefMatches = [regex]::Matches($Html, 'href\s*=\s*["'']([^"'']+)["'']', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($match in $hrefMatches) {
        $href = $match.Groups[1].Value.Trim()
        if (-not [string]::IsNullOrWhiteSpace($href)) {
            [void]$urls.Add($href)
        }
    }
    return @($urls)
}

function Get-LocUrlsFromSitemapXml {
    param([string]$XmlText)

    $urls = New-Object System.Collections.Generic.List[string]
    if ([string]::IsNullOrWhiteSpace($XmlText) -or $XmlText -notmatch '<urlset') {
        return @()
    }
    try {
        $xml = New-Object System.Xml.XmlDocument
        $xml.XmlResolver = $null
        $xml.LoadXml($XmlText)
        $nsmgr = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $nsmgr.AddNamespace('sm', 'http://www.sitemaps.org/schemas/sitemap/0.9')
        $nodes = $xml.SelectNodes('//sm:loc', $nsmgr)
        if ($null -eq $nodes -or $nodes.Count -eq 0) {
            $nodes = $xml.SelectNodes('//loc')
        }
        if ($null -ne $nodes) {
            foreach ($node in $nodes) {
                $text = $node.InnerText.Trim()
                if (-not [string]::IsNullOrWhiteSpace($text)) {
                    [void]$urls.Add($text)
                }
            }
        }
    }
    catch {
    }
    return @($urls)
}

function Convert-ToLocalTestUrl {
    param(
        [string]$Loc,
        [string]$LocalBaseUrl,
        [string]$WebsiteHost
    )

    if ([string]::IsNullOrWhiteSpace($Loc)) {
        return $null
    }
    $trimmed = $Loc.Trim()
    if ($trimmed.StartsWith('#') -or $trimmed -match '^(?i)(mailto:|javascript:|tel:)') {
        return $null
    }
    if ($trimmed.StartsWith('/')) {
        return $LocalBaseUrl.TrimEnd('/') + $trimmed
    }
    try {
        $uri = [Uri]$trimmed
    }
    catch {
        return $LocalBaseUrl.TrimEnd('/') + '/' + $trimmed.TrimStart('/')
    }
    if (-not $uri.IsAbsoluteUri) {
        return $LocalBaseUrl.TrimEnd('/') + '/' + $trimmed.TrimStart('/')
    }
    $sameHost = $uri.Host -eq '127.0.0.1' -or $uri.Host -eq 'localhost'
    if (-not $sameHost -and -not [string]::IsNullOrWhiteSpace($WebsiteHost) -and $uri.Host -eq $WebsiteHost) {
        $sameHost = $true
    }
    if (-not $sameHost) {
        return $null
    }
    $path = $uri.PathAndQuery
    if ([string]::IsNullOrWhiteSpace($path)) {
        $path = '/'
    }
    return $LocalBaseUrl.TrimEnd('/') + $path
}

function Test-SitemapLinks {
    param(
        [string]$LocalBaseUrl,
        [hashtable]$TopologyHeaders,
        [string]$WebsiteHost,
        [string]$HomeHtml,
        [string]$ContainerName
    )

    Write-Host 'Sitemap' -ForegroundColor Yellow
    $sitemap = Get-HttpResult -Url "$LocalBaseUrl/sitemap.xml" -TimeoutSec 45 -HostHeader $TopologyHeaders.HostHeader -OriginHeader $TopologyHeaders.OriginHeader
    $linkUrls = @()
    $sitemapLabel = '/sitemap.xml'

    if ($sitemap.StatusCode -eq 200 -and $sitemap.Content -match '<urlset') {
        $linkUrls = Get-LocUrlsFromSitemapXml -XmlText $sitemap.Content
        Write-TestResult -Passed $true -Name 'Sitemap' -Detail "/sitemap.xml HTTP $($sitemap.StatusCode) ($($linkUrls.Count) loc URLs)"
    }
    else {
        $pageHref = $null
        foreach ($href in (Get-HrefUrlsFromHtml -Html $HomeHtml)) {
            if ($href -match '(?i)sitemap') {
                $pageHref = $href
                break
            }
        }
        if ($pageHref) {
            $pageLocal = Convert-ToLocalTestUrl -Loc $pageHref -LocalBaseUrl $LocalBaseUrl -WebsiteHost $WebsiteHost
            if ($pageLocal) {
                $sitemapLabel = $pageHref
                $sitemap = Get-HttpResult -Url $pageLocal -TimeoutSec 45 -HostHeader $TopologyHeaders.HostHeader -OriginHeader $TopologyHeaders.OriginHeader
                if ($sitemap.StatusCode -eq 200) {
                    if ($sitemap.Content -match '<urlset') {
                        $linkUrls = Get-LocUrlsFromSitemapXml -XmlText $sitemap.Content
                    }
                    else {
                        $linkUrls = Get-HrefUrlsFromHtml -Html $sitemap.Content
                    }
                    Write-TestResult -Passed $true -Name 'Sitemap' -Detail "$sitemapLabel HTTP $($sitemap.StatusCode) ($($linkUrls.Count) links)"
                }
            }
        }
        if ($linkUrls.Count -eq 0) {
            Write-TestResult -Passed $false -Name 'Sitemap' -Detail ("Could not open sitemap.xml HTTP {0} {1}" -f $sitemap.StatusCode, $sitemap.Error)
            return 1
        }
    }

    $unique = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($loc in $linkUrls) {
        if ($seen.Add($loc)) {
            [void]$unique.Add($loc)
        }
    }

    Write-Host ("  Sitemap links ({0}):" -f $unique.Count) -ForegroundColor Yellow
    $bareSectionErrorIsSearchBox = Test-ContainerSearchBoxErrorsOnly -ContainerName $ContainerName
    $broken = 0
    foreach ($loc in $unique) {
        $localUrl = Convert-ToLocalTestUrl -Loc $loc -LocalBaseUrl $LocalBaseUrl -WebsiteHost $WebsiteHost
        if (-not $localUrl) {
            Write-Host ("    SKIP  {0}  (external or non-http)" -f $loc)
            continue
        }
        $hit = Get-HttpResult -Url $localUrl -TimeoutSec 30 -HostHeader $TopologyHeaders.HostHeader -OriginHeader $TopologyHeaders.OriginHeader
        $fatalSection = Test-HasFatalSectionRenderError -Html $hit.Content -BareSectionErrorIsSearchBox:$bareSectionErrorIsSearchBox
        $searchTied = (Test-IsSearchModuleError -Html $hit.Content) -or (
            $bareSectionErrorIsSearchBox -and (Test-HtmlHasSectionRenderError -Html $hit.Content) -and -not $fatalSection
        )
        $httpOk = $hit.StatusCode -ge 200 -and $hit.StatusCode -lt 400
        $ok = ($httpOk -or $searchTied) -and $hit.StatusCode -gt 0 -and $hit.Content -notmatch '(?i)No matching Localization' -and (-not $fatalSection)
        $statusText = if ($hit.StatusCode -gt 0) { [string]$hit.StatusCode } else { 'ERR' }
        if ($ok) {
            if ($searchTied) {
                Write-Host ("    WARN  {0,-5} {1}  Search:Entity:SearchBox ignored" -f $statusText, $loc) -ForegroundColor DarkYellow
            }
            else {
                Write-Host ("    {0,-5} {1}" -f $statusText, $loc) -ForegroundColor Green
            }
        }
        else {
            $broken++
            $err = $hit.Error
            Write-Host ("    {0,-5} {1}  {2}" -f $statusText, $loc, $err) -ForegroundColor Red
            if (Test-HtmlHasSectionRenderError -Html $hit.Content) {
                Write-SectionRenderErrorLog -PageUrl $loc -Html $hit.Content -ContainerName $ContainerName
            }
        }
    }

    if ($broken -gt 0) {
        Write-TestResult -Passed $false -Name 'Sitemap links' -Detail ("{0} of {1} links failed" -f $broken, $unique.Count)
        return 1
    }
    Write-TestResult -Passed $true -Name 'Sitemap links' -Detail ("{0} links resolved" -f $unique.Count)
    return 0
}

function Get-TopologyRequestHeaders {
    param($Mapping, [string]$FallbackHost)

    $result = @{
        HostHeader   = $FallbackHost
        OriginHeader = $null
    }

    if (-not $Mapping -or -not $Mapping.domain) {
        return $result
    }

    $protocol = [string]$Mapping.protocol
    if ([string]::IsNullOrWhiteSpace($protocol)) {
        $protocol = 'http'
    }
    $protocol = $protocol.ToLowerInvariant()
    $domain = [string]$Mapping.domain
    $port = [string]$Mapping.port
    $defaultPort = if ($protocol -eq 'https') { '443' } else { '80' }

    if ([string]::IsNullOrWhiteSpace($port) -or $port -eq $defaultPort) {
        $result.HostHeader = $domain
        $result.OriginHeader = '{0}://{1}' -f $protocol, $domain
    }
    else {
        $result.HostHeader = '{0}:{1}' -f $domain, $port
        $result.OriginHeader = '{0}://{1}:{2}' -f $protocol, $domain, $port
    }

    return $result
}

function Test-DockerAvailable {
    & docker version --format '{{.Server.Version}}' 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Docker is not available. Start Docker Desktop / the Docker engine and retry.'
    }
}

function Ensure-LocalHostsEntry {
    param(
        [string]$HostName,
        [string]$IpAddress = '127.0.0.1'
    )

    if ([string]::IsNullOrWhiteSpace($HostName)) {
        return
    }
    $parsed = $null
    if ([System.Net.IPAddress]::TryParse($HostName, [ref]$parsed)) {
        return
    }

    $hostsPath = Join-Path $env:SystemRoot 'System32\drivers\etc\hosts'
    if (-not (Test-Path -LiteralPath $hostsPath)) {
        Write-Host ("Hosts file not found: {0}" -f $hostsPath) -ForegroundColor DarkYellow
        return
    }

    $lines = Get-Content -LiteralPath $hostsPath
    foreach ($line in $lines) {
        $trim = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trim) -or $trim.StartsWith('#')) {
            continue
        }
        $commentIndex = $trim.IndexOf('#')
        if ($commentIndex -ge 0) {
            $trim = $trim.Substring(0, $commentIndex).Trim()
        }
        $tokens = $trim -split '\s+'
        if ($tokens.Count -lt 2) {
            continue
        }
        if ($tokens[0] -ne $IpAddress) {
            continue
        }
        for ($i = 1; $i -lt $tokens.Count; $i++) {
            if ($tokens[$i] -eq $HostName) {
                Write-Host ("Hosts file already has {0} {1}" -f $IpAddress, $HostName) -ForegroundColor DarkGray
                return
            }
        }
    }

    $entry = "{0} {1}" -f $IpAddress, $HostName
    try {
        Add-Content -LiteralPath $hostsPath -Value $entry -Encoding ASCII
        Write-Host ("Added to hosts file: {0}" -f $entry) -ForegroundColor Green
    }
    catch {
        Write-Host ("Could not add '{0}' to {1}. Run this script as Administrator. {2}" -f $entry, $hostsPath, $_.Exception.Message) -ForegroundColor DarkYellow
    }
}

function Get-HttpResult {
    param(
        [string]$Url,
        [int]$TimeoutSec = 45,
        [string]$HostHeader,
        [string]$OriginHeader
    )

    $result = @{
        Url        = $Url
        StatusCode = 0
        Content    = ''
        MediaType  = ''
        Error      = $null
        HostHeader = $HostHeader
        OriginHeader = $OriginHeader
    }

    try {
        if ($HostHeader -or $OriginHeader) {
            $request = [System.Net.HttpWebRequest]::Create($Url)
            $request.Method = 'GET'
            $request.Timeout = [Math]::Max(1000, $TimeoutSec * 1000)
            $request.AllowAutoRedirect = $true
            if ($HostHeader) {
                $request.Host = $HostHeader
            }
            if ($OriginHeader) {
                $request.Headers['Origin'] = $OriginHeader
            }
            $response = $request.GetResponse()
            $result.StatusCode = [int]$response.StatusCode
            $result.MediaType = [string]$response.ContentType
            $stream = $response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $result.Content = $reader.ReadToEnd()
                $reader.Close()
            }
            $response.Close()
        }
        else {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec -MaximumRedirection 5
            $result.StatusCode = [int]$response.StatusCode
            $result.Content = [string]$response.Content
            if ($response.Headers['Content-Type']) {
                $result.MediaType = [string]$response.Headers['Content-Type']
            }
        }
    }
    catch {
        $result.Error = $_.Exception.Message
        $errResponse = $null
        $ex = $_.Exception
        while ($null -ne $ex) {
            if ($ex -is [System.Net.WebException]) {
                $errResponse = $ex.Response
                break
            }
            $ex = $ex.InnerException
        }
        if ($null -ne $errResponse) {
            try {
                $result.StatusCode = [int]$errResponse.StatusCode
            }
            catch {
            }
            try {
                $stream = $errResponse.GetResponseStream()
                if ($null -ne $stream) {
                    $reader = New-Object System.IO.StreamReader($stream)
                    $result.Content = $reader.ReadToEnd()
                    $reader.Close()
                }
            }
            catch {
            }
        }
    }

    return $result
}

function Write-TestResult {
    param(
        [bool]$Passed,
        [string]$Name,
        [string]$Detail
    )

    if ($Passed) {
        Write-Host ("  PASS  {0}  {1}" -f $Name, $Detail) -ForegroundColor Green
    }
    else {
        Write-Host ("  FAIL  {0}  {1}" -f $Name, $Detail) -ForegroundColor Red
    }
}

Test-DockerAvailable

if ($null -ne $websiteUri) {
    Ensure-LocalHostsEntry -HostName $websiteUri.Host -IpAddress '127.0.0.1'
}

& docker image inspect $imageTag 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Image '$imageTag' was not found. Run .\test\New-CmTestDockerfile.ps1 -Framework $Framework -CmServerIp <ip> first."
}

$discoveryUri = Get-ImageDiscoveryUri -ImageTag $imageTag
if (-not $CmServerIp) {
    if ($discoveryUri -and $discoveryUri.Host) {
        $parsed = $null
        if ([System.Net.IPAddress]::TryParse($discoveryUri.Host, [ref]$parsed)) {
            $CmServerIp = $discoveryUri.Host
        }
    }
}
if (-not $CmServerIp) {
    throw 'Provide -CmServerIp (or rebuild the image with New-CmTestDockerfile.ps1 so Dxa__Services__Discovery contains the CM IP).'
}

$discoveryPort = 8082
if ($discoveryUri -and $discoveryUri.Port -gt 0) {
    $discoveryPort = $discoveryUri.Port
}
$discoveryBase = "http://${CmServerIp}:${discoveryPort}/discovery.svc"
$tokenUrl = "http://${CmServerIp}:${discoveryPort}/token.svc"

Write-Host "Discovery: $discoveryBase" -ForegroundColor DarkGray
$discoveryInfo = Get-DiscoveryCapabilityInfo -DiscoveryBaseUrl $discoveryBase -TokenUrl $tokenUrl
$capabilityHosts = @($discoveryInfo.Hosts)
$dockerHostNames = New-Object System.Collections.Generic.HashSet[string]
$addHostArgs = @()
foreach ($capabilityHost in $capabilityHosts) {
    $parsedHost = $null
    $isIp = [System.Net.IPAddress]::TryParse($capabilityHost, [ref]$parsedHost)
    if ($isIp -or $capabilityHost -eq 'localhost' -or $capabilityHost -eq '127.0.0.1') {
        continue
    }
    if ($dockerHostNames.Add($capabilityHost)) {
        Write-Host ("Mapping Discovery host {0} -> {1} (docker --add-host)" -f $capabilityHost, $CmServerIp) -ForegroundColor Cyan
        $addHostArgs += '--add-host'
        $addHostArgs += "${capabilityHost}:$CmServerIp"
    }
}
if ($null -ne $websiteUri -and $dockerHostNames.Add($websiteUri.Host)) {
    Write-Host ("Mapping Topology website host {0} -> {1} (docker --add-host)" -f $websiteUri.Host, $CmServerIp) -ForegroundColor Cyan
    $addHostArgs += '--add-host'
    $addHostArgs += "$($websiteUri.Host):$CmServerIp"
}
if ($addHostArgs.Count -eq 0) {
    Write-Host 'No extra Docker host mappings (Discovery URLs already use an IP or localhost).' -ForegroundColor DarkGray
}

$topologyHeaders = @{
    HostHeader   = $null
    OriginHeader = $null
}
if ($null -ne $websiteUri) {
    $topologyHeaders = Get-HeadersFromWebsiteUrl -WebsiteUri $websiteUri
    Write-Host ("Topology website {0} -> Host '{1}', Origin '{2}'" -f $WebsiteUrl, $topologyHeaders.HostHeader, $topologyHeaders.OriginHeader) -ForegroundColor Cyan
}

$existing = & docker ps -aq --filter "name=^/${containerName}$"
if (-not $existing) {
    $existing = & docker ps -aq --filter "name=$containerName"
}
if ($existing) {
    Write-Host "Removing existing container $containerName..." -ForegroundColor DarkGray
    & docker rm -f $containerName | Out-Null
}

Write-Host ("Starting {0} as {1} (host port {2} -> container 8080)..." -f $imageTag, $containerName, $HostPort) -ForegroundColor Cyan
$containerId = & docker run -d --name $containerName -p "${HostPort}:8080" `
    -e Dxa__PreferOriginHeaderForLocalizationResolver=true `
    -e ASPNETCORE_ENVIRONMENT=Development `
    @addHostArgs $imageTag
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
    throw ("docker run failed for {0}. If host port {1} is in use, stop the process bound to it (often IIS on 80) or pass -HostPort. Topology localization requires the browser port to match the website Base URL (default 80, not 8080)." -f $imageTag, $HostPort)
}

$failed = 0
$runError = $null
try {
    Write-Host "Waiting for the site at $baseUrl ..." -ForegroundColor Cyan
    $deadline = [DateTime]::UtcNow.AddSeconds($StartupTimeoutSec)
    $health = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        $health = Get-HttpResult -Url "$baseUrl/system/health" -TimeoutSec 5
        if ($health.StatusCode -eq 200 -and $health.Content -match 'DXA Health Check OK') {
            break
        }
        Start-Sleep -Seconds 2
    }

    Write-Host 'Connectivity tests' -ForegroundColor Yellow

    $siteUp = $health.StatusCode -eq 200 -and $health.Content -match 'DXA Health Check OK'
    if ($siteUp) {
        Write-TestResult -Passed $true -Name 'Site running' -Detail '/system/health returned 200 DXA Health Check OK'
    }
    else {
        $failed++
        $detail = if ($health.Error) { $health.Error } else { "HTTP $($health.StatusCode)" }
        Write-TestResult -Passed $false -Name 'Site running' -Detail "/system/health: $detail"
    }

    $homePage = Get-HttpResult -Url "$baseUrl/" -TimeoutSec 45 -HostHeader $topologyHeaders.HostHeader -OriginHeader $topologyHeaders.OriginHeader
    if (-not (Test-PageLocalizationOk -HttpResult $homePage) -and [string]::IsNullOrWhiteSpace($WebsiteUrl)) {
        Write-Host 'Home page missed Topology website URL; trying GraphQL mapping then Origin probes (not port 8080)...' -ForegroundColor DarkYellow
        $publicationMapping = Get-PublicationMapping -ContentServiceUrl $discoveryInfo.ContentServiceUrl -CmServerIp $CmServerIp -Token $discoveryInfo.Token -CandidateHosts $capabilityHosts
        $fallbackHeaders = Get-TopologyRequestHeaders -Mapping $publicationMapping -FallbackHost $null
        $originProbes = New-Object System.Collections.Generic.List[object]
        [void]$originProbes.Add($fallbackHeaders)
        if ($null -ne $websiteUri) {
            $probeHost = $websiteUri.Host
            [void]$originProbes.Add(@{ HostHeader = $probeHost; OriginHeader = "https://${probeHost}" })
            [void]$originProbes.Add(@{ HostHeader = $probeHost; OriginHeader = "http://${probeHost}" })
            [void]$originProbes.Add(@{ HostHeader = "${probeHost}:443"; OriginHeader = "https://${probeHost}:443" })
            [void]$originProbes.Add(@{ HostHeader = $probeHost; OriginHeader = "http://${probeHost}:80" })
            [void]$originProbes.Add(@{ HostHeader = "${probeHost}:81"; OriginHeader = "http://${probeHost}:81" })
        }
        foreach ($probe in $originProbes) {
            if ([string]::IsNullOrWhiteSpace($probe.HostHeader) -and [string]::IsNullOrWhiteSpace($probe.OriginHeader)) {
                continue
            }
            if ($probe.OriginHeader -eq $topologyHeaders.OriginHeader -and $probe.HostHeader -eq $topologyHeaders.HostHeader) {
                continue
            }
            $homePage = Get-HttpResult -Url "$baseUrl/" -TimeoutSec 45 -HostHeader $probe.HostHeader -OriginHeader $probe.OriginHeader
            if (Test-HomePageSuccess -HttpResult $homePage) {
                $topologyHeaders = $probe
                Write-Host ("Home page succeeded with Origin '{0}' Host '{1}'" -f $probe.OriginHeader, $probe.HostHeader) -ForegroundColor Cyan
                break
            }
        }
    }
    if (Test-HomePageSuccess -HttpResult $homePage) {
        $hostNote = if ($topologyHeaders.OriginHeader) { " Origin=$($topologyHeaders.OriginHeader)" } elseif ($topologyHeaders.HostHeader) { " Host=$($topologyHeaders.HostHeader)" } else { '' }
        Write-TestResult -Passed $true -Name 'Home page' -Detail "HTTP $($homePage.StatusCode) HTML from /$hostNote"
    }
    elseif (Test-PageLocalizationOk -HttpResult $homePage -and (Test-IsSearchModuleError -Html $homePage.Content) -and -not (Test-HasFatalSectionRenderError -Html $homePage.Content)) {
        $hostNote = if ($topologyHeaders.OriginHeader) { " Origin=$($topologyHeaders.OriginHeader)" } elseif ($topologyHeaders.HostHeader) { " Host=$($topologyHeaders.HostHeader)" } else { '' }
        Write-TestResult -Passed $true -Name 'Home page' -Detail "HTTP $($homePage.StatusCode) HTML from /$hostNote (Search:Entity:SearchBox ignored)"
        Write-Host '  WARN  Home page Search Module errors are ignored.' -ForegroundColor DarkYellow
    }
    else {
        $failed++
        if (Test-PageLocalizationOk -HttpResult $homePage) {
            Write-TestResult -Passed $false -Name 'Home page' -Detail 'Page opened but a region failed to render.'
            Write-SectionRenderErrorLog -PageUrl "$baseUrl/" -Html $homePage.Content -ContainerName $containerName
        }
        else {
            $snippet = ''
            if ($homePage.Content) {
                $collapsed = ($homePage.Content -replace '\s+', ' ')
                $snippet = $collapsed.Substring(0, [Math]::Min(180, $collapsed.Length))
            }
            $detail = "HTTP $($homePage.StatusCode) $($homePage.Error) $snippet"
            if ($homePage.Content -match 'No matching Localization') {
                $detail += ' Topology website Base URL must match Origin/Host (default http://dxa.tridiondemo.com).'
            }
            Write-TestResult -Passed $false -Name 'Home page' -Detail $detail.Trim()
        }
    }

    if (Test-PageLocalizationOk -HttpResult $homePage) {
        $websiteHostName = $null
        if ($null -ne $websiteUri) {
            $websiteHostName = $websiteUri.Host
        }
        $sitemapFailed = Test-SitemapLinks -LocalBaseUrl $baseUrl -TopologyHeaders $topologyHeaders -WebsiteHost $websiteHostName -HomeHtml $homePage.Content -ContainerName $containerName
        if ($sitemapFailed -gt 0) {
            $failed++
        }
    }

    $nav = Get-HttpResult -Url "$baseUrl/navigation.json" -TimeoutSec 45 -HostHeader $topologyHeaders.HostHeader -OriginHeader $topologyHeaders.OriginHeader
    $navJson = $false
    if ($nav.StatusCode -eq 200 -and $nav.Content) {
        $trim = $nav.Content.TrimStart()
        $navJson = $trim.StartsWith('{') -or $trim.StartsWith('[')
    }
    if ($navJson) {
        Write-TestResult -Passed $true -Name 'CD communication' -Detail '/navigation.json returned JSON (Content Service / Discovery)'
    }
    else {
        $failed++
        $detail = if ($nav.Error) { $nav.Error } else { "HTTP $($nav.StatusCode) (expected JSON from CD)" }
        Write-TestResult -Passed $false -Name 'CD communication' -Detail "/navigation.json: $detail"
    }
}
catch {
    $runError = $_.Exception.Message
    Write-Host ("Start script error: {0}" -f $runError) -ForegroundColor Red
}
finally {
    $cleanup = ($failed -gt 0) -or $RemoveWhenDone -or (-not [string]::IsNullOrWhiteSpace($runError))
    if ($failed -gt 0 -or -not [string]::IsNullOrWhiteSpace($runError)) {
        Write-Host ''
        Write-Host 'Container logs (last 80 lines):' -ForegroundColor DarkYellow
        & docker logs --tail 80 $containerName
    }

    if ($cleanup) {
        Write-Host "Removing container $containerName..." -ForegroundColor DarkGray
        & docker rm -f $containerName 2>$null | Out-Null
    }
    else {
        Write-Host ("Container {0} is running. Browse {1}" -f $containerName, $browseUrl) -ForegroundColor Cyan
    }
}

Write-Host ''
if ($failed -eq 0 -and [string]::IsNullOrWhiteSpace($runError)) {
    Write-Host 'Site is running and CD communication succeeded.' -ForegroundColor Green
    if (-not $RemoveWhenDone) {
        Write-Host ("Opening default browser: {0}" -f $browseUrl) -ForegroundColor Cyan
        Start-Process $browseUrl
    }
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($runError)) {
    Write-Host ("Start script failed: {0}" -f $runError) -ForegroundColor Red
    exit 1
}

Write-Host ("{0} connectivity test(s) failed." -f $failed) -ForegroundColor Red
exit 1
