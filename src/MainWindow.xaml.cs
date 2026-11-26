using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace ArcSyncSwitch
{
    public partial class MainWindow : Window
    {
        bool _suppressEvents;

        public MainWindow(bool previewOnly = false)
        {
            InitializeComponent();
            if (!previewOnly)
            {
                Loaded += (_, _) => { FitToScreen(); RefreshFromDriver(); };
                SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
                Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            }
        }

        public void SetPreviewState()
        {
            _suppressEvents = true;
            ModeFull.IsChecked = true;
            LfcSwitch.IsChecked = true;
            AutostartSwitch.IsChecked = false;
            ApplyLangRadios(Lang.Preference);
            HeaderStatus.Text = Lang.S("PreviewHeader");
            ModeDescription.Text = Lang.S("PreviewModeDesc");
            LfcSub.Text = Lang.S("PreviewLfcSub");
            FooterStatus.Text = Lang.S("PreviewFooter");
            _suppressEvents = false;
        }

        public void OnLanguageChanged() => RefreshFromDriver();

        void ApplyLangRadios(string? pref)
        {
            string norm = Lang.Normalize(pref);
            LangAuto.IsChecked = norm == "auto";
            LangRu.IsChecked = norm == "ru";
            LangEn.IsChecked = norm == "en";
        }

        string SelectedLang()
        {
            if (LangRu.IsChecked == true) return "ru";
            if (LangEn.IsChecked == true) return "en";
            return "auto";
        }

        void OnDisplayChanged(object? sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(FitToScreen));
        }

        void FitToScreen()
        {
            Rect wa = SystemParameters.WorkArea;
            Width = Math.Min(500, wa.Width);
            UpdateLayout();
            Left = wa.Left + Math.Max(0, (wa.Width - ActualWidth) / 2);
            Top = wa.Top + Math.Max(0, (wa.Height - ActualHeight) / 2);
        }

        void RefreshFromDriver(string? note = null, bool isError = false)
        {
            var snap = ArcSyncEngine.Query();
            var cfg = ArcSyncEngine.LoadConfig();
            ApplySnapshotToUi(snap, cfg, note, isError);
        }

        void ApplySnapshotToUi(DriverSnapshot snap, AppConfig cfg, string? note = null, bool isError = false)
        {
            _suppressEvents = true;
            try
            {
                var mode = snap.DetectedMode ?? cfg.Mode;
                ModeFull.IsChecked = mode == VrrMode.Full;
                ModeStock.IsChecked = mode == VrrMode.Stock;
                ModeOff.IsChecked = mode == VrrMode.Off;

                bool lfc = snap.D3dOk ? snap.LfcEnabled : cfg.LfcEnabled;
                LfcSwitch.IsChecked = lfc;
                AutostartSwitch.IsChecked = LogonTask.Exists();
                ApplyLangRadios(cfg.Language);

                if (snap.ControlLibOk)
                {
                    string hz = Lang.S("Hz");
                    string range = string.Format(Lang.Culture, "{0:0.#}..{1:0.#} {2}", snap.ActiveMinHz, snap.ActiveMaxHz, hz);
                    string lfcShort = snap.D3dOk ? Lang.S(snap.LfcEnabled ? "LfcOn" : "LfcOff") : Lang.S("LfcNa");
                    HeaderStatus.Text = $"{range} · {lfcShort}";
                    ModeDescription.Text = Lang.F("DriverDescFmt", snap.ProfileName, range, snap.IncUs, snap.MonMinHz, snap.MonMaxHz);
                }
                else
                {
                    HeaderStatus.Text = Lang.S("NoDriverHeader");
                    ModeDescription.Text = string.IsNullOrEmpty(snap.Error)
                        ? Lang.S("NoDriverDesc")
                        : snap.Error;
                }

                LfcSub.Text = snap.D3dOk
                    ? Lang.F("D3dStateFmt", Lang.S(snap.LowFpsEnabled ? "OnShort" : "OffShort"), Lang.S(snap.HighFpsEnabled ? "OnShort" : "OffShort"))
                    : Lang.S("D3dNa");

                if (!string.IsNullOrEmpty(note))
                {
                    FooterStatus.Text = note;
                    FooterStatus.SetResourceReference(ForegroundProperty, isError ? "Danger" : "Ok");
                }
                else if (!string.IsNullOrEmpty(snap.Error))
                {
                    FooterStatus.Text = snap.Error;
                    FooterStatus.SetResourceReference(ForegroundProperty, "Danger");
                }
                else
                {
                    FooterStatus.Text = Lang.S("Synced");
                    FooterStatus.SetResourceReference(ForegroundProperty, "Sub");
                }
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        VrrMode SelectedMode()
        {
            if (ModeStock.IsChecked == true) return VrrMode.Stock;
            if (ModeOff.IsChecked == true) return VrrMode.Off;
            return VrrMode.Full;
        }

        void ApplyCurrentSelection()
        {
            var cfg = new AppConfig
            {
                Mode = SelectedMode(),
                LfcEnabled = LfcSwitch.IsChecked == true,
                Language = SelectedLang()
            };
            ArcSyncEngine.SaveConfig(cfg);
            var (ok, err, snap) = ArcSyncEngine.Apply(cfg.Mode, cfg.LfcEnabled);
            string note = ok
                ? Lang.F("AppliedFmt", snap.ProfileName, snap.ActiveMinHz, snap.ActiveMaxHz)
                : Lang.F("ErrorFmt", err);
            ArcSyncEngine.Log($"UI apply mode={cfg.Mode} lfc={cfg.LfcEnabled} ok={ok} err={err}");
            ApplySnapshotToUi(snap, cfg, note, isError: !ok);
        }

        void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            ApplyCurrentSelection();
        }

        void LfcSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            ApplyCurrentSelection();
        }

        void Lang_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            var cfg = ArcSyncEngine.LoadConfig();
            cfg.Language = SelectedLang();
            ArcSyncEngine.SaveConfig(cfg);
            Lang.Apply(Application.Current.Resources, cfg.Language);
            if (LogonTask.Exists())
            {
                string exe = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(exe))
                    LogonTask.Create(exe, "--apply", Lang.S("TaskDesc"));
            }
            RefreshFromDriver();
        }

        void AutostartSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            bool want = AutostartSwitch.IsChecked == true;
            if (want)
            {
                string exe = Environment.ProcessPath ?? "";
                bool ok = !string.IsNullOrEmpty(exe) && LogonTask.Create(exe, "--apply", Lang.S("TaskDesc"));
                if (!ok)
                {
                    _suppressEvents = true;
                    AutostartSwitch.IsChecked = false;
                    _suppressEvents = false;
                    FooterStatus.Text = Lang.S("TaskError");
                    FooterStatus.SetResourceReference(ForegroundProperty, "Danger");
                    return;
                }
                FooterStatus.Text = Lang.S("AutostartOn");
                FooterStatus.SetResourceReference(ForegroundProperty, "Ok");
            }
            else
            {
                LogonTask.Delete();
                FooterStatus.Text = Lang.S("AutostartOff");
                FooterStatus.SetResourceReference(ForegroundProperty, "Sub");
            }
        }

        void RefreshBtn_Click(object sender, RoutedEventArgs e) => RefreshFromDriver();
        void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();
        void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
