# Combined CM/CD test server (AWS)

Use this when a **single** Tridion Sites Content Manager + Content Delivery host is running on AWS for DXA testing. The example web app lives in `dxa-web-application-mvc-net` and talks to CD over HTTP; it does not need the CME UI to render pages.

Scripts for this setup live in `test/`. Run them from the **repository root**.

Replace `cm-cd.example.internal` (or the instance public DNS / Elastic IP) everywhere you see a hostname.

## Quick Start

On the Windows CM/CD instance (elevated PowerShell), open Windows Firewall, then from your DXA machine verify ports, build the Example WebApp image, and start it:

```powershell
# 1. On the CM/CD host (Administrator): open DXA runtime ports
.\test\Open-CmTestFirewallPorts.ps1 -Section DxaRuntime

# 2. From the DXA / Docker machine: confirm CM/CD ports (replace with the instance IP)
.\test\Test-DxaRuntimePorts.ps1 -CmServerIp 192.0.2.10

# 3. Build a gitignored test image pointed at that CM IP (net8 or net10)
.\test\New-CmTestDockerfile.ps1 -Framework net10 -CmServerIp 192.0.2.10

# 4. Start the container and run site / CD / sitemap checks
.\test\Start-CmTestWebApp.ps1 -Framework net10
```

Step 3 also runs step 2 for DXA runtime ports and skips the Docker build if they are closed. Attach AWS security group **DXA-CmTest** to each instance manually before step 2. Optional: `-Section Publishing` / `Optional` / `WebApp` in step 1.

---

## Ports to open

Open these on the AWS security group **DXA-CmTest** (and Windows Firewall on the instance) from the machine that runs the DXA web app. For a locked-down test box, restrict the source to your office/VPN CIDR, not `0.0.0.0/0`.

**DXA-CmTest** has been added as an AWS security group with the ports listed below. Attach it to each EC2 instance yourself: AWS does not apply a new group to existing instances. In the EC2 console, select the instance → **Actions** → **Security** → **Change security groups** → add **DXA-CmTest** (keep the existing groups unless you intend to replace them). Repeat for every CM/CD (and DXA) instance that should be reachable for this test setup.

On the Windows CM/CD (or DXA) host, open inbound TCP ports in Windows Defender Firewall by **section ID** (elevated PowerShell):

| ID | Section |
|----|---------|
| `DxaRuntime` | Required for DXA runtime |
| `Publishing` | Required for publishing (CM on the same box) |
| `Optional` | Optional (open only if you use the feature) |
| `WebApp` | DXA web app listen port (local, not AWS) |

```powershell
.\test\Open-CmTestFirewallPorts.ps1 -Section DxaRuntime
.\test\Open-CmTestFirewallPorts.ps1 -Section DxaRuntime,Publishing
.\test\Open-CmTestFirewallPorts.ps1 -Section Optional
.\test\Open-CmTestFirewallPorts.ps1 -Section WebApp
```

Rules are named `DXA CmTest - <ID> - TCP <port>`. Existing rules with the same name are left unchanged.

### Required for DXA runtime

DXA only needs Discovery in config. After that it follows Discovery for Token, Content (GraphQL / Content Service), and optionally Session Content and search. Those follow-on ports still must be reachable.

| Port | Protocol | Service | Why |
|------|----------|---------|-----|
| **8082** | TCP | Discovery Service (`/discovery.svc`) and Token Service (`/token.svc`) | First hop. OAuth token + capability URLs. |
| **8081** | TCP | Content Service (`/content.svc`, GraphQL) | Page/entity content. Capability URL is registered in Discovery. |
| **8083** | TCP | Session-enabled Content Service | Experience Manager / session preview. Open if you test XPM. |

From the machine that will run DXA, test **all** CmTestSetup ports against the CM/CD **IP address**. Output is grouped by the section names below:

```powershell
.\test\Test-DxaRuntimePorts.ps1 -CmServerIp 192.0.2.10
```

