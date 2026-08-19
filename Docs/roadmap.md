# Roadmap

Slice aims to make deploying a .NET application to a user-owned Linux server
feel as direct as deploying to a managed platform:

```text
install → configure → verify → deploy → receive a URL → manage the service
```

Priority is based on user value, production readiness, and the smallest
vertical slice that can be completed safely. GitHub Projects is the source of
truth for day-to-day status, priority, and size.

## Current product

The core deployment path works:

- The CLI publishes a .NET project for a configurable Linux runtime.
- Complete publish output is packaged, including Blazor static assets and
  nested `wwwroot` files.
- The Agent extracts the artifact, allocates a port, writes a user-level
  systemd service, and starts it.
- Caddy can register an optional public HTTPS route.
- The CLI can deploy, list, inspect, stop, and remove services.
- Removal cleans up the unit, application files, port allocation, and Caddy
  route.
- Development environments expose OpenAPI and a Scalar API reference.
- The Agent requires API-key authentication for deployment and management.
- Deployed applications receive persistent runtime configuration and secrets
  through a runtime-neutral Slice configuration snapshot.

The core is useful, but onboarding is still manual and Agent traffic still
needs a trusted network or external TLS termination.

## P0 — Production-ready deployments

These capabilities block realistic workloads or safe exposure. They take
priority over new surfaces such as a dashboard or Aspire integration.

### Secure transport — [#27](https://github.com/n4sser77/slice/issues/27)

Authentication credentials and deployment payloads must not cross an
untrusted network over plaintext HTTP. The initial supported approach is TLS
termination through Caddy. Embedded proxy work is deferred.

## P1 — Seamless first-use experience

These are ordered to build on each other in small, testable slices.

### 1. Health endpoint and `slice doctor` — [#41](https://github.com/n4sser77/slice/issues/41)

Add a non-sensitive Agent readiness endpoint and a CLI command that explains
whether configuration, connectivity, systemd, the .NET runtime, and the reverse
proxy are ready. This becomes the diagnostic foundation for initialization and
CI.

### 2. Persistent CLI configuration — [#42](https://github.com/n4sser77/slice/issues/42)

Load settings from `~/.config/slice/config.json` while retaining environment
variables for CI. Precedence is:

```text
CLI option → environment variable → config file → default
```

### 3. Guided client initialization — [#43](https://github.com/n4sser77/slice/issues/43)

```bash
slice init client
```

Prompt for the Agent URL, verify it through the health endpoint, determine the
target runtime, and save configuration. Authentication setup joins this flow
once #25 lands.

### 4. Small security wins

- Return generic API failures while logging internal details server-side
  ([#29](https://github.com/n4sser77/slice/issues/29)).
- Rate-limit expensive deployment uploads
  ([#30](https://github.com/n4sser77/slice/issues/30)).

Both are deliberately small changes with high defensive value.

### 5. Install without cloning — [#45](https://github.com/n4sser77/slice/issues/45)

Publish the CLI as a .NET global tool and provide versioned `linux-arm64` and
`linux-x64` Agent artifacts with checksums. A seamless setup cannot require
every user to clone and build the repository.

## P2 — Complete and automate the workflow

### Service lifecycle

- `slice restart <app>` — [#10](https://github.com/n4sser77/slice/issues/10)
- `slice start <app>` — [#46](https://github.com/n4sser77/slice/issues/46)

Together with deploy, list, status, stop, and remove, these complete the basic
service lifecycle.

### Local server initialization — [#44](https://github.com/n4sser77/slice/issues/44)

The first server initializer will run locally on the target:

```bash
slice init server
```

It will verify prerequisites, install a released Agent artifact, write and
enable the user-level systemd unit, configure authentication, start the Agent,
and run its health check.

Remote SSH orchestration is intentionally deferred. Proving an idempotent local
initializer is smaller, safer, and easier to troubleshoot.

The initializer will not install operating-system packages, change firewalls,
configure DNS, or act as a general provisioning tool.

### Reliability and capacity

- Expand consistent CLI and ProcessManager coverage
  ([#13](https://github.com/n4sser77/slice/issues/13)).
- Block deployments when the server is under unsafe resource pressure
  ([#32](https://github.com/n4sser77/slice/issues/32)).

## Later product phases

### Read-only web dashboard

Host a small dashboard inside the Agent for service state, CPU, memory, and
logs. Operational actions can follow after the read-only surface is stable.
Deployment remains CLI-first.

Authentication, TLS, safe error handling, and rate limiting must be complete
before this surface is exposed.

### Git integration

Keep `slice deploy` as the portable primitive used by CI. Later, add a
signature-validated webhook flow for deployments triggered by repository
updates. Git integration should orchestrate the existing deployment path rather
than create a second one.

### .NET Aspire applications — [#40](https://github.com/n4sser77/slice/issues/40)

Use the Aspire deployment manifest as the boundary between AppHost evaluation
and Slice. The first milestone supports .NET project resources and maps them to
grouped systemd services. Containers and cloud resources remain later,
explicit extensions.

### Embedded reverse proxy

Caddy is the supported short-term proxy. YARP remains a possible long-term
direction, but certificate issuance, renewal, persistence, and failure recovery
make it a separate product effort rather than a current dependency.

## Security status

Completed hardening includes:

- Slice-managed service-name validation.
- Safe `systemctl` argument boundaries.
- Uploaded DLL-name sanitization.
- ZIP traversal rejection.
- Port and reverse-proxy cleanup during removal.
- `NoNewPrivileges` and `PrivateTmp` for deployed services.
- API-key authentication with fixed-time comparison.
- Application configuration validation, private atomic storage, and systemd
  runtime materialization.

Still required before production exposure:

- TLS for Agent traffic (#27).
- Generic external errors with structured internal logging (#29).
- Deployment rate limiting (#30).

## Product principles

- Build vertical slices: CLI → HTTP contract → Agent → systemd → tests.
- Prefer a small complete workflow over a large partially connected feature.
- Keep manual `slice deploy` useful even after init, web, Git, and Aspire work.
- Preserve environment variables for CI even when interactive configuration is
  introduced.
- Never make insecure public exposure the easy default.
- Update documentation and the changelog in the same commit as user-visible
  behavior.
