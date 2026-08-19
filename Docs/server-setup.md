# Setting up the Agent on your server

No Docker yet — this is early and experimental. You set it up manually.

> **Security warning:** The agent can deploy and control processes on the host. API-key
> authentication is required, but it does not encrypt traffic. Keep port 5165 behind
> a firewall or VPN. If it must cross an untrusted network, put it behind HTTPS and
> do not expose the plain HTTP listener publicly.

---

## Prerequisites

- Linux with systemd (Raspberry Pi, VPS, anything)
- .NET 10 SDK — needed to build the agent on the server

---

## Step 1 — Get the code on your server

SSH into your server, then clone the repo:

```bash
git clone https://github.com/n4sser77/slice.git
cd slice
```

If you already have it and just want to update:

```bash
cd ~/slice
git pull
```

---

## Step 2 — Build the agent

Two options depending on your preference:

**Option A — AOT native binary (recommended)**

No .NET runtime needed to run the agent. Self-contained binary.

```bash
dotnet publish Agent -c Release -r linux-arm64
```

Output: `Agent/bin/Release/net10.0/linux-arm64/publish/Agent`

**Option B — Framework-dependent**

Smaller build, but requires the .NET 10 runtime with `Microsoft.AspNetCore.App` installed on the server.

```bash
dotnet publish Agent -c Release -p:PublishAot=false --output ./agent-out
```

Check that the runtime is available:

```bash
dotnet --list-runtimes
# should include: Microsoft.AspNetCore.App 10.x.x
```

---

## Step 3 — Configure API-key authentication

Generate a separate, high-entropy key on the server:

```bash
mkdir -p ~/.config/slice
umask 077
openssl rand -hex 32 > ~/.config/slice/api-key
printf 'SLICE_API_KEY=' > ~/.config/slice/agent.env
cat ~/.config/slice/api-key >> ~/.config/slice/agent.env
chmod 600 ~/.config/slice/api-key ~/.config/slice/agent.env
```

The environment file should contain one line in this form:

```text
SLICE_API_KEY=<random-key>
```

Do not commit either file, paste the key into the systemd unit, or reuse a password
or key from another service. The agent refuses authenticated endpoints when
`SLICE_API_KEY` is missing or empty.

Copy `~/.config/slice/api-key` to each trusted CLI machine over a secure channel.
Keep the copy readable only by your user.

---

## Step 4 — Create the systemd service

```bash
mkdir -p ~/.config/systemd/user
nano ~/.config/systemd/user/slice-agent.service
```

**If you used Option A (AOT)** — replace `<your-user>` with your username:

```ini
[Unit]
Description=Slice Agent

[Service]
WorkingDirectory=/home/<your-user>/slice/Agent/bin/Release/net10.0/linux-arm64/publish
ExecStart=/home/<your-user>/slice/Agent/bin/Release/net10.0/linux-arm64/publish/Agent
Restart=always
Environment=ASPNETCORE_HTTP_PORTS=5165
EnvironmentFile=%h/.config/slice/agent.env

[Install]
WantedBy=default.target
```

**If you used Option B (framework-dependent)** — you need the `dotnet` path and `DOTNET_ROOT`:

```ini
[Unit]
Description=Slice Agent

[Service]
WorkingDirectory=/home/<your-user>/slice/agent-out
ExecStart=<path-to-dotnet> /home/<your-user>/slice/agent-out/Agent.dll
Restart=always
Environment=ASPNETCORE_HTTP_PORTS=5165
Environment=DOTNET_ROOT=<your-dotnet-root>
EnvironmentFile=%h/.config/slice/agent.env

[Install]
WantedBy=default.target
```

### Finding your paths (Option B only)

```bash
which dotnet          # → use this for ExecStart
dotnet --info | grep "Base Path"  # → strip /sdk/... to get DOTNET_ROOT
```

Example if you installed via mise:

```
ExecStart=/home/yourname/.local/share/mise/shims/dotnet ...
DOTNET_ROOT=/home/yourname/.local/share/mise/dotnet-root
```

Example if you installed via apt or the Microsoft install script:

```
ExecStart=/usr/bin/dotnet ...
DOTNET_ROOT=/usr/share/dotnet
```

---

## Step 5 — Enable and start it

```bash
systemctl --user daemon-reload
systemctl --user enable --now slice-agent.service
systemctl --user status slice-agent.service
```

The agent is now running on port 5165.

---

## Step 6 — Set up Caddy (required for public URLs)

Caddy acts as the reverse proxy. It routes traffic from `myapp.yourdomain.com` to the right app and handles HTTPS automatically via Let's Encrypt.

Install:

```bash
sudo apt install -y caddy
```

Create a minimal Caddyfile at `/etc/caddy/Caddyfile`:

```
{
    admin localhost:2019
    email your@email.com
}

:80, :443 {
}
```

Start it:

```bash
sudo systemctl enable --now caddy
```

Verify the admin API is up:

```bash
curl http://localhost:2019/config/
```

Make sure your router forwards **port 80** and **port 443** to your server — Let's Encrypt needs port 80 to issue certificates.

---

## Step 7 — Configure the agent

The agent reads reverse proxy settings from `appsettings.json` in the publish directory. The defaults are:

```json
"ReverseProxy": {
  "AdminUrl": "http://localhost:2019",
  "BaseDomain": "your-domain.example.com"
}
```

Change `BaseDomain` to your own domain if needed.

---

## Step 8 — Configure and verify the CLI

On your local machine, protect the copied key and export the agent URL and key:

