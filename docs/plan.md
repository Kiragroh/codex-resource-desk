# Resource Desk Implementation Plan

**Goal:** deliver a tested portable English Windows resource controller, ready for the user's Unity test.
**Architecture:** Core owns TOML edits, process identity and resource sampling; WPF owns the view and explicit actions. Configuration state and runtime state remain independent.
**Tech stack:** .NET 10, WPF, Tomlyn 0.19 (TOML 1.0), System.Management 10.0, native Windows memory APIs.

- [x] Configuration: ConfigStore.Load(path), SetEnabled(path, keys, value, expectedHash), Undo(change) with fail-closed parse/concurrency/semantic validation and byte-exact backup. Fixture tests for comments, quoted names, existing and absent flags, hostile multiline headers, invalid TOML and concurrent writes.
- [x] Sampling: WindowsSampler.Capture() reads process identities and physical/private-resident/commit metrics. ProcessAttribution.Build(snapshot, config) assigns each proven Codex descendant once to matching local MCP, browser/runtime or core; configured zero-process servers stay visible. Hostinger family aggregates nine keys. ProcessController.Stop(component) checks process identity and stops only selected owned processes. Fixture tests for ancestry, collisions, PID reuse and unowned processes.
- [x] UI: App.xaml, MainWindow.xaml, MainWindow.xaml.cs and ViewModels.cs implement asynchronous five-second refresh, system and Codex totals, sparkline/stacked breakdown, component list, hover details, PID inspector, enable/disable confirmation, separate stop, guarded undo, config picker and sanitized export. Disabled servers remain discoverable. Built-in controls remain read-only.
- [x] Verification: run `dotnet run --project tests/ResourceDesk.Tests`; build and launch native app; create screenshot through the app's explicit preview mode and inspect it; validate fixture toggles and live Unity/Hostinger preservation. Review implementation for config and process safety.
- [x] Package: publish self-contained Windows x64 folder, zip with README, licenses, checksums and known limitations. Keep source portable and free of personal paths. Provide local executable immediately for user testing. Do not publish remotely yet.
