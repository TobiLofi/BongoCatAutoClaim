# Architecture

The project deliberately separates user interface code from game-specific
patching and compatibility data.

## Components

- `BongoCatAutoClaim.App` is a small WinForms front end. It displays detection
  and hash status and exposes only **Install Auto Claim** and **Restore
  Original** mutations.
- `BongoCatAutoClaim.Core` owns Steam discovery, SHA-256 validation, backups,
  restoration, diagnostics, and the IL patch engine.
- `compatibility/*.json` contains the allowlist of manually audited Steam
  builds. These files are embedded in the compiled application.
- `BongoCatAutoClaim.Tests` validates the catalog without proprietary files.
  A developer may optionally pass a locally owned pristine assembly to verify
  exact reproduction of an accepted patched hash in a temporary directory.

## Safety boundaries

The application has no network or telemetry code. It never downloads DLLs.
It refuses to patch unless the Steam build (when available), original SHA-256,
module MVID, member signatures, and exact IL anchor all match an allowlisted
definition. Patched output is reopened to validate branch targets and must
match the runtime-accepted SHA-256 before installation.

A pristine backup is created under the portable application's `Backups`
directory before modification. Installation and restoration are refused while
`BongoCat.exe` is running. Candidate files are produced beside the target and
replaced on the same volume only after complete verification.

## Accepted patch definition

`shop-timer-ready-buy-v1` adds one `ShopItem.Buy()` call immediately after the
one-time `ChestIsReady` transition and `OnChestReady()` call in the generated
`Shop.TimerUpdate()` iterator. Both current shop instances share this path.
The game's existing `ChestExchanger` static lock serializes simultaneous
normal and emote exchanges.

The inserted IL makes one pre-existing short timer guard jump exceed its legal
range, so that branch is widened from `bgt.s` to the equivalent `bgt`. The
30-minute timer, one-second yield, click handling, Steam exchange definitions,
and retry behavior are unchanged.
