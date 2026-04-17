# Birdie Mod
**Version 1.2.0** — Super Battle Golf | BepInEx

> A feature-rich client and host mod that gives you full control over your round. Works solo, in private lobbies, or with friends. Every feature is individually toggleable — turn on what you want, leave off what you don't.

---

## Important: Is This a Cheat Mod?

Yes — Birdie Mod gives you mechanical advantages. Use it in private sessions with friends who are all aware and consenting. Do not use it in public lobbies to grief other players. The **Host Controls** system was specifically designed so that friends can share the mod privately while keeping certain features hidden or restricted.

---

## Mod Loader Requirement

This version is for **BepInEx 5**. Install BepInEx 5 (Unity Mono x64) into your game folder before installing this mod. The DLL goes in `BepInEx/plugins/`.

For the **MelonLoader** version, see the [GitHub releases page](https://github.com/cb12438/Birdie-Mod-Super-Battle-Golf/releases).

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

As soon as you enable it, a configuration message is broadcast to all connected players who also have Birdie Mod installed. They immediately receive your feature allowlist and are restricted accordingly.

### What It Does

When Host Controls is **ON**:
- Each connected modded client receives a bitmask that tells them exactly which features are allowed.
- Features you have disabled cannot be used by clients — the toggle does nothing when pressed.
- The **Assist** feature is **never included** in the host broadcast. Clients will not see it in their Features tab at all — it is completely hidden from their UI.
- Clients see a **"Host Controls Active"** banner in their Net tab so they know restrictions are in place.

When Host Controls is **OFF**:
- All modded clients immediately regain full unrestricted access to every feature.
- Switching it off mid-session takes effect instantly — no reconnection required.

### Configuring Per-Feature Access

While Host Controls is ON, an **Allowed Features** section appears in your Net tab with a toggle for each feature:

| Toggle | Bit | What it controls |
|--------|-----|-----------------|
| Ice Immunity | 0 | Allow/block ice immunity |
| Shot Tracer | 1 | Allow/block the shot tracer |
| Impact Preview | 2 | Allow/block impact landing preview |
| No Wind | 3 | Allow/block wind removal |
| Perfect Shot | 4 | Allow/block forced perfect shots |
| No Air Drag | 5 | Allow/block air drag removal |
| Speed Multiplier | 6 | Allow/block speed boosts |
| Infinite Item Usage | 7 | Allow/block infinite ammo |
| No Recoil | 8 | Allow/block recoil removal |
| No Knockback | 9 | Allow/block knockback immunity |
| Landmine Immunity | 10 | Allow/block landmine immunity |
| Lock-On Any Dist. | 11 | Allow/block extended lock-on range |
| Expanded Slots | 12 | Allow/block extra hotbar slots |
| Coffee Boost | 13 | Allow/block coffee boost |

All toggles default to **ON** (all features allowed). The **Assist** feature is never listed here — it is always private to the host.

### Persistence

- **Host Controls ON/OFF state** does NOT persist between sessions. It always starts as OFF when you launch the game.
- **The per-feature allowlist** DOES persist in your config file (`BirdieMod.cfg`). Your custom feature permissions carry over between sessions.

---

## Feature Reference

### CORE Features

#### Assist (Host-Only, Hidden from Clients)
Auto-aims your shot and releases at the statistically optimal moment. Accounts for wind direction, ball lie, and distance to the hole. This feature is invisible to clients when Host Controls is active — they will never see it in their UI.

**Hotkey:** F1 (rebindable)

---

#### Ice Immunity
Prevents ice surfaces from applying their slipping effect to your character. You move at full speed across ice patches.

**Hotkey:** I (rebindable)

---

#### Shot Tracer
Renders a visible line showing the actual flight path of your ball after you hit it. Useful for learning trajectories.

---

#### Impact Preview
Renders a marker on the terrain showing exactly where your ball will land based on current aim and power. Updates in real time as you adjust your shot.

---

### EXTRAS Features

#### No Wind
Suppresses the wind manager's force scale so that wind has zero effect on your ball. The wind arrows on the HUD may still display, but the deflection is removed.

**Hotkey:** W (rebindable)

---

#### Perfect Shot
Forces your swing power field to the perfect zone (0.999) while you are holding the swing button. Every shot is registered as a perfect power hit regardless of your actual timing.

**Hotkey:** P (rebindable)

---

#### No Air Drag
Removes the linear air drag coefficient from the golf ball settings so the ball carries further through the air. Particularly effective on long holes.

**Hotkey:** D (rebindable)

---

#### Speed Multiplier
Multiplies your movement speed by a configurable factor. The default multiplier is adjustable via slider in the Features tab from 0.5× to 10×.

**Hotkey:** S (rebindable)

---

#### Infinite Item Usage
Patches the server-authoritative inventory update to ignore item consumption for your player. Weapons and consumable items never deplete.

**Hotkey:** A (rebindable)

---

#### No Recoil
Removes the screen shake and camera kick that occurs when you fire weapons. Does not affect the projectile itself, only the visual feedback.

**Hotkey:** R (rebindable)

---

#### No Knockback
Prevents incoming damage from applying knockback velocity to your character. You stay in position when hit by weapons or explosions.

**Hotkey:** K (rebindable)

---

#### Landmine Immunity
Bypasses the landmine detonation trigger for your player. You walk over placed landmines without activating them.

**Hotkey:** M (rebindable)

---

#### Lock-On Any Distance
Removes the maximum range check from the lock-on targeting system. You can lock onto other players' golf balls from anywhere on the course.

**Hotkey:** L (rebindable)

---

#### Coffee Boost
Applies a short-duration speed burst to your character. The same mechanic used by in-game coffee items, triggered on demand.

**Hotkey:** F2 (rebindable)

---

### Expanded Slots System

The **Expanded Slots** feature modifies both the client-side hotbar UI and the server-side inventory to give you up to 7 item slots.

#### Default Inventory

By default, Super Battle Golf gives every player 3 item slots (slot 1, 2, 3) plus the golf club slot. The hotbar UI only shows these 4 positions.

#### What Expanded Slots Does

When enabled, the mod:

1. **Expands the UI** — Clones existing hotbar slot UI elements to create slots 4 through 7 (keys 4–7 for direct selection).
2. **Adjusts local settings** — Updates the `PlayerInventorySettings.MaxItems` value on your client so pickup logic allows additional items.
3. **Expands the server SyncList** — If you are the host, adds entries to your `PlayerInventory.slots` SyncList. Mirror networking replicates this to all clients.

#### "Apply to All Players" Option

A sub-toggle appears beneath Expanded Slots when it is enabled (host only):

- **ON (default)** — Uses `FindObjectsOfType<PlayerInventory>()` to expand every connected player's SyncList. All players — even those without the mod — can now hold up to 7 items. Unmodded players won't see the extra UI slots, but they can pick up more items and the server accepts it.
- **OFF** — Only your own inventory SyncList is expanded. Other players keep their 3-slot limit.

#### Client Behavior Without Host

If you are a client and the host does not have the mod, your UI will show 8 slots but the server will reject picking up more than 3 items. The extra slots will remain empty.

**Hotkey:** U (rebindable)

---

## Items Tab

The **Items** tab lets you spawn any item directly into your inventory via the server's item grant system. The host player must also have Birdie Mod installed for this to work — it uses a Mirror Command message to request the item spawn server-side.

When the host does not have the mod, a red status message is shown: *"Host does not have BirdieMod — items require host."*

---

## HUD

The Birdie Mod HUD displays real-time information on screen:

- **Bottom keybind bar** — shows your active hotkeys
- **Ball distance to hole** — shows distance in yards from your ball to the flag
- **Ice immunity indicator** — small icon when ice immunity is active
- **Center title** — brief notification when you toggle a feature
- **Player info** — your player name and role in the top-left

All HUD elements can be individually toggled in the **HUD** tab.

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

All keybinds can be rebound in the **Keys** tab of the settings panel. Changes save immediately to `BirdieMod.cfg`.

---

## Configuration File

Birdie Mod saves all settings to `BepInEx/config/BirdieMod.cfg` (BepInEx) or `UserData/BirdieMod.cfg` (MelonLoader). The config is created automatically on first run.

Settings that persist:
- All keybind assignments
- HUD visibility toggles
- Speed multiplier factor
- Host Controls per-feature allowlist (`hostAllowedFeatureMask`)
- Credits grant amount

Settings that do NOT persist:
- Whether Host Controls is currently active (always starts as OFF)
- Whether any individual feature is toggled on (features start OFF each session)

---

## FAQ

**Do other players need the mod for me to use it?**
No. Most features are purely client-side. The only exception is the Items tab (requires host to have the mod) and Expanded Slots when you want server-side backing.

**Will this mod work in public lobbies?**
The mod loads and features work, but using it against players who haven't consented is unsportsmanlike. Please use it in private sessions.

**The host turned on Host Controls — can I tell?**
Yes. A gold banner reading **"Host Controls Active"** appears in your Net tab when restrictions are active. You won't see which features are blocked, only that restrictions are in place.

**Can I disable specific features without the host knowing?**
Yes. If the host allows a feature, you can still choose to keep it off. Host Controls only sets the maximum allowed, not a forced state.

**Expanded Slots says "8 slots" but I only count 7 item slots.**
The expanded target is 7 item slots plus the always-present golf club slot = 8 total positions (keys 1–8 where slot 1 is the golf club). The game uses 1-indexed slot display.

---

## Installation (BepInEx)

1. Install [BepInEx 5 Unity Mono x64](https://github.com/BepInEx/BepInEx/releases) into your `Super Battle Golf` game folder.
2. Run the game once and close it — BepInEx will create its folder structure.
3. Drop `BirdieMod.dll` into `Super Battle Golf/BepInEx/plugins/`.
4. Launch the game. Press F6 in-game to open settings.

---

## Credits

**Birdie Mod** — cb12438
Forked and greatly expanded from MidTano's original swing-assist foundation. Added Host Controls networking, wind compensation, full IMGUI settings panel, landmine/ice/knockback immunity, expanded item slots, lock-on range extension, BepInEx dual-build support, and more.

**MidTano** — Original Mod
The bare-bones swing-assist foundation that Birdie Mod is built upon.

**BepInEx** — https://github.com/BepInEx/BepInEx
The mod loader that makes this possible.
