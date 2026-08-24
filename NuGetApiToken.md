# Create a NuGet.org API token

Use a **nuget.org API key** when you push a stable DXA release to the public feed with `Release-Dxa.ps1`. Do not use the default Nexus key from `build.proj` against nuget.org.

Official reference: [Create API keys](https://learn.microsoft.com/en-us/nuget/nuget-org/publish-a-package#create-api-keys).

## Prerequisites

- A [nuget.org](https://www.nuget.org/) account that is allowed to publish `Tridion.Dxa.*` packages (organization membership as required by RWS).
- Two-factor authentication enabled on that account (required by nuget.org).

## Create the key

1. Sign in at [https://www.nuget.org/](https://www.nuget.org/).
2. Open **API Keys**: [https://www.nuget.org/account/apikeys](https://www.nuget.org/account/apikeys) (or select your username, then **API Keys**).
3. Select **Create**.
4. Fill in:
   - **Key name** — for example `dxa-core-push-YYYY-MM`.
   - **Expires** — pick a short lifetime (for example 1 year or less). Rotate before expiry.
   - **Glob pattern** — `Tridion.Dxa.*` (limits the key to DXA package IDs).
   - **Select scopes** — enable **Push** (new packages and new versions). Enable **Unlist** only if you must unlist a bad package.
5. Select **Create**. Copy the key immediately. nuget.org shows the full value **once**.

Store the key in a password manager or a CI secret. Never commit it to git, paste it into `build.proj`, or share it in chat.

## Use the key

Push a verified stable build to nuget.org:

```powershell
.\Release-Dxa.ps1 -NuGetSource https://api.nuget.org/v3/index.json -ApiKey <nuget-org-key>
```

Replace `<nuget-org-key>` with the value you copied. Preview releases (`-Preview`) stay on internal Nexus and must not use nuget.org.

To store the key locally for `dotnet nuget` (optional):

```powershell
dotnet nuget setapikey <nuget-org-key> --source https://api.nuget.org/v3/index.json
```

## If a key is leaked

1. On [API Keys](https://www.nuget.org/account/apikeys), **Regenerate** or **Remove** the leaked key.
2. Create a new key with the same glob and scopes.
3. Update any CI secrets or local `setapikey` entries.
