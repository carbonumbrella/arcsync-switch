@echo off
setlocal
cd /d "%~dp0"
if not defined DOTNET set "DOTNET=C:\AI\dotnet\dotnet.exe"
set DOTNET_CLI_TELEMETRY_OPTOUT=1
set DOTNET_NOLOGO=1

"%DOTNET%" publish src -c Release -r win-x64 --self-contained false ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o publish || exit /b 1
echo Build OK: publish\ArcSyncSwitch.exe
