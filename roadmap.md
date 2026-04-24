# Roadmap

Things I want to build, roughly in order of priority.

---

## Web client

A dashboard for viewing deployed services — status, logs, CPU, memory per app.

Hosting it inside the Agent (same project, same process) keeps things simple for now. Either Razor pages or Blazor served as static files through the minimal API. No separate deployment, no extra infrastructure.

Deployment itself stays CLI-only. The web client is read-only — inspect what's running, nothing more. If that changes later and the web grows too big, it can be split out then. Not before.

AOT might need to be disabled for this. See notes on AOT below.

---

## GitHub integration

### Phase 1 — CI/CD for Slice itself

A small webhook endpoint in the Agent that GitHub Actions calls after a push to main. The endpoint:
- Validates the request is actually from GitHub
- Runs `git pull`
- Runs `dotnet build` and `dotnet publish`
- Restarts the agent service

This simplifies my own workflow and lays the groundwork for the next phase.

### Phase 2 — GitHub integration for user apps

Same idea, extended to user-deployed apps. A user connects their repo, and the agent monitors for pushes to main, then pulls, builds, and publishes their app automatically.

The manual `slice deploy` stays as an alternative — useful for CI pipelines, one-off deploys, or users who don't want git integration.

---

## CLI as a portable deploy tool

The CLI should work as a standalone binary — either as a .NET global tool or a native AOT binary. The idea: run `slice deploy myapp` in any CI pipeline, point it at a server running the Agent, and the app gets deployed without SSH.

No need to open the SSH port on the server. No VPN. The Agent is the only thing exposed.

How it works:
- Configure the target server via env vars or a config file
- The CLI packages the app and sends it to the Agent's deploy endpoint
- The Agent handles the rest — service file, systemd, port assignment

This makes Slice useful beyond personal use. Anyone can drop the CLI into their pipeline, point it at their server, and have a simple deploy flow.

Security needs a proper review before this goes anywhere near production — authentication, request validation, rate limiting, making sure random people can't deploy to your server.

---

## Considered but not started

- Git background service (poll for changes on main and auto-deploy) — possible future alternative to the webhook approach
- Dashboard metrics (CPU/mem charts over time) — needs a time-series store, probably overkill for now
- Multi-server support — deploy to more than one agent from the same CLI config
