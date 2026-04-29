# Roadmap

Priority order matters here. Nothing in phase N starts until phase N-1 is solid.

---

## Phase 1 — Complete the CLI and Agent (current focus)

The CLI is the main workflow. Deploy and list are implemented. What's missing:

- `slice start <app>` — start a stopped service
- `slice stop <app>` — stop a running service
- `slice restart <app>` — restart a service
- `slice status <app>` — partially implemented, needs to be complete and reliable

Once these are in, the CLI covers the full lifecycle of a deployed app. That's the vertical slice — ship code, see it running, manage it, all without touching the server manually.

**Refactor after every feature.** Not as a separate pass at the end — after each command is working. This is how the project stays maintainable and doesn't turn into the kind of codebase that burns you out.

**Feature-based organization and vertical slice architecture** will be introduced as the CLI and agent grow. Each feature (deploy, list, status, etc.) owns its slice top to bottom — command → HTTP → agent handler → systemd. No horizontal layers that force you to touch five files for one change.

When the CLI is complete and stable, it works in any CI pipeline as-is. Point it at the agent, set the URL, deploy. No git integration needed for that.

---

## Phase 2 — Web client (read-only first)

A dashboard for viewing deployed services — status, logs, CPU, memory per app.

Hosted inside the Agent (same process, same project). Either Razor pages or Blazor served as static files through the minimal API. No separate deployment, no extra infrastructure.

Read-only to start. Once that's solid and stable, add operational support: stop, start, restart. Same operations the CLI has, nothing more.

Deployment stays CLI-only until the web client is proven. The web client is not a replacement for the CLI — it's an inspection tool.

AOT will likely need to be disabled for this. See notes on AOT.

---

## Phase 3 — Git integration

Last, and only after the web client is working.

The plan: a `/deploy` webhook endpoint in the Agent that a GitHub Actions job hits after a push to main. The endpoint validates the request, pulls the latest code, builds, publishes, and restarts the service.

Before building this in slice, I'll build a minimal version of it for slice itself — a tiny .NET minimal API that handles deploys triggered by GitHub Actions CI. This avoids opening SSH ports on the Raspberry Pi and keeps the attack surface small. The agent is the only thing exposed. Whatever I learn from that will feed directly into the implementation here.

**Phase 1 of git integration** — slice deploys itself via a webhook. Lays the groundwork.

**Phase 2** — extend it to user-deployed apps. Connect a repo, push to main, the agent picks it up and deploys automatically.

The manual `slice deploy` stays as an alternative. Useful for one-off deploys and CI pipelines that don't want git integration.

---

## Security

Security is a hard requirement before any of this goes near production or gets exposed to the internet. Things to figure out and implement:

### Must be in place before Phase 2 ships

These are blockers. The web client expands the attack surface — anyone with a browser could hit the agent. None of Phase 2 goes public without these done.

- **Authentication** — the agent needs to verify that requests come from a trusted source. Simple shared token or API key stored in the agent, passed by the client on every request. The CLI reads it from an env var or config file. Nothing hits the agent without it.
- **HTTPS** — the agent should run behind a reverse proxy with TLS before being exposed publicly. Tokens over plain HTTP are not tokens, they're usernames.
- **Input validation on service names** — every endpoint that accepts a `serviceName` must reject anything that doesn't match the `slice-*` pattern. Right now the stop and status endpoints pass the name straight to `systemctl`, which means a caller can operate on any user unit on the machine, not just deployed apps. A 400 with a clear message is enough.

### Important but not Phase 2 blockers

- **Token storage** — tokens on the server side stored securely, not in plaintext config if avoidable. Clients store theirs in env vars (easy to inject in CI, easy to keep out of version control).
- **Rate limiting** — the deploy endpoint accepts large file uploads and shouldn't be hammerable. ASP.NET has built-in rate limiting middleware. A sliding window on the upload endpoint is enough to start.
- **Least privilege** — the agent runs as a user-level systemd service, not root. Keep it that way. Review what permissions it actually needs and cut anything extra.
- **Audit log** — know what was deployed, by whom (which token), and when. Even a simple append-only log file is better than nothing.

### Phase 3 only

- **Request validation for webhooks** — GitHub sends a signature with every webhook payload. Verify it before doing anything. Standard HMAC-SHA256 pattern.

Still need to think through what key/token model fits best for the two different clients (CLI and GitHub Actions). Will revisit this before Phase 3.

---

## Reverse proxy and accessibility

Deploying an app is only half the job. If it's not reachable, it might as well not be running.

Right now the agent assigns a port and the app listens on it. Getting to it from outside requires knowing the port, having it open, and hitting the IP directly. That's fine for local testing but not for anything real.

### Short-term — Caddy (requires Docker)

The simplest path to subdomain routing without touching DNS complexity: require Caddy running on the server (via Docker) and have the agent inject routes into Caddy's config dynamically.

How it would work:
- During `slice deploy`, pass a `--domain` flag (e.g. `--domain myapp.example.com`) or a subdomain flag that gets appended to a configured host domain
- The agent registers the route in Caddy via its admin API — no file editing, no restarts
- Caddy handles TLS automatically via Let's Encrypt

A `slice set domain <app> <domain>` command would let you update the domain for an already-deployed app.

This is the plan for the staging environment on the Raspberry Pi while the core deployment and management features are being built and tested. It's a workaround — takes a Docker dependency but works well enough to unblock real use.

### Long-term — YARP (fully embedded, no external dependencies)

The eventual goal: ship the reverse proxy as part of the agent, no Caddy, no Docker required.

YARP (Yet Another Reverse Proxy) is a .NET library — it can run inside the agent process or as a companion service. The idea:
- The agent manages YARP routing config the same way it manages systemd service files — programmatically, on deploy
- Automatic TLS handled by the agent itself, including cert renewal via a background service
- Install scripts to set up any required CLI tooling
- No external dependencies, no Docker, nothing to maintain separately

This is an early idea. It might not get built at all, or it might look completely different when it does. Adding it here to capture the thinking, not as a commitment.

---

## Considered but not started

- Git polling (background service that polls main and auto-deploys) — possible alternative to webhooks, simpler but less immediate
- Dashboard metrics over time (CPU/mem charts) — needs a time-series store, overkill for now
- Multi-server support — deploy to more than one agent from the same CLI config