If Discovery TCP (default **8082**) is open, the script calls Discovery (`/discovery.svc`) and Token (`/token.svc`, using Example WebApp OAuth `cduser`) and **lists each capability URL and port**. When a capability port differs from the CmTestSetup default, that default is **overridden** for the TCP checks that follow. Capability URLs that still say `localhost` are listed with a warning; the port test still uses the CM server IP.

The script exits `0` if every port in **Required for DXA runtime** and **Required for publishing** (after any Discovery overrides) accepts a TCP connection. Closed ports in **Optional** are reported but do not fail the script. Port **8080** (DXA web app listen port) is local and is not tested against the CM server.

### Required for publishing (CM on the same box)

| Port | Protocol | Service | Why |
|------|----------|---------|-----|
| **80** / **443** | TCP | IIS (CME, Topology Manager if bound here, Core Service) | CME, Topology Manager, Core Service. Prefer 443 if TLS is configured. |
| **81** | TCP | Topology Manager (common alternate IIS binding) | Only if TTM is not on 80/443. Confirm the site binding on the instance. |
| **8084** | TCP | Content Deployer | Publish from CM to CD. Needed even on a combined box if the deployer listens on this port. |

### Optional (open only if you use the feature)

| Port | Protocol | Service | Why |
|------|----------|---------|-----|
| **8087** | TCP | Context Engine | Device/context claims. |
| **8097** | TCP | IQ Query | Search (DXA Search module / `IQSearchIndex` in appsettings). Confirm the IQ Query port on your Sites version if search fails. |
| **9200** | TCP | Elasticsearch / OpenSearch | Only if clients query the search engine directly (unusual for DXA). Prefer keeping this private. |
| **3389** | TCP | RDP | Windows admin. Restrict to your IP. |
| **1433** | TCP | SQL Server | Only if the database is on this instance **and** you connect from outside. Prefer leaving it closed and using RDP/VPN. |

### DXA web app listen port (local, not AWS)

The example app binds **8080** (`URLs` in `appsettings.json`). Open 8080 on the **web app host**, not on the CM/CD security group, unless you also host DXA on the AWS instance.

---

## Discovery capability URLs on the CM/CD server

After opening ports, Discovery must advertise **hostnames the DXA app can resolve**, not `localhost`.

On the AWS instance, capabilities registered in Discovery (Content, Token, Session Content, Deployer, IQ Query) should use the public or private DNS you will call from DXA, for example:

`http://cm-cd.example.internal:8081/content.svc`

If capabilities still say `http://localhost:8081/...`, DXA will authenticate against Discovery successfully and then fail when it follows those URLs.

Also register/update the **website Base URL** in Topology Manager so it matches the URL you use in the browser for the DXA site (for example `http://localhost:8080` or your test hostname).

---

## Web app config to change

File: `dxa-web-application-mvc-net/dotnet/src/Tridion.Dxa.Example.WebApp/appsettings.json`

Use `appsettings.Development.json` for local overrides if you prefer not to edit the checked-in file.

### 1. Discovery endpoint (required)

Default:

```json
"Dxa": {
  "Services": {
    "Discovery": "http://localhost:8082/discovery.svc"
  }
}
```

Change `localhost` to the AWS host:

```json
"Discovery": "http://cm-cd.example.internal:8082/discovery.svc"
```

Use `https://` and the TLS port if Discovery is bound with certificates.

To test in Docker without editing the committed Dockerfile or `appsettings.json`, generate a local copy (gitignored) that rewrites Discovery to the CM IP:

```powershell
.\test\New-CmTestDockerfile.ps1 -Framework net8 -CmServerIp 192.0.2.10
.\test\New-CmTestDockerfile.ps1 -Framework net10 -CmServerIp 192.0.2.10
```

The script first runs `Test-DxaRuntimePorts.ps1` for **Required for DXA runtime** (8082, 8081, 8083). If any of those ports are closed, it skips `docker build` and exits `1`. Otherwise it writes `Dockerfile.cmtest` or `Dockerfile.net10.0.cmtest` and builds (`dxa-example-webapp:net8-cmtest` or `dxa-example-webapp:net10-cmtest`). Do not commit `*.cmtest` files.

