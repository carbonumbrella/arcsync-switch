using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace ArcSyncSwitch
{
    public enum VrrMode
    {
        Full = 2,   // EXCELLENT: 48..120 Hz
        Stock = 4,  // COMPATIBLE: 60..120 Hz
        Off = 5     // OFF: 120..120 Hz
    }

    public sealed class AppConfig
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public VrrMode Mode { get; set; } = VrrMode.Full;
        public bool LfcEnabled { get; set; } = true;
        public string Language { get; set; } = "auto";
    }

    public sealed class DriverSnapshot
    {
        public bool ControlLibOk { get; set; }
        public float MonMinHz { get; set; }
        public float MonMaxHz { get; set; }
        public int ProfileId { get; set; }
        public string ProfileName { get; set; } = "";
        public float ActiveMinHz { get; set; }
        public float ActiveMaxHz { get; set; }
        public uint IncUs { get; set; }
        public uint DecUs { get; set; }

        public bool D3dOk { get; set; }
        public bool LowFpsEnabled { get; set; }
        public bool HighFpsEnabled { get; set; }
        public string Error { get; set; } = "";

        public bool LfcEnabled => LowFpsEnabled || HighFpsEnabled;
        public VrrMode? DetectedMode => ProfileId switch
        {
            2 => VrrMode.Full,
            4 => VrrMode.Stock,
            5 => VrrMode.Off,
            _ => null
        };
    }

    static class ArcSyncEngine
    {
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcSyncSwitch");
        public static readonly string ConfigPath = Path.Combine(DataDir, "config.json");
        public static readonly string LogPath = Path.Combine(DataDir, "apply.log");

        public static AppConfig LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                    return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
            }
            catch { }
            return new AppConfig();
        }

        public static void SaveConfig(AppConfig cfg)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static void Log(string msg)
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {msg}{Environment.NewLine}");
            }
            catch { }
        }

        public static DriverSnapshot Query()
        {
            var snap = new DriverSnapshot();
            try
            {
                QueryControlLib(snap);
            }
            catch (Exception ex)
            {
                snap.Error = ex.Message;
            }

            try
            {
                var lfc = DirectVrrEscape.Invoke(0);
                if (lfc.NtStatus == 0)
                {
                    snap.D3dOk = true;
                    snap.LowFpsEnabled = lfc.LowFpsSolutionEnabled;
                    snap.HighFpsEnabled = lfc.HighFpsSolutionEnabled;
                }
                else if (string.IsNullOrEmpty(snap.Error))
                {
                    snap.Error = $"D3DKMT 0x{lfc.NtStatus:X8}";
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(snap.Error)) snap.Error = ex.Message;
            }
            return snap;
        }

        public static (bool ok, string message, DriverSnapshot snapshot) Apply(VrrMode mode, bool lfcEnabled, int maxAttempts = 3, int retryDelayMs = 600)
        {
            string lastErr = "";
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    int ctlRes = SetControlLibProfile((int)mode);
                    if (ctlRes != 0)
                    {
                        lastErr = $"ControlLib 0x{(uint)ctlRes:X8}";
                        if (attempt < maxAttempts) { Thread.Sleep(retryDelayMs); continue; }
                        return (false, lastErr, Query());
                    }

                    uint lowOp = lfcEnabled ? 3u : 4u;
                    uint highOp = lfcEnabled ? 5u : 6u;
                    var r1 = DirectVrrEscape.Invoke(lowOp);
                    var r2 = DirectVrrEscape.Invoke(highOp);
                    if (r1.NtStatus != 0 || r2.NtStatus != 0)
                    {
                        lastErr = $"D3DKMT 0x{r1.NtStatus:X8}/0x{r2.NtStatus:X8}";
                        if (attempt < maxAttempts) { Thread.Sleep(retryDelayMs); continue; }
                        return (false, lastErr, Query());
                    }

                    Thread.Sleep(250);
                    var snap = Query();
                    return (snap.ControlLibOk && snap.D3dOk, snap.Error, snap);
                }
                catch (Exception ex)
                {
                    lastErr = ex.Message;
                    if (attempt < maxAttempts) Thread.Sleep(retryDelayMs);
                }
            }
            return (false, lastErr, Query());
        }

        // ---- ControlLib.dll P/Invoke ----
        [StructLayout(LayoutKind.Sequential)]
        struct CtlApplicationId
        {
            public uint Data1;
            public ushort Data2;
            public ushort Data3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] Data4;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct CtlInitArgs
        {
            public uint Size;
            public byte Version;
            public uint AppVersion;
            public uint Flags;
            public uint SupportedVersion;
            public CtlApplicationId ApplicationUid;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ArcSyncMonitorParams
        {
            public uint Size;
            public byte Version;
            [MarshalAs(UnmanagedType.U1)] public bool IsSupported;
            public float MinimumRefreshRateInHz;
            public float MaximumRefreshRateInHz;
            public uint MaxFrameTimeIncreaseInUs;
            public uint MaxFrameTimeDecreaseInUs;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ArcSyncProfileParams
        {
            public uint Size;
            public byte Version;
            public int Profile;
            public float MaxRefreshRateInHz;
            public float MinRefreshRateInHz;
            public uint MaxFrameTimeIncreaseInUs;
            public uint MaxFrameTimeDecreaseInUs;
        }

        [DllImport("ControlLib.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ctlInit(ref CtlInitArgs args, out IntPtr api);
        [DllImport("ControlLib.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ctlClose(IntPtr api);
        [DllImport("ControlLib.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ctlEnumerateDevices(IntPtr api, ref uint count, IntPtr devices);
        [DllImport("ControlLib.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ctlEnumerateDisplayOutputs(IntPtr adapter, ref uint count, IntPtr displays);
        [DllImport("ControlLib.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ctlGetIntelArcSyncInfoForMonitor(IntPtr display, ref ArcSyncMonitorParams p);
        [DllImport("ControlLib.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ctlGetIntelArcSyncProfile(IntPtr display, ref ArcSyncProfileParams p);
        [DllImport("ControlLib.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int ctlSetIntelArcSyncProfile(IntPtr display, ref ArcSyncProfileParams p);

        static string GetProfileName(int profile)
        {
            string[] names = { "INVALID", "RECOMMENDED", "EXCELLENT", "GOOD", "COMPATIBLE", "OFF", "VESA", "CUSTOM" };
            return profile >= 0 && profile < names.Length ? names[profile] : profile.ToString();
        }

        static CtlInitArgs NewInitArgs() => new()
        {
            Size = (uint)Marshal.SizeOf<CtlInitArgs>(),
            Version = 0,
            AppVersion = 0x00010001,
            ApplicationUid = new CtlApplicationId { Data4 = new byte[8] }
        };

        static void WithPrimaryArcSyncDisplay(Action<IntPtr> action)
        {
            var init = NewInitArgs();
            int r = ctlInit(ref init, out IntPtr api);
            if (r != 0) throw new InvalidOperationException($"ControlLib init: 0x{(uint)r:X8}");

            IntPtr adapters = IntPtr.Zero;
            try
            {
                uint ac = 0;
                r = ctlEnumerateDevices(api, ref ac, IntPtr.Zero);
                if (r != 0 || ac == 0) throw new InvalidOperationException(Lang.S("ErrNoAdapter"));
                adapters = Marshal.AllocHGlobal((int)ac * IntPtr.Size);
                ctlEnumerateDevices(api, ref ac, adapters);

                for (int a = 0; a < ac; a++)
                {
                    IntPtr adp = Marshal.ReadIntPtr(adapters, a * IntPtr.Size);
                    uint dc = 0;
                    if (ctlEnumerateDisplayOutputs(adp, ref dc, IntPtr.Zero) != 0 || dc == 0) continue;
                    IntPtr displays = Marshal.AllocHGlobal((int)dc * IntPtr.Size);
                    try
                    {
                        if (ctlEnumerateDisplayOutputs(adp, ref dc, displays) != 0) continue;
                        for (int d = 0; d < dc; d++)
                        {
                            IntPtr disp = Marshal.ReadIntPtr(displays, d * IntPtr.Size);
                            var m = new ArcSyncMonitorParams { Size = (uint)Marshal.SizeOf<ArcSyncMonitorParams>() };
                            if (ctlGetIntelArcSyncInfoForMonitor(disp, ref m) == 0 && m.IsSupported)
                            {
                                action(disp);
                                return;
                            }
                        }
                    }
                    finally { Marshal.FreeHGlobal(displays); }
                }
                throw new InvalidOperationException(Lang.S("ErrNoArcDisplay"));
            }
            finally
            {
                if (adapters != IntPtr.Zero) Marshal.FreeHGlobal(adapters);
                ctlClose(api);
            }
        }

        static void QueryControlLib(DriverSnapshot snap)
        {
            WithPrimaryArcSyncDisplay(disp =>
            {
                var m = new ArcSyncMonitorParams { Size = (uint)Marshal.SizeOf<ArcSyncMonitorParams>() };
                int mr = ctlGetIntelArcSyncInfoForMonitor(disp, ref m);
                var p = new ArcSyncProfileParams { Size = (uint)Marshal.SizeOf<ArcSyncProfileParams>() };
                int pr = ctlGetIntelArcSyncProfile(disp, ref p);
                if (mr != 0 || pr != 0) throw new InvalidOperationException($"ControlLib query: 0x{(uint)mr:X8}/0x{(uint)pr:X8}");

                snap.ControlLibOk = true;
                snap.MonMinHz = m.MinimumRefreshRateInHz;
                snap.MonMaxHz = m.MaximumRefreshRateInHz;
                snap.ProfileId = p.Profile;
                snap.ProfileName = GetProfileName(p.Profile);
                snap.ActiveMinHz = p.MinRefreshRateInHz;
                snap.ActiveMaxHz = p.MaxRefreshRateInHz;
                snap.IncUs = p.MaxFrameTimeIncreaseInUs;
                snap.DecUs = p.MaxFrameTimeDecreaseInUs;
            });
        }

        static int SetControlLibProfile(int profileId)
        {
            int sr = 0;
            WithPrimaryArcSyncDisplay(disp =>
            {
                var p = new ArcSyncProfileParams
                {
                    Size = (uint)Marshal.SizeOf<ArcSyncProfileParams>(),
                    Version = 0,
                    Profile = profileId
                };
                sr = ctlSetIntelArcSyncProfile(disp, ref p);
            });
            return sr;
        }

        // ---- Direct Intel D3DKMT VRR/LFC escape ----
        sealed class VrrState
        {
            public uint NtStatus;
            public bool Supported;
            public bool Enabled;
            public bool HighFpsSolutionEnabled;
            public bool LowFpsSolutionEnabled;
        }

        static class DirectVrrEscape
        {
            const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x1;
            const uint D3DKMT_ESCAPE_DRIVERPRIVATE = 0;
            const int PrivateDataSize = 124;
            const int HeaderSize = 16;
            const uint DisplayEscapeMajor = 1;
            const uint VrrEscapeMinor = 10;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            struct DISPLAY_DEVICE
            {
                public int cb;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
                public int StateFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct LUID { public uint LowPart; public int HighPart; }

            [StructLayout(LayoutKind.Sequential)]
            struct D3DKMT_OPENADAPTERFROMHDC
            {
                public IntPtr hDc;
                public uint hAdapter;
                public LUID AdapterLuid;
                public uint VidPnSourceId;
            }

            [StructLayout(LayoutKind.Sequential)]
            struct D3DKMT_CLOSEADAPTER { public uint hAdapter; }

            [StructLayout(LayoutKind.Sequential)]
            struct D3DKMT_ESCAPE
            {
                public uint hAdapter;
                public uint hDevice;
                public uint Type;
                public uint Flags;
                public IntPtr pPrivateDriverData;
                public uint PrivateDriverDataSize;
                public uint hContext;
            }

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            static extern bool EnumDisplayDevices(string? lpDevice, int iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, int dwFlags);
            [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
            static extern IntPtr CreateDC(string? lpszDriver, string lpszDevice, string? lpszOutput, IntPtr lpInitData);
            [DllImport("gdi32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool DeleteDC(IntPtr hdc);
            [DllImport("gdi32.dll")]
            static extern int D3DKMTOpenAdapterFromHdc(ref D3DKMT_OPENADAPTERFROMHDC data);
            [DllImport("gdi32.dll")]
            static extern int D3DKMTCloseAdapter(ref D3DKMT_CLOSEADAPTER data);
            [DllImport("gdi32.dll")]
            static extern int D3DKMTEscape(ref D3DKMT_ESCAPE data);

            static string GetActiveIntelDisplay()
            {
                var matches = new List<string>();
                for (int i = 0; ; i++)
                {
                    var dev = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
                    if (!EnumDisplayDevices(null, i, ref dev, 0)) break;
                    if ((dev.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) == 0) continue;
                    if (!string.IsNullOrEmpty(dev.DeviceID) && dev.DeviceID.Contains("VEN_8086", StringComparison.OrdinalIgnoreCase))
                        matches.Add(dev.DeviceName);
                }
                if (matches.Count == 0) throw new InvalidOperationException(Lang.S("ErrNoActiveDisplay"));
                return matches[0];
            }

            public static VrrState Invoke(uint operation)
            {
                string displayName = GetActiveIntelDisplay();
                IntPtr hdc = IntPtr.Zero;
                uint adapterHandle = 0;
                IntPtr privateData = IntPtr.Zero;
                try
                {
                    hdc = CreateDC(null, displayName, null, IntPtr.Zero);
                    if (hdc == IntPtr.Zero) throw new InvalidOperationException("CreateDC failed");
                    var open = new D3DKMT_OPENADAPTERFROMHDC { hDc = hdc };
                    int openStatus = D3DKMTOpenAdapterFromHdc(ref open);
                    if (openStatus != 0 || open.hAdapter == 0)
                        throw new InvalidOperationException($"D3DKMTOpenAdapter: 0x{(uint)openStatus:X8}");
                    adapterHandle = open.hAdapter;

                    var payload = new byte[PrivateDataSize];
                    Buffer.BlockCopy(BitConverter.GetBytes(DisplayEscapeMajor), 0, payload, 8, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(VrrEscapeMinor), 0, payload, 12, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(operation), 0, payload, HeaderSize, 4);

                    privateData = Marshal.AllocHGlobal(payload.Length);
                    Marshal.Copy(payload, 0, privateData, payload.Length);
                    var escape = new D3DKMT_ESCAPE
                    {
                        hAdapter = adapterHandle,
                        Type = D3DKMT_ESCAPE_DRIVERPRIVATE,
                        pPrivateDriverData = privateData,
                        PrivateDriverDataSize = (uint)payload.Length
                    };
                    uint ntStatus = unchecked((uint)D3DKMTEscape(ref escape));
                    Marshal.Copy(privateData, payload, 0, payload.Length);

                    var state = new VrrState { NtStatus = ntStatus };
                    if (ntStatus == 0)
                    {
                        state.Supported = payload[20] == 1;
                        state.Enabled = payload[21] == 1;
                        state.HighFpsSolutionEnabled = payload[22] == 1;
                        state.LowFpsSolutionEnabled = payload[23] == 1;
                    }
                    return state;
                }
                finally
                {
                    if (privateData != IntPtr.Zero) Marshal.FreeHGlobal(privateData);
                    if (adapterHandle != 0)
                    {
                        var close = new D3DKMT_CLOSEADAPTER { hAdapter = adapterHandle };
                        D3DKMTCloseAdapter(ref close);
                    }
                    if (hdc != IntPtr.Zero) DeleteDC(hdc);
                }
            }
        }
    }
}
