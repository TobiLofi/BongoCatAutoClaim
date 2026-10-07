# Changelog

## 1.1.1 - 2026-10-07

- Added support for Bongo Cat Steam build 25723683 while retaining support for
  builds 25562987 and 25659569.
- Made no Auto Claim behavior change; normal and emote timers remain independent.
- Preserved the normal cooldown and strict unknown-build compatibility gating.

## 1.1.0 - 2026-10-04

- Added a manual **Check for Updates** button using the official GitHub release
  API and semantic version comparison.
- Added clear current, update-available, and offline/error messages.
- Opens only the official release page after user confirmation; no automatic
  download or self-update is performed.
- Added no telemetry or background network activity.
- Made no compatibility, timer, or Auto Claim patch behavior changes.

## 1.0.1 - 2026-10-03

- Added validated support for Bongo Cat Steam build 25659569 while retaining
  support for build 25562987.
- Preserved the normal 30-minute cooldown, manual click/tap behavior, and Steam
  inventory logic.
- Kept strict build, hash, module-identity, and IL-layout compatibility gating.
- Completed runtime acceptance on build 25659569, including already-ready
  rewards and a later natural timer completion.

## 1.0.0 - 2026-09-29

- Initial portable Windows GUI.
- Detects supported Steam installations or accepts a selected game folder.
- Installs the validated automatic normal/emote chest claim patch.
- Creates and verifies a pristine local backup before modification.
- Restores the pristine assembly through the GUI.
- Rejects unknown game builds, hashes, module identities, and IL layouts.
