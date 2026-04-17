@echo off
setlocal EnableExtensions
cd /d "%~dp0"

echo [1/3] Building BirdieMod.dll...
call compile_mod.bat %*
if errorlevel 1 (
    echo [ERROR] Mod build failed.
    exit /b 1
)

echo [2/3] Building BirdieModInstaller.exe + BirdieModUninstaller.exe...
call build_gui_installer.bat %*
if errorlevel 1 (
    echo [ERROR] Installer build failed.
    exit /b 1
)

echo [3/3] Creating BirdieMod.zip...
if exist "BirdieMod.zip" del /q "BirdieMod.zip"
tar -a -cf "BirdieMod.zip" Installer Mods Source compile_mod.bat build_gui_installer.bat build_release.bat BUILDING.md
if errorlevel 1 (
    echo [ERROR] Failed to create BirdieMod.zip
    exit /b 1
)

echo [OK] Output: BirdieMod.zip
exit /b 0
