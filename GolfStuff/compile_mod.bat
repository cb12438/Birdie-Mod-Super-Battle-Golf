@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

echo [1/3] Locating C# compiler...
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
echo [2/3] Resolving game directory...

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
    exit /b 1
)

for %%I in ("%GAME_ROOT%") do set "GAME_ROOT=%%~fI"

if not exist "%GAME_ROOT%\Super Battle Golf.exe" (
    echo [ERROR] Invalid game folder: %GAME_ROOT%
    exit /b 1
)

echo [INFO] Game root: %GAME_ROOT%

REM ── Shared Unity references ─────────────────────────────────────────────────
set "UNITY_REFS="
for %%r in (
    "UnityEngine.dll"
    "UnityEngine.CoreModule.dll"
    "UnityEngine.UIModule.dll"
    "UnityEngine.UI.dll"
    "Unity.InputSystem.dll"
    "Unity.TextMeshPro.dll"
    "UnityEngine.PhysicsModule.dll"
    "UnityEngine.UIElementsModule.dll"
    "UnityEngine.IMGUIModule.dll"
    "UnityEngine.TextRenderingModule.dll"
    "UnityEngine.ParticleSystemModule.dll"
    "UnityEngine.AudioModule.dll"
    "UnityEngine.UnityWebRequestModule.dll"
    "UnityEngine.UnityWebRequestAudioModule.dll"
    "RosettaUI.dll"
    "netstandard.dll"
) do (
    set "UNITY_REFS=!UNITY_REFS! /reference:"%GAME_ROOT%\Super Battle Golf_Data\Managed\%%~r""
)

set "SRC_ROOT=Source\BirdieMod"

REM ═══════════════════════════════════════════════════════════════════════════
REM  BUILD — BepInEx
REM ═══════════════════════════════════════════════════════════════════════════
echo [3/3] Building BepInEx DLL...

set "BEPINEX_REF=%GAME_ROOT%\BepInEx\core\BepInEx.dll"
if not exist "%BEPINEX_REF%" (
    set "BEPINEX_REF=%~dp0lib\bepinex\BepInEx.dll"
)

if not exist "%BEPINEX_REF%" (
    echo [ERROR] BepInEx.dll not found at %BEPINEX_REF%
    echo [ERROR] Install BepInEx to %GAME_ROOT% or place BepInEx.dll in lib\bepinex\.
    exit /b 1
)

set "BEPINEX_OUT_DLL=Mods\BirdieMod.dll"
set "BEPINEX_OUT_PDB=Mods\BirdieMod.pdb"
set "BEPINEX_SRC="

for /R "%SRC_ROOT%" %%f in (*.cs) do (
    if /I not "%%~nxf"=="BirdieMod.MelonEntry.cs" (
        set "BEPINEX_SRC=!BEPINEX_SRC! "%%f""
    )
)

if exist "%BEPINEX_OUT_PDB%" del /q "%BEPINEX_OUT_PDB%"

"%CSC_PATH%" /nologo /target:library /langversion:latest /optimize+ /deterministic+ /debug:portable ^
    /define:BEPINEX ^
    /pdb:"%BEPINEX_OUT_PDB%" /out:"%BEPINEX_OUT_DLL%" ^
    /reference:"%BEPINEX_REF%" ^
    !UNITY_REFS! ^
    !BEPINEX_SRC!

if errorlevel 1 (
    echo [ERROR] BepInEx build failed.
    exit /b 1
)
echo [OK] BepInEx: %BEPINEX_OUT_DLL%

REM ═══════════════════════════════════════════════════════════════════════════
REM  DEPLOY BepInEx DLL to game plugins folder
REM ═══════════════════════════════════════════════════════════════════════════
set "GAME_PLUGINS=%GAME_ROOT%\BepInEx\plugins"
if not exist "%GAME_PLUGINS%" (
    echo [WARN] BepInEx plugins folder not found: %GAME_PLUGINS%
    exit /b 0
)
copy /y "%BEPINEX_OUT_DLL%" "%GAME_PLUGINS%\" >nul
if errorlevel 1 (
    echo [WARN] Deploy failed — copy error.
) else (
    echo [OK] Deployed: %GAME_PLUGINS%\BirdieMod.dll
)
exit /b 0