Start the image and run basic site/CD checks (site `/system/health`, home page `/`, CD `/navigation.json`):

```powershell
.\test\Start-CmTestWebApp.ps1 -Framework net8
.\test\Start-CmTestWebApp.ps1 -Framework net10
```

The script replaces any existing `dxa-cmtest` container, publishes the **Topology website port** (default **80**) to container 8080, and leaves it running on **success** (then opens `http://dxa.tridiondemo.com/` in the default browser — not `:8080`, which Topology does not map). If any check fails (or the script errors), it prints the last 80 log lines and **removes** the container. Use `-RemoveWhenDone` to delete it after a successful run as well. If Docker cannot bind port 80, stop IIS or whatever is using it.

Discovery often advertises Token/Content URLs on a hostname such as `dxd.tridiondemo.com` even when you pointed DXA at the CM IP. The start script adds Docker `--add-host` for those CD hosts **and** for the Topology website host (`dxa.tridiondemo.com` by default).

Page requests use Topology website **`http://dxa.tridiondemo.com`** (`Host` / `Origin`), not `dxd.tridiondemo.com` and not Docker port **8080**. Override with `-WebsiteUrl`. The start script adds `127.0.0.1 dxa.tridiondemo.com` to the local hosts file if that entry is missing (requires Administrator to write). The container sets `Dxa__PreferOriginHeaderForLocalizationResolver`.

After a successful home page, the script opens `/sitemap.xml` (or a sitemap link on the home page), then GETs each sitemap URL and prints **URL** and **HTTP status**. Errors tied to `Search:Entity:SearchBox` are **WARN** only and do not fail the home page or sitemap. Other **A problem occurred while rendering this section** errors still fail and print page/container logs. Broken links fail the run.

### 2. OAuth (required unless CD OAuth is disabled)

Default:

```json
"OAuth": {
  "Enabled": true,
  "ClientId": "cduser",
  "ClientSecret": "CDUserP@ssw0rd"
}
```

Align `ClientId` / `ClientSecret` with the CD user in the Token Service on that environment (`cd_ambient_conf.xml` / Token Service config). If OAuth is off on this test CD, set `"Enabled": false`.

### 3. App listen URL (optional)

Default:

```json
"URLs": "http://*:8080"
```

Change the port or host binding if IIS, a load balancer, or a conflict requires it. Topology Manager website mapping must match whatever URL you actually browse.

### 4. Search (only if the Search module is used)

`IQSearchIndex` defaults to `udp-index`. Change it if the AWS IQ / Elasticsearch index name is different. IQ Query itself is resolved via Discovery once port **8097** (or your actual IQ Query port) is open.

### 5. Redis (leave as-is for a single test node)

`SdlWebDelivery:Caching` defaults to in-memory handlers (`regularCache` / `longLivedCache`). Redis (`localhost:6379`) is only used if you switch regions to `regularDistributedCache` / `longLivedDistributedCache`. Do not open Redis on AWS unless you actually enable those handlers.

### 6. Logging (optional for test)

For troubleshooting CD connectivity, raise log levels in `appsettings.Development.json` (already `Debug` for `Default`) and check `logs/sites-*.log` under the web app.

---

## Minimal change checklist

1. Attach security group **DXA-CmTest** to each instance manually, then confirm **8082**, **8081**, plus **8083** if testing XPM, **8084** if you publish, **80/443** (and **81** if needed) for CME/TTM.
2. Discovery capabilities: no `localhost` URLs for services DXA will call.
3. `appsettings.json`: `Dxa:Services:Discovery` → AWS host; `Dxa:OAuth` matches Token Service.
4. Topology Manager: website base URL matches the DXA site URL.
5. From the web app host, verify:
   - `http://<aws-host>:8082/discovery.svc`
   - Token endpoint (usually `http://<aws-host>:8082/token.svc`)
   - Content / GraphQL URL returned by Discovery
