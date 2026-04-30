# Setting up the Agent on your server

No Docker yet — this is early and experimental. You set it up manually.

> **Security warning:** Authentication is not yet implemented. Do not expose port 5165 publicly or put it behind a reverse proxy that makes it reachable from the internet. Access it only over a VPN or local network. Auth is planned — this restriction is temporary.

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
dotnet publish Agent -c Release
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

## Step 3 — Create the systemd service

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

## Step 4 — Enable and start it

```bash
systemctl --user daemon-reload
systemctl --user enable --now slice-agent.service
systemctl --user status slice-agent.service
```

The agent is now running on port 5165.

---

## Step 5 — Set up Caddy (required for public URLs)

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

## Step 6 — Configure the agent

The agent reads reverse proxy settings from `appsettings.json` in the publish directory. The defaults are:

```json
"ReverseProxy": {
  "AdminUrl": "http://localhost:2019",
  "BaseDomain": "naslice.duckdns.org"
}
```

Change `BaseDomain` to your own domain if needed.

---

## Step 7 — Point your CLI at the server

On your local machine, set the agent URL:

```bash
export SLICE_AGENT_URL=http://<your-server-ip>:5165
```

Add it to `~/.bashrc` or `~/.zshrc` to make it permanent.

Verify it works:

```bash
slice list
```

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

> The agent has no authentication yet — make sure port 5165 is only reachable from machines you trust. Don't expose it publicly.

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
