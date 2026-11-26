# ArcSyncSwitch

Compact Intel Arc Sync (VRR) and LFC switcher for MSI Claw handhelds, built in the SaySo visual style.

## Features

- **VRR Profile Switcher** (via Intel `ControlLib.dll`, preserves custom EDID resolutions such as 1600x1000 and 1536x960):
  - **Полный 48..120**: Intel `EXCELLENT` profile (unlocks the full 48..120 Hz panel VRR window instead of the default 60..120 Hz clamp).
  - **Сток 60..120**: Intel `COMPATIBLE` profile (factory default).
  - **Выкл (120)**: Intel `OFF` profile (fixed 120 Hz without VRR).
- **LFC Toggle** (via Intel `D3DKMT` private escape): toggles `LowFpsSolutionEnabled` and `HighFpsSolutionEnabled`.
- **Logon Autostart** (`--apply`): registers a per-user Windows logon task (`ArcSyncSwitch`, works without UAC and under Xbox Full Screen Experience) that restores the saved profile and exits immediately with zero background memory/CPU footprint.
- **Touch-friendly Help Tips (`?`)**: tap/click `?` next to each section/setting for a detailed explanation.

## Build

```cmd
build.cmd
```

Produces `publish\ArcSyncSwitch.exe` (single-file .NET 10 WPF executable) and `publish\installer\ArcSyncSwitch-Setup-1.0.0.0.exe` (per-user Inno Setup 6 installer with uninstaller and `.NET 10` runtime check).
