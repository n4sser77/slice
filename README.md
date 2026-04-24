# slice

Deploy .NET apps to your own machine without touching systemd, scp, or ssh manually. Think Vercel but for .NET — and you own the server.

Built for a Raspberry Pi 5 but works on any Linux machine.

---

## What it does

You zip your app, run one command, and it shows up as a running systemd service with its own port. That's it.

```
dotnet run --project Agent.Cli -- deploy MyApp
```

---

## How it works

There are two parts:

- **Agent** — a minimal API that runs on your server. It receives deployments, writes systemd service files, and manages the lifecycle of your apps.
- **CLI** — what you run from your machine to talk to the agent.

---

## Requirements

- Linux (or WSL if you're on Windows — it needs systemd)
- .NET 10 SDK
- `dotnet` available at `/usr/bin/dotnet` — if you're using mise or a version manager, symlink it:

```bash
sudo ln -s /home/$USER/.local/share/mise/dotnet-root/dotnet /usr/bin/dotnet
```

---

## Quickstart

**1. Start the agent**

```bash
dotnet run --project Agent
```

It runs on `http://localhost:5165` by default.

**2. Deploy an app**

```bash
dotnet run --project Agent.Cli -- deploy MyApp
```

Point it at a project folder. The CLI builds it, zips it, and uploads it to the agent.

**3. Check what's running**

```bash
dotnet run --project Agent.Cli -- list
```

---

## Run the tests

```bash
dotnet test
```

---

## Notes

Things learned while building this — systemd internals, process management in C#, testing strategies: [notes.md](notes.md)

---

## Project structure

```
Agent/        the server — minimal API, manages systemd services
Agent.Cli/    the CLI — deploy, list, inspect
Common/       shared models between agent and CLI
Agent.Tests/
Agent.Cli.Tests/
```
