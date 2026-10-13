# Resource Desk 0.1.2 — tray preview

Resource Desk now stays in the Windows notification area when you close its window. The same local five-second monitor continues to read the selected global MCP configuration and process inventory.

- Double-click the icon to reopen; right-click for Open, Refresh, Pause/Resume and Exit.
- Hover for Codex RAM, process count and live/paused/stale status.
- Start hidden with `--tray`, or use `--no-tray` for exit-on-close behavior.
- Reopening a normal instance activates the existing window for the same Windows user/session.
- Global MCP entries added, disabled or removed while the app is running appear on the next automatic refresh. No automatic process shutdown or Windows autostart is introduced.
- Shared helpers and project/plugin discovery limitations are unchanged.

Download the ZIP, extract the complete folder, and open ResourceDesk.exe. This is an unsigned, self-contained Windows x64 early preview. Close older versions before starting the new one.

Validated with 30 safety fixture groups, seven demo UI checks and nine native lifecycle/discovery checks using a disposable config. The background add/disable/remove test changes no live integration settings.
