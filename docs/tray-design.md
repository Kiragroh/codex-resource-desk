# Tray and automatic discovery — 0.1.2

The user requested background monitoring in the Windows notification area and clarification of automatic discovery. Existing monitoring rereads the selected global configuration and process inventory every five seconds. Entries are discovered from the TOML model; no integration allowlist restricts discovery. Project and plugin configuration remain outside the supported inventory.

Use a Windows NotifyIcon with the existing logo. Closing the normal window hides it and preserves its monitor timer and in-memory undo. The tray menu opens the window, refreshes, pauses/resumes monitoring, and exits explicitly. The tooltip distinguishes live, paused and stale data. The app does not create a Windows startup entry. `--tray` starts hidden; `--no-tray` retains exit-on-close behavior. Screenshots and checks exit normally without leaving a tray process.

Normal launches for the same user and desktop session share one application instance. Re-launch signals the existing window to open; select another configuration in Settings when it is already running. Screenshot/demo fixtures are independent. All tray UI actions are dispatched to WPF; Exit refuses while a configuration/process action is in flight, rather than interrupting it. Icon and wait handles are disposed on exit.

Verification: build and existing fixtures; native tray close/hide/show, pause/resume, background timer and disposal checks; add/disable/remove an MCP entry in a disposable config and observe automatic refresh; verify a second normal launch exits and activates the original. Preserve all live integration settings. Package, update desktop shortcut, publish 0.1.2 preview.
