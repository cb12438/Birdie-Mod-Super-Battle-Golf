# Birdie Mod
**Version 1.2.0** — Super Battle Golf | MelonLoader

> A feature-rich client and host mod that gives you full control over your round. Works solo, in private lobbies, or with friends. Every feature is individually toggleable — turn on what you want, leave off what you don't.

---

## Mod Loader Requirement

This version is for **MelonLoader**. Install MelonLoader into your game folder before installing this mod. The DLL goes in your game's `Mods/` folder.

For the **BepInEx / Thunderstore** version, see the [GitHub releases page](https://github.com/cb12438/Birdie-Mod-Super-Battle-Golf/releases).

---

## Opening the Settings Panel

Press **F6** in-game at any time to open or close the Birdie Mod settings panel. The panel has six tabs:

| Tab | Purpose |
|-----|---------|
| **Features** | Toggle all gameplay features on/off |
| **Keys** | Rebind every hotkey |
| **HUD** | Control what appears on your HUD |
| **$** | Grant yourself credits |
| **Items** | Spawn items into your hotbar |
| **Net** | Host Controls settings (host only) |

---

## Host vs Client — How It Works

Birdie Mod operates in two modes depending on your role in the lobby:

### If You Are the Host

You have full access to every feature. Additionally, you can optionally enable **Host Controls** from the **Net** tab to manage what modded clients are allowed to use.

The following features have server-side effects when you are hosting:

- **Expanded Slots (Apply to all players ON)** — The server expands every connected player's inventory to 7 slots. Even unmodded players can carry more items. If this option is OFF, only your own inventory is expanded.

Everything else is purely client-side and affects only your own game.

### If You Are a Client (Not the Host)

All personal features still work fully for you. The exception is **Expanded Slots** — without the host also running the mod and enabling slot expansion, your extra UI slots will be visible but items cannot be placed in them (the server still enforces a 3-slot limit).

When the host has **Host Controls** active, you will see a gold **"Host Controls Active"** banner in your Net tab. Some features may be restricted or hidden entirely depending on what the host has configured.

---

## Host Controls System

The **Host Controls** system allows a host to share the mod experience with friends while keeping specific features private or restricted.

### How to Enable

1. Open the settings panel with **F6**
2. Go to the **Net** tab
3. Toggle **Host Controls** to **ON**

### What It Does

When Host Controls is **ON**:
- Each connected modded client receives a bitmask that tells them exactly which features are allowed.
- Features you have disabled cannot be used by clients.
- Clients see a **"Host Controls Active"** banner in their Net tab.

When Host Controls is **OFF**:
- All modded clients immediately regain full unrestricted access to every feature.

### Configuring Per-Feature Access

| Toggle | What it controls |
|--------|-----------------|
| Ice Immunity | Allow/block ice immunity |
| Shot Tracer | Allow/block the shot tracer |
| Impact Preview | Allow/block impact landing preview |
| No Wind | Allow/block wind removal |
| Perfect Shot | Allow/block forced perfect shots |
| No Air Drag | Allow/block air drag removal |
| Speed Multiplier | Allow/block speed boosts |
| Infinite Item Usage | Allow/block infinite ammo |
| No Recoil | Allow/block recoil removal |
| No Knockback | Allow/block knockback immunity |
| Landmine Immunity | Allow/block landmine immunity |
| Lock-On Any Dist. | Allow/block extended lock-on range |
| Expanded Slots | Allow/block extra hotbar slots |
| Coffee Boost | Allow/block coffee boost |
| Assist | Allow/block auto-swing assist |

All toggles default to **ON** (all features allowed).

---

## Feature Reference

### CORE Features

#### Assist
Auto-aims your shot and releases at the statistically optimal moment. Accounts for wind direction, ball lie, and distance to the hole.

**Hotkey:** F1 (rebindable)

---

#### Ice Immunity
Prevents ice surfaces from applying their slipping effect to your character.

**Hotkey:** I (rebindable)

---

#### Shot Tracer
Renders a visible line showing the actual flight path of your ball after you hit it.

---

#### Impact Preview
Renders a marker on the terrain showing exactly where your ball will land based on current aim and power. Updates in real time as you adjust your shot.

---

### EXTRAS Features

#### No Wind
Suppresses the wind manager's force scale so that wind has zero effect on your ball.

**Hotkey:** W (rebindable)

---

#### Perfect Shot
Forces your swing power field to the perfect zone while you are holding the swing button.

**Hotkey:** P (rebindable)

---

#### No Air Drag
Removes the linear air drag coefficient so the ball carries further through the air.

**Hotkey:** D (rebindable)

---

#### Speed Multiplier
Multiplies your movement speed by a configurable factor (0.5× to 10×).

**Hotkey:** S (rebindable)

---

#### Infinite Item Usage
Patches item consumption so weapons and consumable items never deplete.

**Hotkey:** A (rebindable)

---

#### No Recoil
Removes the screen shake and camera kick when you fire weapons.

**Hotkey:** R (rebindable)

---

#### No Knockback
Prevents incoming damage from applying knockback velocity to your character.

**Hotkey:** K (rebindable)

---

#### Landmine Immunity
Bypasses the landmine detonation trigger for your player.

**Hotkey:** M (rebindable)

---

#### Lock-On Any Distance
Removes the maximum range check from the lock-on targeting system.

**Hotkey:** L (rebindable)

---

#### Coffee Boost
Applies a short-duration speed burst on demand.

**Hotkey:** F2 (rebindable)

---

### Expanded Slots System

Expands your hotbar to 7 item slots (keys 1–7) plus the golf club slot (key 8).

- **As host with "Apply to all players" ON** — All connected players get expanded slots, even those without the mod.
- **As host with "Apply to all players" OFF** — Only your own inventory is expanded.
- **As client without host mod** — UI shows extra slots but the server rejects picking up more than 3 items.

**Hotkey:** U (rebindable)

---

## Default Keybinds

| Key | Action |
|-----|--------|
| **F6** | Open / close settings panel |
| **F1** | Toggle Assist (auto-swing) |
| **F2** | Coffee Boost |
| **F3** | Nearest ball mode |
| **F4** | Unlock cosmetics |
| **H** | Toggle HUD |
| **G** | Collect random item |
| **I** | Toggle Ice Immunity |
| **W** | Toggle No Wind |
| **P** | Toggle Perfect Shot |
| **D** | Toggle No Air Drag |
| **S** | Toggle Speed Multiplier |
| **A** | Toggle Infinite Item Usage |
| **R** | Toggle No Recoil |
| **K** | Toggle No Knockback |
| **M** | Toggle Landmine Immunity |
| **L** | Toggle Lock-On Any Distance |
| **U** | Toggle Expanded Slots |

---

## Installation (MelonLoader)

1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) into your `Super Battle Golf` game folder.
2. Run the game once and close it — MelonLoader will create its folder structure.
3. Drop `BirdieMod.dll` into `Super Battle Golf/Mods/`.
4. Launch the game. Press F6 in-game to open settings.

---

## Credits

**Birdie Mod** — cb12438
Forked and greatly expanded from MidTano's original swing-assist foundation. Added Host Controls networking, wind compensation, full IMGUI settings panel, landmine/ice/knockback immunity, expanded item slots, lock-on range extension, BepInEx dual-build support, and more.

**MidTano** — Original Mod
The bare-bones swing-assist foundation that Birdie Mod is built upon.

**MelonLoader** — https://github.com/LavaGang/MelonLoader
The mod loader that makes this possible.
