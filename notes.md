# Things I've Learned

## AOT vs JIT — considerations for this project

AOT (Ahead-of-Time compilation) produces a native binary with no runtime dependency and faster startup. Great in theory for a Raspberry Pi with 4GB RAM.

In practice it slowed me down a lot:
- No runtime reflection means JSON serialization needs source generators everywhere
- Third-party libraries often aren't AOT-compatible
- Razor/Blazor support in AOT is limited — makes adding a web client painful
- Workarounds pile up fast and obscure what the code is actually doing

JIT is fine for a Pi 5. The app is already small, startup time isn't critical, and memory footprint with JIT is still very reasonable. The tradeoff isn't worth it at this stage.

**Consideration:** Disable AOT for the Agent while the project is still moving fast. Revisit when the feature set is stable and the web client is built. AOT makes more sense as an optimization pass at the end, not a constraint throughout development.

## systemd
- Services start with a clean environment — shell env vars like `DOTNET_ROOT` are not inherited
- Every service gets its own cgroup, which is how memory and CPU are tracked per-service
- `systemctl show` with `--property=` is the machine-readable alternative to `systemctl status` — KEY=VALUE output, stable and easy to parse
- `MemoryCurrent` returns `ulong.MaxValue` (18446744073709551615) when memory accounting is unavailable — not an actual memory value
- `CPUUsageNSec` is cumulative nanoseconds since last start, not a percentage
- `WantedBy=default.target` for user services, `multi-user.target` is for system-level services
- `DOTNET_ROOT` set in a service `Environment=` line does NOT redirect framework lookup for the dotnet CLI — the CLI always looks relative to its own binary location

## Managing external processes in C#
- If you redirect a stream (stdout/stderr) you must read it — the OS pipe buffer (~4KB) fills up, the process blocks, and WaitForExitAsync deadlocks
- `ReadToEndAsync` is for bounded responses (process exits quickly with a fixed output)
- Event-based reading (`OutputDataReceived` + `BeginOutputReadLine`) is for streaming, long-running processes where you need output as it arrives
- `Process.Start` returns null if the process fails to start — always null-check

## .NET / C#
- `StringComparer.OrdinalIgnoreCase` on a dictionary makes key lookups case-insensitive — useful when parsing output from external tools where casing is inconsistent
- `InternalsVisibleTo` in the csproj lets a test project access `internal` classes without making them `public`
- `ulong.TryParse` + sentinel check is cleaner than catching overflow exceptions
- AOT (PublishAot) means no runtime reflection — source generators replace it (JsonSerializerContext, etc.)

## Testing
- Test setup duplication is a smell just like production code duplication — fix it with helpers
- The test body should read as: given this input → assert this output, not as infrastructure setup
- Fake bash scripts injected via constructor are a clean way to stub OS-level dependencies without mocking frameworks
- `IDisposable` + temp dirs with `Guid` names keep tests hermetic and parallel-safe
