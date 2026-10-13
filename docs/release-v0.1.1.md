# Resource Desk 0.1.1 — early preview

See system memory, private Codex resident RAM, committed memory and attributed process counts. Inspect individual integrations, keep disabled entries visible, and control supported global local MCP servers from a native Windows app.

## Download

Download `ResourceDesk-0.1.1-win-x64.zip`, extract the **whole folder**, and open `ResourceDesk.exe`. Windows x64; bundled .NET 10 runtime; no installation or administrator access required. The executable is unsigned. The `.sha256` asset checks the ZIP; `SHA256SUMS.txt` inside checks its files.

## Controls and limits

- Switching off changes configuration and optionally stops verified integration processes.
- Switching on saves configuration. A new connection or Codex restart may be needed.
- Disabled configuration and running processes are tracked separately; old Codex hosts can retain settings.
- Core and shared helpers, remote entries and ambiguous matches remain read-only.
- Only global local MCP configuration is supported in this preview. Project overrides and plugin-managed processes are not comprehensively covered.

## Verification

30 config/process safety fixture groups and seven native UI checks passed. Live sampling, executable logo, portable startup and package checksums were checked. This remains an early Windows preview, not a broadly validated lifecycle manager.

MIT licensed. Independent of OpenAI. No telemetry. See README for privacy and configuration-backup details.
