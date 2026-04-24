# Setting up the Agent on your server

No Docker yet — this is early and experimental. You set it up manually.

---

## Prerequisites

- Linux with systemd (Raspberry Pi, VPS, anything)
- .NET 10 runtime with `Microsoft.AspNetCore.App` — not just the base runtime

Check after installing:

```bash
dotnet --list-runtimes
# should include: Microsoft.AspNetCore.App 10.x.x
```

---

## Step 1 — Clone the repo on your server

SSH into your server, then:

```bash
git clone https://github.com/n4sser77/slice.git
cd slice
```

---

## Step 2 — Publish the agent

```bash
dotnet publish Agent --configuration Release --output ./agent-out
```

---

## Step 3 — Create the systemd service

Create the file `~/.config/systemd/user/slice-agent.service`:

```bash
mkdir -p ~/.config/systemd/user
nano ~/.config/systemd/user/slice-agent.service
```

Paste this — **replace the paths with your own**:

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

### Finding your paths

Run these on the server to get the right values:

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

## Step 5 — Point your CLI at it

On your local machine:

```bash
export SLICE_AGENT_URL=http://<your-server-ip>:5165
slice deploy MyApp
```

> The agent has no authentication yet — make sure port 5165 is only reachable from machines you trust. Don't expose it publicly.

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
