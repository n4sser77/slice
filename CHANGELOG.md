# Changelog

All notable changes to Slice will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Slice does not have a stable release series yet, so current development is
recorded under **Unreleased**.

## [Unreleased]

### Added

- CLI commands to deploy, list, inspect, stop, and remove services.
- Agent endpoints for deploying and managing user-level systemd services.
- Optional public HTTPS routing through the Caddy Admin API.
- Service status reporting, including state, process, memory, and CPU data.
- Port allocation and release for deployed services.
- Scalar API reference in development environments.
- Architecture, roadmap, server setup, and deployment target documentation.
- CI helper for updating and deploying the Slice agent.

### Changed

- Reorganized the roadmap around production blockers, seamless onboarding,
  and prioritized GitHub Project backlog items.
- Deployment packages now preserve nested publish output, enabling Blazor
  static assets, `wwwroot`, and application configuration files to deploy.
- The CLI deployment target can be configured with
  `SLICE_AGENT_TARGET_HOST`; it defaults to `linux-arm64`.
- Service removal now deletes its systemd unit and application files, releases
  its port, reloads systemd state, and removes its reverse-proxy route.
- Service discovery filters stale and non-Slice systemd entries.

### Fixed

- Corrected Caddy route serialization and omitted null route/upstream fields.
- Corrected target-host configuration used by CLI publishing.
- Cleared stale systemd entries after removing a service.
- Rolled back service creation when reverse-proxy registration fails.
- Improved CLI error handling for failed, invalid, timed-out, and cancelled
  requests.
- Preserved relative file paths when creating and extracting deployment ZIPs.

### Security

- Reject ZIP entries that attempt to extract outside the deployment directory.
- Sanitize uploaded application and DLL names before using them in paths or
  systemd unit files.
- Pass dynamic `systemctl` values through `ProcessStartInfo.ArgumentList`
  instead of interpolated argument strings.
- Restrict service management operations to validated `slice-*` unit names.
- Run deployed services with `NoNewPrivileges` and `PrivateTmp`.
