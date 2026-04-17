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

for %%I in (
    "%GAME_ROOT%\Super Battle Golf.exe"
    "%GAME_ROOT%\version.dll"
    "%GAME_ROOT%\dobby.dll"
    "%GAME_ROOT%\NOTICE.txt"
    "%GAME_ROOT%\MelonLoader"
    "%GAME_ROOT%\Dependencies"
    "Mods\BirdieMod.dll"
    "Mods\BirdieMod.cfg"
) do (
    if not exist %%~I (
        echo [ERROR] Missing required installer input: %%~I
        exit /b 1
    )
)

echo [INFO] Game root: %GAME_ROOT%
echo [3/5] Preparing embedded payload and zip...

set "PS_SCRIPT=%TEMP%\birdie_build_payload_%RANDOM%.ps1"
(
    echo $ErrorActionPreference = 'Stop'
    echo $root = '%CD%'
    echo $gameRoot = '%GAME_ROOT%'
    echo $installerDir = Join-Path $root 'Installer'
    echo $stage = Join-Path $installerDir '_PayloadStage'
    echo $payloadZip = Join-Path $installerDir 'PayloadBundle.zip'
    echo Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    echo Remove-Item $payloadZip -Force -ErrorAction SilentlyContinue
    echo New-Item -ItemType Directory -Path $stage^,(Join-Path $stage 'Mods'^) -Force ^| Out-Null
    echo Copy-Item (Join-Path $root 'Mods\BirdieMod.dll'^) (Join-Path $stage 'Mods\BirdieMod.dll'^) -Force
    echo Copy-Item (Join-Path $root 'Mods\BirdieMod.cfg'^) (Join-Path $stage 'Mods\BirdieMod.cfg'^) -Force
    echo Copy-Item (Join-Path $gameRoot 'version.dll'^) (Join-Path $stage 'version.dll'^) -Force
    echo Copy-Item (Join-Path $gameRoot 'dobby.dll'^) (Join-Path $stage 'dobby.dll'^) -Force
    echo Copy-Item (Join-Path $gameRoot 'NOTICE.txt'^) (Join-Path $stage 'NOTICE.txt'^) -Force
    echo Copy-Item (Join-Path $gameRoot 'MelonLoader'^) (Join-Path $stage 'MelonLoader'^) -Recurse -Force
    echo Copy-Item (Join-Path $gameRoot 'Dependencies'^) (Join-Path $stage 'Dependencies'^) -Recurse -Force
    echo Remove-Item (Join-Path $stage 'MelonLoader\Logs'^) -Recurse -Force -ErrorAction SilentlyContinue
    echo Remove-Item (Join-Path $stage 'MelonLoader\Latest.log'^) -Force -ErrorAction SilentlyContinue
    echo Add-Type -AssemblyName System.IO.Compression.FileSystem
    echo [System.IO.Compression.ZipFile]::CreateFromDirectory($stage^, $payloadZip^)
) > "%PS_SCRIPT%"

powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%"
set "PS_ERR=%ERRORLEVEL%"
del /q "%PS_SCRIPT%" 2>nul

if %PS_ERR% neq 0 (
    echo [ERROR] Failed to prepare payload.
    exit /b 1
)

echo [4/5] Building BirdieModInstaller.exe...
if exist "Installer\BirdieModInstaller.pdb" del /q "Installer\BirdieModInstaller.pdb"

"%CSC_PATH%" /nologo /target:winexe /optimize+ /debug:portable /pdb:"Installer\BirdieModInstaller.pdb" /out:"Installer\BirdieModInstaller.exe" ^
    /resource:"Installer\PayloadBundle.zip",BirdieMod.PayloadBundle.zip ^
    /reference:"System.dll" ^
    /reference:"System.Core.dll" ^
    /reference:"System.Drawing.dll" ^
    /reference:"System.Windows.Forms.dll" ^
    /reference:"System.IO.Compression.dll" ^
    /reference:"System.IO.Compression.FileSystem.dll" ^
    "Installer\BirdieModInstaller.cs"

if errorlevel 1 (
    echo [ERROR] Installer build failed.
    exit /b 1
)

if exist "Installer\_PayloadStage" rmdir /s /q "Installer\_PayloadStage"
if exist "Installer\PayloadBundle.zip" del /q "Installer\PayloadBundle.zip"

echo [5/5] Building BirdieModUninstaller.exe...
if exist "Installer\BirdieModUninstaller.pdb" del /q "Installer\BirdieModUninstaller.pdb"

"%CSC_PATH%" /nologo /target:winexe /optimize+ /debug:portable /pdb:"Installer\BirdieModUninstaller.pdb" /out:"Installer\BirdieModUninstaller.exe" ^
    /reference:"System.dll" ^
    /reference:"System.Core.dll" ^
    /reference:"System.Drawing.dll" ^
    /reference:"System.Windows.Forms.dll" ^
    "Installer\BirdieModUninstaller.cs"

if errorlevel 1 (
    echo [ERROR] Uninstaller build failed.
    exit /b 1
)

if not exist "Release" mkdir "Release"
copy /Y "Installer\BirdieModInstaller.exe"     "Release\BirdieModInstaller.exe"     >nul
copy /Y "Installer\BirdieModInstaller.pdb"     "Release\BirdieModInstaller.pdb"     >nul
copy /Y "Installer\BirdieModUninstaller.exe"   "Release\BirdieModUninstaller.exe"   >nul
copy /Y "Installer\BirdieModUninstaller.pdb"   "Release\BirdieModUninstaller.pdb"   >nul
copy /Y "Mods\BirdieMod.dll"                   "Release\BirdieMod.dll"               >nul
copy /Y "Mods\BirdieMod.pdb"                   "Release\BirdieMod.pdb"               >nul

if exist "Release\Source" rmdir /s /q "Release\Source"
xcopy /E /I /Q "Source\BirdieMod" "Release\Source" >nul

echo [OK] Installer:   Installer\BirdieModInstaller.exe
echo [OK] Uninstaller: Installer\BirdieModUninstaller.exe
echo [OK] Release:     Release\
exit /b 0
