# Birdie Mod

> A feature-rich host & client mod for **Super Battle Golf**

Birdie Mod gives you a full in-game settings panel to toggle gameplay features on and off at any time. Works solo or with friends in private lobbies. The **Host Controls** system lets the lobby host decide which features modded players are allowed to use — so you can share the experience in a controlled, agreed-upon way.

Press **F6** in-game to open the settings panel.

---

## Downloads

| Version | Download |
|---------|----------|
| **BepInEx** (Thunderstore) | [Latest Release](https://github.com/cb12438/Birdie-Mod-Super-Battle-Golf/releases) → `BirdieMod-BepInEx-vX.X.X.zip` |
| **MelonLoader** | [Latest Release](https://github.com/cb12438/Birdie-Mod-Super-Battle-Golf/releases) → `BirdieMod-MelonLoader-vX.X.X.zip` |

---

## Installation

### BepInEx (Thunderstore)
1. Install [BepInEx 5 Unity Mono x64](https://github.com/BepInEx/BepInEx/releases) into your `Super Battle Golf` game folder.
2. Run the game once and close it — BepInEx will create its folder structure.
3. Drop `BirdieMod.dll` into `Super Battle Golf/BepInEx/plugins/`.
4. Launch the game. Press **F6** to open settings.

### MelonLoader
1. Install [MelonLoader](https://github.com/LavaGang/MelonLoader) into your `Super Battle Golf` game folder.
2. Run the game once and close it — MelonLoader will create its folder structure.
3. Drop `BirdieMod.dll` into `Super Battle Golf/Mods/`.
4. Launch the game. Press **F6** to open settings.

---

## Features

Open the settings panel with **F6** to toggle everything on or off individually.

![Features Tab](https://i.imgur.com/aShiXJj.png)

| Feature | Description |
|---------|-------------|
| **Assist** | Auto-aims and releases your swing at the optimal moment, accounting for wind and distance |
| **Ice Immunity** | Walk on ice without slipping |
| **Shot Tracer** | Shows your ball's actual flight path |
| **Impact Preview** | Shows exactly where your ball will land in real time |
| **No Wind** | Wind does not affect your ball trajectory |
| **Perfect Shot** | Forces every shot to register as perfect power |
| **No Air Drag** | Ball travels further through the air |
| **Speed Multiplier** | Move faster around the course (adjustable 0.5× to 10×) |
| **Infinite Item Usage** | Weapons and consumables never run out |
| **No Recoil** | Removes screen shake when firing weapons |
| **No Knockback** | Prevents being flung by explosions and hits |
| **Landmine Immunity** | Walk over landmines without triggering them |
| **Lock-On Any Distance** | Lock-on targets golf balls at any range |
| **Coffee Boost** | Instant speed burst on demand |
| **Expanded Slots** | Expands the hotbar to 7 item slots + golf club slot |

![Expanded Slots](https://i.imgur.com/h7Qx5bH.png)

---

## Host Controls

When you are the lobby host, the **Net** tab lets you enable **Host Controls** — a permission system that lets you decide which features other modded players in your lobby can use.

- Turn Host Controls **ON** to send a permissions list to all connected modded clients.
- Toggle each feature individually to allow or block it.
- Clients see a **"Host Controls Active"** banner and are restricted accordingly.
- Turn it **OFF** at any time to restore full access for everyone.

This makes it easy to play with friends with agreed-upon rules — everyone knows what's enabled.

---

## Expanded Slots — How It Works

By default Super Battle Golf gives every player 3 item slots. When **Expanded Slots** is enabled:

- Your hotbar UI expands to show 7 item slots (keys 1–7) plus the golf club slot (key 8).
- As **host**, an **"Apply to all players"** sub-toggle appears. When ON, the server expands every connected player's inventory — even players without the mod can carry more items. When OFF, only your own inventory is expanded.
- As **client** without a modded host, the extra UI slots show but the server won't allow items past slot 3.

---

## Default Keybinds

All keybinds can be rebound in the **Keys** tab.

![Keybinds Tab](https://i.imgur.com/vtidAqW.png)

| Key | Action |
|-----|--------|
| **F6** | Open / close settings panel |
| **F1** | Toggle Assist |
| **F2** | Coffee Boost |
| **F3** | Nearest ball mode |
| **F4** | Unlock cosmetics |
| **H** | Toggle HUD |
| **G** | Collect random item |
| **I** | Ice Immunity |
| **W** | No Wind |
| **P** | Perfect Shot |
| **D** | No Air Drag |
| **S** | Speed Multiplier |
| **A** | Infinite Item Usage |
| **R** | No Recoil |
| **K** | No Knockback |
| **M** | Landmine Immunity |
| **L** | Lock-On Any Distance |
| **U** | Expanded Slots |

---

## Building from Source

**Requirements:** Visual Studio 2019 or 2022 Build Tools (C# compiler), Super Battle Golf installed via Steam.

```bat
# 1. Set your game path
echo D:\SteamLibrary\steamapps\common\Super Battle Golf > GolfStuff\game_root.txt

# 2. Build both DLLs (MelonLoader + BepInEx)
cd GolfStuff
compile_mod.bat
```

Source files are in [`GolfStuff/Source/BirdieMod/`](GolfStuff/Source/BirdieMod/). The same source compiles to both mod loaders — `compile_mod.bat` produces both DLLs in one run.

---

## Credits

**Birdie Mod** — cb12438
Forked and greatly expanded from MidTano's original swing-assist foundation. Added Host Controls networking, wind compensation, full IMGUI settings panel, immunity features, expanded item slots, lock-on range extension, BepInEx dual-build support, and more.

**MidTano** — Original Mod
The bare-bones swing-assist foundation that Birdie Mod is built upon.

**BepInEx** — https://github.com/BepInEx/BepInEx

**MelonLoader** — https://github.com/LavaGang/MelonLoader
