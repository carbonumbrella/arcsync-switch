# CHANGELOG

## 2026-11-26: 1.0.0: Initial release of ArcSyncSwitch

- Built a standalone .NET 10 WPF utility matching the SaySo visual design (rounded cards, dynamic Windows light/dark theme following, touch-friendly `?` HelpTip popups, custom title bar).
- Implemented direct Intel `ControlLib.dll` (`ctlGetIntelArcSyncInfoForMonitor`, `ctlGetIntelArcSyncProfile`, `ctlSetIntelArcSyncProfile`) switching across `EXCELLENT` (48..120 Hz), `COMPATIBLE` (60..120 Hz), and `OFF` (120 Hz) without modifying EDID or breaking custom 1600x1000 / 1536x960 resolutions.
- Implemented direct Intel `D3DKMT` private escape toggling for `LowFpsSolutionEnabled` and `HighFpsSolutionEnabled` (LFC).
- Added silent logon auto-apply (`--apply`) via per-user Windows Task Scheduler (`LogonTask`) so the Intel driver's post-reboot reset to `COMPATIBLE` is automatically overridden with zero background memory or CPU usage.