```bash
chmod 600 ~/.config/slice/api-key
export SLICE_AGENT_URL=http://<your-server-ip>:5165
export SLICE_API_KEY="$(cat ~/.config/slice/api-key)"
```

You can add the URL to `~/.bashrc` or `~/.zshrc`. Avoid writing the key itself into
shell startup files or command history; load it from the protected file instead.
All `slice` commands send the key as a Bearer token, so use HTTPS or a trusted
private network to prevent interception.

Verify it works:

```bash
slice list
```

An incorrect or missing key returns `Unauthorized`. Confirm that the CLI and agent
process receive the same value if that happens:

```bash
systemctl --user show slice-agent.service --property=EnvironmentFiles
test -n "$SLICE_API_KEY" && echo "CLI API key is set"
journalctl --user -u slice-agent.service -n 50
```

### Rotate the API key

Generate a new key, replace the contents of both protected key files, and restart
the agent. Existing CLI sessions keep the old environment value, so reload it after
the server restarts:

```bash
# server, after replacing ~/.config/slice/agent.env
systemctl --user restart slice-agent.service

# each CLI machine, after replacing ~/.config/slice/api-key
export SLICE_API_KEY="$(cat ~/.config/slice/api-key)"
```

The agent accepts one key at a time, so coordinate rotation to avoid a temporary
loss of CLI access.

---

## Deploying apps

```bash
# Deploy — app runs on localhost only (safe default)
slice deploy MyApp

# Deploy and expose publicly at myapp.<base-domain>
slice deploy MyApp --publish

# Deploy with a custom domain
slice deploy MyApp --publish --domain myapp.example.com
```

### Application configuration and secrets

Slice accepts one conventional key format and adapts it to the target runtime.
For .NET applications, use colon-separated hierarchy just as you would with
`dotnet user-secrets` or `appsettings.json`:

```bash
slice deploy MyApp \
  --config 'ConnectionStrings:Postgres=Host=db;Database=myapp' \
  --config 'Serilog:WriteTo:0:Name=Console'
```

Command-line values can be visible in shell history and process listings. Put
secrets in a private file instead:

```bash
umask 077
touch .env.slice
chmod 600 .env.slice
```

```dotenv
# Blank lines and full-line comments are allowed.
ConnectionStrings:Postgres="Host=db;Database=myapp;Password=replace-me"
ExternalApi:Key='replace-me'
PUBLIC_ORIGIN=https://app.example.com/path#fragment
EMPTY_VALUE=
```

Deploy it explicitly:

```bash
slice deploy MyApp --config-file .env.slice
```

Slice never auto-loads `.env.slice`. The file supports unquoted, single-quoted,
and double-quoted values; `\\`, `\"`, `\'`, `\n`, `\r`, and `\t` escapes; and
inline comments when whitespace precedes `#`. It deliberately does not support
`export`, variable expansion such as `$DATABASE_URL`, or multiline source
entries. The first `=` separates the key from its value.

#### Precedence and redeployment

Configuration is an app-level snapshot, not a patch:

| Deployment input | Result |
| --- | --- |
| No `--config` or `--config-file` | Preserve the stored configuration |
| `--config-file FILE` | Replace it with the file |
| File plus `--config KEY=VALUE` | Replace it; command-line keys override file keys |
| Explicit empty file | Clear it |

Duplicate keys within the file or within the command-line options are rejected
case-insensitively. Slice validates before building or changing the deployed app.
An older Agent that lacks configuration support is also rejected before build.

Keys may be flat (`DATABASE_URL`) or hierarchical
(`ConnectionStrings:Postgres`, `Serilog:WriteTo:0:Name`). Use `:`, never `__`;
Slice converts hierarchy for each runtime. `ASPNETCORE_*` and `DOTNET_*` roots are
reserved because Slice manages the service host. A deployment may contain at
most 128 entries, a 256-character key, 64 KiB per value, and 256 KiB total.

#### Storage and systemd

The Agent keeps the canonical, versioned snapshot here:

```text
$XDG_DATA_HOME/slice/apps/<service>/configuration.json
```

If `XDG_DATA_HOME` is unset, it uses
`~/.local/share/slice/apps/<service>/configuration.json`. For systemd, Slice
atomically generates `runtime/systemd.env`, changes `:` to `__`, and references
that file from the app's unit. Managed directories use mode `0700`; canonical
and runtime files use `0600`. Removing an app removes both copies. Selected
configuration files and `.env.slice` are excluded from deployment archives.

This boundary is intentionally runtime-neutral: a future Docker or Kubernetes
adapter can materialize the same canonical values in its own format without
changing what users enter.

Mode `0600` protects values from other unprivileged operating-system users, but
it is not a vault. Processes running as the same Unix user as the Agent or app,
and root, may be able to read them. Slice currently treats apps deployed under
one Agent account as mutually trusted. Use separate OS users or machines when
applications must be isolated from one another, and keep the Agent behind TLS or
a trusted private network.

> API-key authentication does not replace network isolation or TLS. Keep port 5165
> reachable only from trusted machines, preferably over a VPN or private network.

---

## Updating the agent

If you already have the repo and want to upgrade:

```bash
cd ~/slice
git pull
dotnet publish Agent -c Release   # or whichever option you used initially
systemctl --user restart slice-agent.service
```

---

## Troubleshooting

```bash
# live logs
journalctl --user -u slice-agent.service -f

# check it's actually running
systemctl --user status slice-agent.service

# restart after changes
systemctl --user restart slice-agent.service
```
