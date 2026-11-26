@echo off
setlocal
cd /d "%~dp0"
if not defined DOTNET (
  where dotnet >nul 2>&1 && set "DOTNET=dotnet" || (
    if exist "C:\Program Files\dotnet\dotnet.exe" (set "DOTNET=C:\Program Files\dotnet\dotnet.exe") else (set "DOTNET=C:\AI\dotnet\dotnet.exe")
  )
)
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1

"%DOTNET%" publish src -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish || exit /b 1
echo Build OK: publish\ArcSyncSwitch.exe

if defined ISCC (
  "%ISCC%" "installer\ArcSyncSwitch.iss" || exit /b 1
  echo Installer OK: publish\installer\
) else (
  echo Inno Setup 6 not found, skipping installer build.
)
