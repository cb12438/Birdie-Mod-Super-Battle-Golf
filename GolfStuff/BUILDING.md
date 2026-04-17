# Building MimiMod

This source package intentionally does not include Unity/game reference DLLs.
Build scripts use your local Super Battle Golf installation instead.

## Requirements

- Super Battle Golf installed
- MelonLoader already installed into the game folder
- Visual Studio Build Tools 2019/2022 or another setup that provides `csc.exe`

## How the scripts find the game folder

Use any one of these methods:

1. Pass the game folder as the first argument
2. Set the `SUPER_BATTLE_GOLF_DIR` environment variable
3. Create `game_root.txt` next to the build scripts and put the full game path on the first line
4. Put this source package inside `<game folder>\Release\MimiMod-Source` so auto-detection can find `..\..`

The game folder must contain:

- `Super Battle Golf.exe`
- `MelonLoader\MelonLoader.dll`
- `version.dll`
- `dobby.dll`
- `NOTICE.txt`
- `Dependencies\`
- `Super Battle Golf_Data\Managed\...`

## Build the mod DLL

```bat
compile_mod.bat "C:\Path\To\Super Battle Golf"
```

Outputs:

- `Mods\MimiMod.dll`
- `Mods\MimiMod.pdb`

## Build the installer

```bat
build_gui_installer.bat "C:\Path\To\Super Battle Golf"
```

Outputs:

- `Installer\MimiModInstaller.exe`
- `Installer\MimiModInstaller.pdb`

The installer builder embeds:

- `MelonLoader`
- `Dependencies`
- `version.dll`
- `dobby.dll`
- `NOTICE.txt`
- `Mods\MimiMod.dll`
- `Mods\MimiMod.cfg`

## Build the full release archive

```bat
build_release.bat "C:\Path\To\Super Battle Golf"
```

Output:

- `MimiMod.zip`

## Build from Visual Studio / MSBuild

Open:

- `Source\MimiMod\MimiMod.csproj`

Then make sure one of the game-folder methods above is configured before pressing Build.
