# Preview verification — 13 October 2026

## 0.1.2 tray update

30 safety fixture groups, seven native UI checks and nine lifecycle/discovery checks passed on the self-contained build. Lifecycle checks closed the window to the tray, observed new/disabled/removed entries from an app-created disposable TOML file using the real five-second timer, verified pause/resume and reopening, then exited and checked icon disposal. No live integration configuration was changed. All copied network release payloads are SHA-256 verified before activation. No Windows startup entry is created.

## 0.1.1 logo update

GPT Image logo embedded in UI, window and executable; seven ICO resolutions exported. Extracted executable icon and native demo screenshot visually checked. Release build and the same 30 fixture groups plus seven UI checks passed again. All 412 ZIP payload files verified against their SHA-256 manifest. Integration controls are unchanged.

Version 0.1.0, Windows x64, .NET 10.0.8 self-contained runtime.

- Release build: zero warnings, zero errors.
- 30 safety fixture groups passed: 17 config groups and 13 process groups.
- Seven native WPF checks passed with simulated data: disabled entries retained; mutations blocked in demo; physical-memory percentage; selection under disabled/running filters; empty search; read-only config-state fidelity.
- Self-contained executable rendered demo and live views, exited with code 0, and left the live config file hash unchanged.
- Actual live sample distinguished disabled Hostinger processes from enabled Unity. Private resident RAM and private committed memory were reported separately. Native memory counters were sanity-checked against the executing process.
- Two source reviews completed: specification review, then independent quality/safety review. Findings on asynchronous settings state, filtered selection, stale indicator, percentage scaling, read-only state and recovery races were corrected and re-reviewed.
- Screenshots inspected after the blue/purple theme change. Public illustration uses labelled simulated data.

No live integration was disabled or stopped while building/testing this preview. The user's first Unity switch and subsequent Codex reload behavior remain user acceptance tests. No claims of cross-machine compatibility or comprehensive project/plugin inventory have been validated. Real usage varies as other Codex work starts and stops.

An external writer that atomically replaces the config at the same instant remains a concurrency boundary: the displaced bytes are retained, a conflict is reported and no automatic recovery replacement is attempted.
