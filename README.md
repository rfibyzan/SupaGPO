# SupaGPO

Clean-room rebuild of Grand Piece Online (Roblox) fishing automation, heritage-documented from REA of
`Grand Piece.exe` (Astro Launcher Macros/grand-piece). C# / .NET 8 / Avalonia.

External reader — no injection: attach to RobloxPlayerBeta by PID → OpenProcess (QUERY_LIMITED|QUERY|VM_READ)
→ resolve module base + DataModel via offsets → poll state → act + Discord webhook.

## Prerequisites

* .NET 8 SDK — per-user install at `D:\Tools\dotnet\dotnet.exe` (8.0.426). Prepend to PATH per session:
  `$env:Path = "D:\Tools\dotnet;" + $env:Path`

## Build

```powershell
$env:Path = "D:\Tools\dotnet;" + $env:Path
dotnet build SupaGPO.sln
```

## Layout

* `src/SupaGPO.Core` — memory core, fishing loop, input, webhooks, settings (net8.0-windows)
* `src/SupaGPO.Presentation` — Avalonia 11 shell; design system Of* (port SupaFisch/SupaZero):
  tab strip atas (Overview, Fishing, Loot, Overlay, Hotkeys, Webhooks, Offsets),
  strip status koneksi, status realtime via timer 250 ms
* `src/SupaGPO.Core.Tests` — xunit tests (RawMemory, OffsetsSnapshot, FishingLoop dry-run, Watchdog, Webhook)

## Features (from REA of Grand Piece.exe)

* Fishing: read reel flag, bait, fruits, spawn notices; cast hold/release (Normal + Pulse)
* Loot: AutoOpenChests, AutoStoreChestFruits
* Input: SendInput/mouse_event/SetCursorPos/GetAsyncKeyState; global hotkey Start/Stop
* Overlay: running status overlay (reel, bait, cast state)
* Webhooks: Discord embeds (FishingStarted/Stopped, Catch), user-configured URL, Test button
* Offsets: offsets.json loader + update service (validate-then-replace); supports the
  provider wrapper format (offsets as nested JSON string + "Roblox Version"), Roblox
  version check with UI warning, and offsets-driven flat reads (module base + offset key)
  plus tree-walk fallback (SupaZero-style: GUI objects found by name, values read via
  real dump offsets GuiObject.Visible/Text; camera via Workspace.CurrentCamera)
  as primary sensor path per REA findings
* Visual reel: pixel-based reel detection (template matching on a screen region,
  PerfectVisualReelController-style) with calibrate/test UI on the Fishing page

## Guardrails

* No license/entitlement gate or short-URL activation (deliberately not ported from Grand Piece.exe).
* No anti-cheat bypass, no bypass of Roblox protections, no hardcoded third-party channel links.
* "Roblox absent" is a normal state — never crashes on startup.
* Offsets live files are git-ignored; only samples committed.
