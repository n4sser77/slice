# slice

Deploy .NET apps to your own machine without touching systemd, scp, or ssh manually. Think Vercel but for .NET — and you own the server.

Built for a Raspberry Pi 5 but works on any Linux machine.

---

## How it works

Two parts:

- **Agent** — a minimal API that runs on your server. It receives deployments, writes systemd service files, and manages the lifecycle of your apps.
- **CLI** — what you run from your machine to talk to the agent.

---

## Option A — Just want to try it or use it?

> The CLI is not published to NuGet yet. Until then, clone the repo and build it yourself — it's a one time thing.

Clone and install:

```bash
git clone https://github.com/n4sser77/slice.git
cd slice
dotnet pack Agent.Cli --configuration Release
dotnet tool install --global --add-source ./Agent.Cli/bin/Release slice
```

Point the CLI at your server:

```bash
export SLICE_AGENT_URL=http://<your-server-ip>:5165
```

Once installed, the `slice` command is available everywhere on your machine:

```bash
slice deploy MyApp                                         # localhost only
slice deploy MyApp --publish                               # public HTTPS URL
slice deploy MyApp --publish --domain myapp.example.com   # custom domain
slice list
slice status MyApp
slice stop MyApp
```

The CLI packages your app, sends it to the agent running on your server, and the agent handles the rest — systemd service, port, and optionally a public HTTPS URL via Caddy.

**The agent needs to be running on your server first.** See [Docs/server-setup.md](Docs/server-setup.md) for how to get it running.

---

## Option B — Want to run it locally or contribute?

> You need Linux or WSL for this. The agent talks directly to systemd, so it won't work on plain Windows or macOS.

Clone the repo and use `dotnet run` directly — no install needed:

```bash
# terminal 1: start the agent (runs on http://localhost:5165)
dotnet run --project Agent

# terminal 2: use the CLI against the local agent
dotnet run --project Agent.Cli -- deploy MyApp
dotnet run --project Agent.Cli -- list

# run the tests
dotnet test
```

---

## Project structure

```
Agent/          the server — minimal API, manages systemd services
Agent.Cli/      the CLI — deploy, list, inspect
Common/         shared models between agent and CLI
Agent.Tests/
Agent.Cli.Tests/
Docs/           server setup, roadmap, learning notes
```

---

## Docs

- [Server setup](Docs/server-setup.md) — get the agent running on your server
- [Roadmap](Docs/roadmap.md) — web client, GitHub integration, CLI as a portable deploy tool
- [Notes](Docs/notes.md) — things learned while building this
