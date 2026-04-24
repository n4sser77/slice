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

- **Authentication** — the agent needs to verify that requests come from a trusted source. Simple shared token or API key stored in the agent, passed by the client on every request. The CLI reads it from an env var or config file. Nothing hits the agent without it.
- **Token storage** — tokens on the server side stored securely, not in plaintext config if avoidable. Clients store theirs in env vars (easy to inject in CI, easy to keep out of version control).
- **HTTPS** — the agent should run behind a reverse proxy with TLS before being exposed publicly. Tokens over plain HTTP are not tokens, they're usernames.
- **Request validation for webhooks** — GitHub sends a signature with every webhook payload. Verify it before doing anything. Standard HMAC-SHA256 pattern.
- **Rate limiting** — the deploy endpoint shouldn't be hammerable. Basic rate limiting on the agent.
- **Least privilege** — the agent runs as a user-level systemd service, not root. Keep it that way. Review what permissions it actually needs and cut anything extra.
- **Audit log** — know what was deployed, by whom (which token), and when. Even a simple append-only log file is better than nothing.

Still need to think through what key/token model fits best for the two different clients (CLI and GitHub Actions). Will revisit this before Phase 3.

---

## Considered but not started

- Git polling (background service that polls main and auto-deploys) — possible alternative to webhooks, simpler but less immediate
- Dashboard metrics over time (CPU/mem charts) — needs a time-series store, overkill for now
- Multi-server support — deploy to more than one agent from the same CLI config
