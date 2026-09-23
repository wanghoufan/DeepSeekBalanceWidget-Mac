# DeepSeek Balance Widget (macOS)

> English | [简体中文](README.md)

> ⚠️ **This repository maintains the macOS build only** (Avalonia / .NET 8). The Windows (WPF) app has been split into a **separate repository** and is no longer maintained here — please submit Windows-related changes to the Windows repo.
>
> DeepSeek balance & ChatGPT Plus usage monitor — a desktop widget for macOS 12+, built on .NET 8.

A macOS menu-bar widget that monitors your DeepSeek API balance and ChatGPT Plus usage. It supports balance polling, Plus remaining quota, launch-at-login, and abnormal-state alerts, with edge-snapping auto-hide, a mini capsule, and a menu-bar presence.

[![Release](https://img.shields.io/github/v/release/wanghoufan/DeepSeekBalanceWidget-Mac?display_name=tag)](https://github.com/wanghoufan/DeepSeekBalanceWidget-Mac/releases/latest)
[![Platform](https://img.shields.io/badge/platform-macOS-000000?logo=apple)](https://github.com/wanghoufan/DeepSeekBalanceWidget-Mac)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)

![DeepSeek balance monitor v0.5.0](artifacts/ui-audit/02-after.png)

## Download

Grab the macOS installer from [Releases](https://github.com/wanghoufan/DeepSeekBalanceWidget-Mac/releases/latest):

| File | Platform |
| --- | --- |
| `DeepSeekBalanceWidget-v0.6.0-macos-arm64.zip` | macOS Apple Silicon (M series) |
| `DeepSeekBalanceWidget-v0.6.0-macos-x64.zip` | macOS Intel |

**macOS**: unzip and drag `DeepSeekBalanceWidget.app` into the Applications folder — no .NET installation required.

> Need the Windows version? Head to the separate Windows repository (this repo ships no Windows builds).

> On first launch, enter your own DeepSeek API key in Settings. On macOS the key is stored in the login Keychain and never uploaded to GitHub.

## Usage

The macOS build is a standalone native `.app` menu-bar application. It supports both Apple Silicon and Intel Macs; API keys are stored in the current user's macOS login Keychain.

If Gatekeeper blocks the unsigned app on first launch, Control-click the app in Finder and choose "Open".

"Launch at login" (Settings) writes `~/Library/LaunchAgents/com.deepseekbalancewidget.plist` for the current user. ChatGPT Plus usage is read from the local `~/.cc-switch/codex_oauth_auth.json`.

### Building on a Mac

On a Mac with the .NET 8 SDK installed:

```bash
chmod +x scripts/publish-macos.sh
./scripts/publish-macos.sh arm64   # Apple Silicon (M series)
# or ./scripts/publish-macos.sh x64  # Intel Mac
open release/macos-arm64/DeepSeekBalanceWidget.app
```

The generated app is self-contained — end users do not need .NET installed (the bundle includes the runtime, so it is fairly large).

To register the app in Launchpad like a regular app, install it into the per-user Applications folder:

```bash
bash scripts/install-macos.sh arm64
```

The script opens the app so it registers with Launchpad; afterwards you can start it from Launchpad or Finder → Applications. An existing same-name install is moved to a timestamped backup.

## Features

- Real-time display of DeepSeek API total balance, top-up balance, and granted balance, with amount/percentage change vs. the last successful refresh
- **ChatGPT Plus usage** from the local Codex login: an aligned table showing both accounts' 5-hour rolling window and weekly window remaining quota, with reset countdowns (column-aligned since v0.4.0)
- **OpenCode Go quota** via the official usage endpoint: 5-hour / weekly / monthly remaining percentages, reset countdown, and progress bars; missing key / invalid key / network errors are shown directly on the block (replaces the former WorkBuddy placeholder). Since v0.6.0 it supports **two accounts**: a second API key configured separately, side-by-side OC1/OC2 mini cards in the capsule, and grouped detail cards
- Balance & quota alerts: persistent alert window with looping siren when DeepSeek balance or ChatGPT / OpenCode quota drops below configured thresholds (default 20% / 10%), plus a recovery notification when quota returns; each threshold reminds once per cycle
- **Mini capsule, single-row-wide layout**: DeepSeek balance (with change badge) | GPT dual-account four-column alignment | OpenCode Go three-window progress bars | edge-snap / pin / minimize / close buttons vertically centered at the far right (refresh time removed from the capsule; kept in the expanded card)
- **Customizable capsule block order** (v0.4.0): reorder DeepSeek / ChatGPT / OpenCode / WorkBuddy blocks via move up/down in Settings; takes effect on save
- Low-balance and abnormal-drop alerts with a cooldown to avoid repeated interruptions
- Full card and mini capsule modes, freely draggable with remembered position, optional edge-snapping auto-hide
- Live menu-bar display of balance, Plus usage percentage (with the 5-hour reset countdown), OpenCode Go quota (a standalone OC2 item when a second key is configured), and peak/off-peak indicator
- Menu-bar status, pin-on-top, hide, launch at login; official peak hours shown in Beijing time
- API key stored in the macOS login Keychain — never in plaintext, never uploaded

## v0.6.0 Highlights

- **OpenCode Go dual-account monitoring**: configure a second API key in Settings (separate Keychain item), side-by-side OC1/OC2 mini cards in the capsule, detail cards grouped per account, a standalone OC2 menu-bar item, and independent alerts for account 2
- **Richer menu bar**: the GPT segment now carries the 5-hour reset countdown (`GPT 65/79% 4h24m`) and the OC label becomes `OC1`; the capsule monthly row shows days until reset
- **GPT alert event log**: low-quota and recovery alerts are persisted the moment they are evaluated, so you can still tell whether a reminder fired after dismissing the window
- **Native Codex credential refresh**: reads Codex's own `auth.json` accounts in addition to CC Switch storage, refreshing expired OAuth tokens and writing both stores atomically
- **Stability**: menu-bar status items are now created once at launch and toggled with `setVisible:` (fixes the whole menu bar going blank), removed on SIGTERM (fixes frozen, unclickable leftovers), Keychain writes roll back so an existing key survives a failed save, settings save moved off the UI thread with a 15 s timeout, negative balances from an overdue account are no longer rejected, and revoked OpenCode accounts no longer occupy the menu bar

**In development (unreleased)**: real WorkBuddy quota integration (still a placeholder) — see the "Unreleased" section of [CHANGELOG.md](CHANGELOG.md).

See [CHANGELOG.md](CHANGELOG.md) for the full changelog.

<details>
<summary>Current UI (v0.5.0)</summary>

| Mini capsule (single-row-wide layout) | Expanded card |
| --- | --- |
| <img src="artifacts/ui-audit/01-before.png" width="480"> | <img src="artifacts/ui-audit/02-after.png" width="280"> |

</details>

Full changes: [CHANGELOG.md](CHANGELOG.md).

## Daily Launch

For daily use, start `DeepSeekBalanceWidget.app` from Launchpad or Applications. Do not launch from `src/.../bin/Debug/...` — that folder is a dev-build cache whose paths and files change with every compile.

## Release

Run `scripts/publish-macos.sh` on macOS to produce the `.app` bundle (see "Building on a Mac" above). Artifacts:

```text
release/macos-arm64/DeepSeekBalanceWidget.app
release/macos-x64/DeepSeekBalanceWidget.app
```

The self-contained build needs no .NET Runtime on the target machine, so it is noticeably larger than the apphost in the Debug folder — this is expected. `bash scripts/install-macos.sh arm64` packages and installs into the per-user Applications folder `~/Applications` in one step (no admin rights needed; the app shows up in Launchpad immediately).

Pushing a `v*` tag triggers GitHub Actions to build the macOS (arm64 / x64) installers and publish them to Releases.

> Windows builds and releases happen in the separate Windows repository, not here.

## Development

Requirements:

- macOS 12+ (Apple Silicon or Intel)
- .NET 8 SDK
- Rider or VS Code (optional)

Build and run the macOS project:

```bash
dotnet build src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj
dotnet run --project src/DeepSeekBalanceWidget.Mac/DeepSeekBalanceWidget.Mac.csproj
```

> The solution `DeepSeekBalanceWidget.sln` still references the Windows (WPF) project and fails to build on macOS (no WindowsDesktop SDK). On a Mac, build the `.Mac` project directly; do not build the whole sln.

## Project Structure

```text
.
├─ src/
│  ├─ DeepSeekBalanceWidget/           Windows WPF app (moved to the separate Windows repo, unmaintained here)
│  └─ DeepSeekBalanceWidget.Mac/       macOS Avalonia app (the only maintained target in this repo)
├─ tests/                  Automated tests
├─ artifacts/ui-audit/     UI before/after screenshots
├─ scripts/                Build & release scripts
│  ├─ governance/          Governance entry & fail-closed interface templates
│  ├─ runtime/macos/       macOS runtime adapters & audit entry
│  └─ workers/             Worker startup interface templates
├─ docs/                   Project governance, progress, QA, review, runtime & handover records
│  ├─ governance/          Origin/Applied, permissions, state machines, migration audits
│  ├─ progress/            Current stage, authoritative state, evidence, user decisions
│  ├─ runtime/             Runtime health & run registry
│  └─ roles/               The eight fixed governance roles
├─ skills/                 Skill candidate registry
├─ tests/governance/       Governance contract tests (auto-discovers tests/governance/test-*)
├─ release/                Local release artifacts, not committed to Git
├─ DeepSeekBalanceWidget.sln
└─ README.md
```

## Governance Template

This project has been migrated to the ORCA V2.3.1 / Delivery V1.10 structure of `01_治理模板/AI-Governance-Template`. The template source is kept in that directory; the project's current adoption state is recorded in `docs/governance/PROJECT_GOVERNANCE_APPLIED.yaml`, with the immutable origin in `docs/governance/PROJECT_GOVERNANCE_ORIGIN.yaml`.

Governance scripts fail closed by default; the existence of a script does not mean the local ORCA, OpenCode, Codex, Computer Use, or release pipeline has been verified end-to-end. See `docs/governance/PROJECT_MIGRATION_AUDIT.md` for project-specific migration conclusions.

## Configuration & Security

Regular settings live in:

```text
~/Library/Application Support/DeepSeekBalanceWidget/config.json
```

API keys are not written to that file — they go into the macOS login Keychain. The config file, API keys, and local release artifacts are never committed to GitHub.

This project never writes API keys into source code, logs, or GitHub Actions. Please do not commit or share your `config.json`.

## Launch at Login

After enabling "Launch at login" in Settings, macOS writes `~/Library/LaunchAgents/com.deepseekbalancewidget.plist`, recording the currently running app path. Install/run the `DeepSeekBalanceWidget.app` from the Applications folder first, then enable the option — this avoids the plist pointing at a Debug build directory.

## Documentation

Version history: [CHANGELOG.md](CHANGELOG.md).
