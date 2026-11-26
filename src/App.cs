using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ArcSyncSwitch
{
    public sealed class App : Application
    {
        const string ShowEventName = "Local\\ArcSyncSwitch.Show";
        const string QuitEventName = "Local\\ArcSyncSwitch.Quit";
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);

        [STAThread]
        public static int Main(string[] args)
        {
            if (Array.IndexOf(args, "--quit") >= 0)
            {
                if (EventWaitHandle.TryOpenExisting(QuitEventName, out var q)) q.Set();
                return 0;
            }

            int ai = Array.IndexOf(args, "--set-autostart");
            if (ai >= 0 && ai + 1 < args.Length)
            {
                if (args[ai + 1] == "on")
                {
                    Lang.Apply(null, ArcSyncEngine.LoadConfig().Language);
                    string exe = Environment.ProcessPath ?? "";
                    if (!string.IsNullOrEmpty(exe))
                        return LogonTask.Create(exe, "--apply", Lang.S("TaskDesc")) ? 0 : 1;
                    return 1;
                }
                LogonTask.Delete();
                return 0;
            }

            if (Array.IndexOf(args, "--apply") >= 0)
            {
                var cfg = ArcSyncEngine.LoadConfig();
                var (ok, err, snap) = ArcSyncEngine.Apply(cfg.Mode, cfg.LfcEnabled, maxAttempts: 20, retryDelayMs: 1000);
                ArcSyncEngine.Log($"Startup --apply mode={cfg.Mode} lfc={cfg.LfcEnabled} ok={ok} active={snap.ProfileName}({snap.ActiveMinHz}-{snap.ActiveMaxHz}) err={err}");
                return ok ? 0 : 1;
            }

            int ri = Array.IndexOf(args, "--render");
            if (ri >= 0 && ri + 1 < args.Length)
            {
                bool? forceLight = null;
                if (Array.IndexOf(args, "--light") >= 0) forceLight = true;
                else if (Array.IndexOf(args, "--dark") >= 0) forceLight = false;
                string? forceLang = null;
                if (Array.IndexOf(args, "--ru") >= 0) forceLang = "ru";
                else if (Array.IndexOf(args, "--en") >= 0) forceLang = "en";
                RenderPreview(args[ri + 1], forceLight, forceLang);
                return 0;
            }

            using var mutex = new Mutex(true, "Local\\ArcSyncSwitch", out bool first);
            if (!first)
            {
                AllowSetForegroundWindow(-1);
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var ev)) ev.Set();
                return 0;
            }

            var app = new App();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/ArcSyncSwitch;component/Theme.xaml")
            });
            Theme.Apply(app.Resources);
            Lang.Apply(app.Resources, ArcSyncEngine.LoadConfig().Language);

            var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            var quitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, QuitEventName);
            var win = new MainWindow();
            app.MainWindow = win;
            SystemEvents.UserPreferenceChanged += (_, e) =>
                app.Dispatcher.BeginInvoke(() =>
                {
                    Theme.Apply(app.Resources);
                    if (e.Category == UserPreferenceCategory.Locale || Lang.Preference == "auto")
                    {
                        if (Lang.Apply(app.Resources, ArcSyncEngine.LoadConfig().Language))
                            win.OnLanguageChanged();
                    }
                });

            var waiter = new Thread(() =>
            {
                var handles = new WaitHandle[] { showEvent, quitEvent };
                while (true)
                {
                    int idx = WaitHandle.WaitAny(handles);
                    if (idx == 1)
                    {
                        app.Dispatcher.BeginInvoke(() => app.Shutdown());
                        return;
                    }
                    app.Dispatcher.BeginInvoke(() =>
                    {
                        if (win.WindowState == WindowState.Minimized) win.WindowState = WindowState.Normal;
                        win.Topmost = true;
                        win.Show();
                        win.Activate();
                        win.Topmost = false;
                    });
                }
            }) { IsBackground = true };
            waiter.Start();

            win.Show();
            return app.Run();
        }

        static void RenderPreview(string outputPath, bool? forceLight, string? forceLang = null)
        {
            var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext());
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/ArcSyncSwitch;component/Theme.xaml")
            });
            Theme.Apply(app.Resources, forceLight);
            Lang.Apply(app.Resources, forceLang ?? ArcSyncEngine.LoadConfig().Language);
            var win = new MainWindow(previewOnly: true);
            win.SetPreviewState();
            var root = (FrameworkElement)win.Content;
            win.Content = null;
            var holder = new System.Windows.Controls.Border
            {
                Background = Brushes.Transparent,
                Child = root,
                Width = 500
            };
            holder.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty, "Fg");
            holder.SetValue(System.Windows.Documents.TextElement.FontFamilyProperty, new FontFamily("Segoe UI Variable Text, Segoe UI"));
            holder.SetValue(System.Windows.Documents.TextElement.FontSizeProperty, 13.0);
            holder.Measure(new Size(500, double.PositiveInfinity));
            holder.Arrange(new Rect(holder.DesiredSize));
            holder.UpdateLayout();

            int w = (int)Math.Ceiling(holder.ActualWidth * 1.5);
            int h = (int)Math.Ceiling(holder.ActualHeight * 1.5);
            var rtb = new RenderTargetBitmap(w, h, 144, 144, PixelFormats.Pbgra32);
            rtb.Render(holder);

            string? dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(outputPath);
            enc.Save(fs);
        }
    }
}
