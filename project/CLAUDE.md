# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A virtual aquarium idle/clicker game built with Godot 4.6 and C# (.NET 8). Players buy fish, watch them grow and breed, collect income, and discover mutations and hybrids. UI is Russian-language.

## Build & Run

This is a Godot project — there are no shell scripts or Makefiles. Use the Godot editor or CLI:

```
# Open in editor
godot --path C:\godotTest\hz_potom_pridumaem\project

# Run directly
godot --path C:\godotTest\hz_potom_pridumaem\project --main-scene aquarium.tscn

# Build C# only (from project root)
dotnet build test.csproj
```

The `.csproj` is `test.csproj` using `Godot.NET.Sdk 4.6.1` targeting `net8.0`. There are no automated tests.

## Architecture

### Singletons / Autoloads

**`GameManager.cs`** is the central autoload singleton (registered in `project.godot`). It owns:
- All economy state: money, fish list, breeding cooldowns, discovered species, owned shop items
- Periodic ticking: breeding check every 0.5s, mutation evaluation every 2s, autosave every 30s
- Save/load via `user://save.cfg` (Godot's `ConfigFile`)
- Fish lifecycle events (birth, growth stage completion, death, predation)
- Signals that UI panels connect to for reactive updates

### Fish Entity

**`Node2d.cs`** (`CharacterBody2D`) is the fish scene script (`node_2d.tscn`). Each fish instance handles:
- Movement AI with wall collision and randomized direction changes
- Growth stages: Fry → Teen → Adult, with income multipliers (0.25× / 0.6× / 1.0×)
- Hunger system and food-seeking behavior
- Mutation application (speed, size, color, predator flag, overlay texture, income multiplier)
- Predation: predator fish can consume non-predators on collision

### UI Panels

All UI is built procedurally in C# (no `.tscn` files for panels). **`Hud.cs`** is the top-level UI orchestrator — it creates and owns all panels, handles panel toggle logic, and displays money/income/fish count.

| File | Purpose |
|---|---|
| `ShopPanel.cs` | 3-category shop (Fish / Food / Decor). Fish tab has pagination and procedural card creation. |
| `MyFishPanel.cs` | Shows current aquarium population with growth stage progress and upcoming evolution times. |
| `BestiaryPanel.cs` | Species encyclopedia with rarity filtering; shows discovered vs. undiscovered state. |
| `SettingsPanel.cs` | Brightness (WMI gamma ramp) and volume (Windows Core Audio / legacy wave API) — Windows-specific. |

### Supporting Types

- **`UiTheme.cs`** — static helpers for building consistently styled panels and buttons (rounded corners, borders).
- **`FishData.cs`** — `Resource` scriptable object defining a species (textures, income rate, rarity, prices, growth durations, stage rewards).
- **`FishMutation.cs`** — `Resource` for a mutation (trigger conditions, stat effects, overlay texture).
- **`HybridRegistry.cs`** — maps species-pair keys to hybrid body textures.
- **`Localization.cs`** — Russian strings for fish names and UI text.
- **`CoinDisplay.cs`** — formats large coin numbers (K/M suffixes).
- **`FoodDropper.cs`** — handles the interactive food-drop mechanic.

### Scene Structure

- **`aquarium.tscn`** — root scene; contains background, four StaticBody2D walls, and the `Hud` UI subtree. Set as main scene in `project.godot`.
- **`node_2d.tscn`** — fish template: `CharacterBody2D` with `BodySprite`, `TailSprite`, `FinsSprite`, `EyesSprite`, `MutationOverlay`, and swim/hit animations.
- **`shop_item.tscn`** — card template instantiated by `ShopPanel`.

## Key Game Parameters (all on `GameManager` exported fields)

- Starting money: 200 coins; fish cap: 15
- Breeding: requires both fish Adult, within proximity, 70% success chance
- Hybrid chance: 33% when two different species breed
- Birth rewards: Common 12c / Rare 28c / Unique 55c
- Income cap per fish: 0.5 coins/sec
- Mutation interval: 2s evaluation cycle

## Conventions

- UI panels are built entirely in C# using `Control` node APIs — avoid creating `.tscn` files for new panels; follow the pattern in `ShopPanel.cs` or `BestiaryPanel.cs`.
- Fish species are defined as `FishData` resources (`.tres` files in `assets/`), not in code.
- `GameManager` signals drive UI updates; panels should connect to signals rather than polling.
- Localization strings live in `Localization.cs` — add new Russian strings there.
