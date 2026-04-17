# Birdie Mod

> Trainer mod for **Super Battle Golf**

A [MelonLoader](https://github.com/LavaGang/MelonLoader) mod that adds swing assist with wind compensation, a full in-game settings UI, landmine/ice/knockback immunity, expanded item slots, lock-on range extension, cosmetic unlocks, and more.

Press **F6** in-game to open the settings panel and toggle anything on or off.

---

## Requirements

- Super Battle Golf installed via Steam
- Windows 10 or later
- **MelonLoader is bundled — you do not need to install it separately**

---

## Installation

1. Download `BirdieModInstaller.exe` from the [GolfStuff/Release](GolfStuff/Release/) folder
2. Run the installer — it will automatically detect your game folder
3. If the game folder is not found, click **Browse** and select the folder containing `Super Battle Golf.exe`
4. Click **Install** and wait for it to finish
5. Launch the game normally through Steam

**Updating:** Run the new installer and click Install — it detects an existing install and overwrites only what changed.

**Uninstalling:** Run `BirdieModUninstaller.exe` and click Uninstall. Original game files are restored from backup.

> **Antivirus warning?** Windows Defender may flag the installer. This is a false positive caused by MelonLoader's DLL injection, the same technique used by all game mods. The mod is safe.

---

## Features

Open the settings panel with **F6** to toggle everything.

### Everyone — works for any player regardless of host status

| Feature | Description |
|---|---|
| **Swing Assist** | Auto-aims and releases your swing perfectly, accounting for wind and distance to the hole |
| **Nearest Ball Mode** | Target the closest ball on the course |
| **Perfect Shot** | Forces every shot to register as perfect |
| **No Knockback** | Ignore knockback from hits |
| **Ice Immunity** | Walk on ice without slipping |
| **Landmine Immunity** | Walk over landmines without being hit |
| **Lock-On Any Distance** | Lock-on targets golf balls at any range |
| **Unlock Cosmetics** | Unlock all cosmetic items |
| **Coffee Boost** | Instant speed burst |
| **Infinite Ammo** | Never run out of item ammo |
| **HUD** | On-screen display showing which features are currently active |

### Host Recommended — full effect as host; may be partial as client

| Feature | Description |
|---|---|
| **No Wind** | Wind does not affect your ball trajectory |
| **No Air Drag** | Ball travels further through the air |
| **Speed Multiplier** | Move faster around the course |
| **No Recoil** | Eliminates weapon recoil |

### Host Provides For All — host must have mod for full benefit

| Feature | Description |
|---|---|
| **Expanded Slots** | Expands the hotbar to 8 total slots (keys 1–8). As host: all players in the lobby get real server-side slots. As client: visual UI only. |

---

## Multiplayer & Host Compatibility

Birdie Mod only affects the player who has it installed. Other players in your lobby are **never affected** unless noted above.

- **As Host:** All features work at full strength. Expanded Slots adds real server-side inventory slots — every player in the lobby benefits, even those without the mod.
- **As Client:** All personal features still work. Expanded Slots gives you the visual UI expansion, but extra slots have no server backing unless the host also runs the mod.

---

## Default Keybinds

All keybinds can be rebound in the **Keys** tab of the F6 settings menu.

| Key | Action |
|---|---|
| `F6` | Open / close settings menu |
| `F1` | Toggle swing assist |
| `F2` | Coffee boost |
| `F3` | Nearest ball mode |
| `F4` | Unlock cosmetics |
| `H` | Toggle HUD |
| `G` | Collect random item |
| `I` | Toggle ice immunity |
| `W` | Toggle no wind |
| `P` | Toggle perfect shot |
| `D` | Toggle no air drag |
| `S` | Toggle speed multiplier |
| `A` | Toggle infinite ammo |
| `R` | Toggle no recoil |
| `K` | Toggle no knockback |
| `M` | Toggle landmine immunity |
| `L` | Toggle lock-on any distance |
| `U` | Toggle expanded item slots |

---

## Building from Source

**Requirements:**
- Visual Studio 2019 or 2022 Build Tools (C# compiler)
- Super Battle Golf installed via Steam

**Steps:**

```bat
# 1. Create game_root.txt with the path to your game folder
echo D:\SteamLibrary\steamapps\common\Super Battle Golf > GolfStuff\game_root.txt

# 2. Build BirdieMod.dll (also deploys to your game's Mods folder)
cd GolfStuff
compile_mod.bat

# 3. Build BirdieModInstaller.exe + BirdieModUninstaller.exe
build_gui_installer.bat
```

Source files are in [`GolfStuff/Source/BirdieMod/`](GolfStuff/Source/BirdieMod/) (plain `.cs` files, no preprocessor tricks).

All binaries compiled with `/optimize+ /debug:portable` (release builds). No string encryption, no import obfuscation, no packing.

---

## Credits

**Birdie Mod — Cb12438**
Forked and greatly expanded from MidTano's original swing-assist mod. Added wind compensation, full settings UI, landmine/ice/knockback immunity, expanded item slots, lock-on range, and more.

**MidTano — Original Mod**
Created the bare-bones swing-assist foundation that Birdie Mod is based on. Thanks for laying the groundwork.

**MelonLoader — https://github.com/LavaGang/MelonLoader**
The mod loader that makes this all possible.
