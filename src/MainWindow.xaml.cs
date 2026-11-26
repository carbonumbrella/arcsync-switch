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
            HeaderStatus.Text = "48..120 Гц · LFC вкл";
            ModeDescription.Text = "Профиль драйвера: EXCELLENT (48..120 Гц, шаг 0 мкс). Паспорт экрана: 48..120 Гц.";
            LfcSub.Text = "Флаги D3DKMT активны (LowFps: вкл, HighFps: вкл)";
            FooterStatus.Text = "Готово к работе";
            _suppressEvents = false;
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

        void RefreshFromDriver(string? note = null)
        {
            var snap = ArcSyncEngine.Query();
            var cfg = ArcSyncEngine.LoadConfig();
            ApplySnapshotToUi(snap, cfg, note);
        }

        void ApplySnapshotToUi(DriverSnapshot snap, AppConfig cfg, string? note = null)
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

                if (snap.ControlLibOk)
                {
                    string range = $"{snap.ActiveMinHz:0.#}..{snap.ActiveMaxHz:0.#} Гц";
                    string lfcShort = snap.D3dOk ? (snap.LfcEnabled ? "LFC вкл" : "LFC выкл") : "LFC н/д";
                    HeaderStatus.Text = $"{range} · {lfcShort}";
                    ModeDescription.Text =
                        $"Профиль драйвера: {snap.ProfileName} ({range}, шаг {snap.IncUs} мкс). Паспорт матрицы: {snap.MonMinHz:0.#}..{snap.MonMaxHz:0.#} Гц.";
                }
                else
                {
                    HeaderStatus.Text = "Нет данных драйвера";
                    ModeDescription.Text = string.IsNullOrEmpty(snap.Error)
                        ? "Не удалось прочитать профиль Intel Arc Sync."
                        : snap.Error;
                }

                LfcSub.Text = snap.D3dOk
                    ? $"Флаги D3DKMT: LowFps {(snap.LowFpsEnabled ? "вкл" : "выкл")}, HighFps {(snap.HighFpsEnabled ? "вкл" : "выкл")}"
                    : "Интерфейс D3DKMT недоступен";

                if (!string.IsNullOrEmpty(note))
                {
                    FooterStatus.Text = note;
                    FooterStatus.SetResourceReference(ForegroundProperty,
                        note.StartsWith("Ошибка") ? "Danger" : "Ok");
                }
                else if (!string.IsNullOrEmpty(snap.Error))
                {
                    FooterStatus.Text = snap.Error;
                    FooterStatus.SetResourceReference(ForegroundProperty, "Danger");
                }
                else
                {
                    FooterStatus.Text = "Состояние синхронизировано с драйвером";
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
                LfcEnabled = LfcSwitch.IsChecked == true
            };
            ArcSyncEngine.SaveConfig(cfg);
            var (ok, err, snap) = ArcSyncEngine.Apply(cfg.Mode, cfg.LfcEnabled);
            string note = ok
                ? $"Применено: {snap.ProfileName} ({snap.ActiveMinHz:0.#}..{snap.ActiveMaxHz:0.#} Гц)"
                : $"Ошибка: {err}";
            ArcSyncEngine.Log($"UI apply mode={cfg.Mode} lfc={cfg.LfcEnabled} ok={ok} err={err}");
            ApplySnapshotToUi(snap, cfg, note);
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

        void AutostartSwitch_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressEvents) return;
            bool want = AutostartSwitch.IsChecked == true;
            if (want)
            {
                string exe = Environment.ProcessPath ?? "";
                bool ok = !string.IsNullOrEmpty(exe) && LogonTask.Create(
                    exe,
                    "--apply",
                    "Восстанавливает профиль Intel Arc Sync и состояние LFC при входе в систему");
                if (!ok)
                {
                    _suppressEvents = true;
                    AutostartSwitch.IsChecked = false;
                    _suppressEvents = false;
                    FooterStatus.Text = "Ошибка: не удалось создать задачу планировщика";
                    FooterStatus.SetResourceReference(ForegroundProperty, "Danger");
                    return;
                }
                FooterStatus.Text = "Автозапуск включён (задача ArcSyncSwitch)";
                FooterStatus.SetResourceReference(ForegroundProperty, "Ok");
            }
            else
            {
                LogonTask.Delete();
                FooterStatus.Text = "Автозапуск отключён";
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
