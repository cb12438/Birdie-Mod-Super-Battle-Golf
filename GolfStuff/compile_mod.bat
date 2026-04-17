@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

echo [1/4] Locating C# compiler...
set "CSC_PATH="
set "PF86=%ProgramFiles(x86)%"
set "PF64=%ProgramFiles%"

for %%p in (
    "!PF86!\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
    "!PF86!\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\csc.exe"
    "!PF64!\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
    "!PF64!\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\csc.exe"
    "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
) do (
    if not defined CSC_PATH if exist "%%~p" (
        set "CSC_PATH=%%~p"
    )
)

if not defined CSC_PATH (
    echo [ERROR] C# compiler not found.
    exit /b 1
)

echo [INFO] Compiler: %CSC_PATH%
echo [2/4] Resolving game directory...

set "GAME_ROOT=%~1"
if not defined GAME_ROOT if defined SUPER_BATTLE_GOLF_DIR set "GAME_ROOT=%SUPER_BATTLE_GOLF_DIR%"
if not defined GAME_ROOT if exist "game_root.txt" set /p GAME_ROOT=<"game_root.txt"
if defined GAME_ROOT set "GAME_ROOT=%GAME_ROOT:"=%"

if not defined GAME_ROOT (
    for %%I in (".","..","..\..") do (
        if not defined GAME_ROOT if exist "%%~fI\Super Battle Golf.exe" (
            set "GAME_ROOT=%%~fI"
        )
    )
)

if not defined GAME_ROOT (
    echo [ERROR] Game folder not found.
    echo [ERROR] Pass the game path as the first argument, set SUPER_BATTLE_GOLF_DIR, or create game_root.txt.
    echo [ERROR] See BUILDING.md for examples.
    exit /b 1
)

for %%I in ("%GAME_ROOT%") do set "GAME_ROOT=%%~fI"

if not exist "%GAME_ROOT%\Super Battle Golf.exe" (
    echo [ERROR] Invalid game folder: %GAME_ROOT%
    echo [ERROR] Super Battle Golf.exe was not found.
    exit /b 1
)

for %%I in (
    "%GAME_ROOT%\MelonLoader\MelonLoader.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.CoreModule.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.UIModule.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.UI.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\Unity.InputSystem.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\Unity.TextMeshPro.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.PhysicsModule.dll"
    "%GAME_ROOT%\Super Battle Golf_Data\Managed\netstandard.dll"
) do (
    if not exist %%~I (
        echo [ERROR] Missing required reference: %%~I
        exit /b 1
    )
)

echo [INFO] Game root: %GAME_ROOT%
echo [3/4] Building mod...

set "OUT_DLL=Mods\BirdieMod.dll"
set "OUT_PDB=Mods\BirdieMod.pdb"
set "SRC_ROOT=Source\BirdieMod"
set "SRC_FILES="

for /R "%SRC_ROOT%" %%f in (*.cs) do (
    set "SRC_FILES=!SRC_FILES! "%%f""
)

if not defined SRC_FILES (
    echo [ERROR] No source files found under %SRC_ROOT%
    exit /b 1
)

if exist "%OUT_PDB%" del /q "%OUT_PDB%"

"%CSC_PATH%" /nologo /target:library /langversion:latest /optimize+ /deterministic+ /debug:portable /pdb:"%OUT_PDB%" /out:"%OUT_DLL%" ^
    /reference:"%GAME_ROOT%\MelonLoader\MelonLoader.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.CoreModule.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.UIModule.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.UI.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\Unity.InputSystem.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\Unity.TextMeshPro.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.PhysicsModule.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.UIElementsModule.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.IMGUIModule.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\UnityEngine.TextRenderingModule.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\RosettaUI.dll" ^
    /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\netstandard.dll" ^
    !SRC_FILES!

if errorlevel 1 (
    echo [ERROR] Build failed.
    exit /b 1
)

if exist "Mods\PlayerMenuMod.dll" (
    del /q "Mods\PlayerMenuMod.dll"
)

echo [4/4] Build completed successfully.
echo [OK] Output: %OUT_DLL%
echo [OK] Symbols: %OUT_PDB%

echo [5/5] Deploying to game Mods folder...
set "GAME_MODS=%GAME_ROOT%\Mods"
if not exist "%GAME_MODS%" (
    echo [WARN] Game Mods folder not found: %GAME_MODS%
    exit /b 0
)
copy /y "%OUT_DLL%" "%GAME_MODS%\" >nul
if errorlevel 1 (
    echo [WARN] Deploy failed — copy error.
) else (
    echo [OK] Deployed: %GAME_MODS%\BirdieMod.dll
)
exit /b 0
