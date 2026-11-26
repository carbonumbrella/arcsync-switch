using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

namespace ArcSyncSwitch
{
    static class Lang
    {
        public static string Preference { get; private set; } = "auto";
        public static bool IsRu { get; private set; } = DetectOsRu();
        public static CultureInfo Culture => CultureInfo.GetCultureInfo(IsRu ? "ru-RU" : "en-US");

        public static string Normalize(string? pref)
        {
            if (string.Equals(pref, "ru", StringComparison.OrdinalIgnoreCase)) return "ru";
            if (string.Equals(pref, "en", StringComparison.OrdinalIgnoreCase)) return "en";
            return "auto";
        }

        static bool DetectOsRu()
        {
            CultureInfo.CurrentUICulture.ClearCachedData();
            return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase);
        }

        public static bool Apply(ResourceDictionary? res, string? pref)
        {
            string norm = Normalize(pref);
            bool nextRu = norm == "ru" || (norm == "auto" && DetectOsRu());
            bool changed = nextRu != IsRu || norm != Preference;
            Preference = norm;
            IsRu = nextRu;
            if (res != null)
            {
                foreach (var kv in Strings)
                    res["Str_" + kv.Key] = nextRu ? kv.Value.Ru : kv.Value.En;
            }
            return changed;
        }

        public static string S(string key) =>
            Strings.TryGetValue(key, out var pair) ? (IsRu ? pair.Ru : pair.En) : key;

        public static string F(string key, params object[] args) =>
            string.Format(Culture, S(key), args);

        static readonly Dictionary<string, (string Ru, string En)> Strings = new()
        {
            ["CloseTip"] = ("Закрыть", "Close"),
            ["SecVrr"] = ("ПРОФИЛЬ VRR (INTEL ARC SYNC)", "VRR PROFILE (INTEL ARC SYNC)"),
            ["TipVrr"] = (
                "Переключает рабочий профиль драйвера Intel напрямую через ControlLib.dll, не меняя EDID (разрешения 1600x1000 и 1536x960 сохраняются). Полный (48..120): снимает зажим 60 Гц. Сток (60..120): заводской режим Intel. Выкл: фиксированные 120 Гц без VRR. Важно для ClawTweaks: если в профиле игры в ClawTweaks включён тумблер VRR, он может сбрасывать режим обратно на стоковые 60..120 при запуске игры. Частоту (48/60/75/120) меняйте в ClawTweaks, а сам тумблер VRR там лучше не трогать.",
                "Switches the Intel driver VRR profile directly via ControlLib.dll without altering EDID (1600x1000 and 1536x960 resolutions stay intact). Full (48..120): removes the 60 Hz floor lock. Stock (60..120): Intel default range. Off: fixed 120 Hz without VRR. Note for ClawTweaks: if VRR toggle is enabled in a game profile there, it may reset the range back to Stock 60..120 on launch. Adjust frame rate limits (48/60/75/120) in ClawTweaks, but leave its VRR toggle untouched."),
            ["ModeFull"] = ("Полный 48..120", "Full 48..120"),
            ["ModeStock"] = ("Сток 60..120", "Stock 60..120"),
            ["ModeOff"] = ("Выкл (120)", "Off (120)"),
            ["SecDriver"] = ("ПАРАМЕТРЫ ДРАЙВЕРА", "DRIVER SETTINGS"),
            ["LfcTitle"] = ("Компенсация низкого FPS (LFC)", "Low Framerate Compensation (LFC)"),
            ["TipLfc"] = (
                "Управляет внутренними флагами драйвера Intel (LowFpsSolutionEnabled и HighFpsSolutionEnabled) через D3DKMT. Включено: при падении ниже 48 FPS драйвер удваивает циклы обновления экрана (например 35 FPS идут на 70 Гц) для плавности. Выключено: убирает возможные x2 скачки драйвера внутри рабочего коридора 48..120 Гц, но при реальном падении ниже 48 FPS удвоения не будет.",
                "Controls internal Intel driver D3DKMT flags (LowFpsSolutionEnabled and HighFpsSolutionEnabled). Enabled: below 48 FPS, the driver doubles refresh cycles (for example, 35 FPS runs at 70 Hz) for smoother motion. Disabled: avoids driver x2 jumps near the lower VRR boundary inside 48..120 Hz, with no doubling below 48 FPS."),
            ["AutostartTitle"] = ("Применять при входе в Windows", "Apply on Windows logon"),
            ["TipAutostart"] = (
                "Драйвер Intel сбрасывает профиль EXCELLENT и флаги LFC после перезагрузки или обновления драйвера. Включение создаёт задачу планировщика (без прав администратора, работает и в полноэкранной оболочке Xbox), которая при входе в систему тихо применяет выбранный режим (--apply) и сразу завершает работу с нулевой нагрузкой в фоне.",
                "The Intel driver resets the EXCELLENT profile and LFC flags after a reboot or driver update. Enabling this creates a standard scheduled task (no admin rights, works in Xbox fullscreen shell) that silently applies your chosen mode (--apply) at logon and exits immediately with zero background overhead."),
            ["AutostartSub"] = (
                "Тихо восстанавливает режим при запуске системы и сразу закрывается",
                "Silently restores the selected mode at startup and exits"),
            ["LangTitle"] = ("Язык", "Language"),
            ["TipLang"] = (
                "Авто выбирает язык интерфейса Windows (русский для русской системы, английский для остальных). Можно выбрать язык вручную.",
                "Auto follows the Windows display language (Russian on Russian Windows, English otherwise), or you can force RU/EN here."),
            ["LangAuto"] = ("Авто", "Auto"),
            ["Refresh"] = ("Обновить", "Refresh"),

            ["PreviewHeader"] = ("48..120 Гц · LFC вкл", "48..120 Hz · LFC on"),
            ["PreviewModeDesc"] = (
                "Профиль драйвера: EXCELLENT (48..120 Гц, шаг 0 мкс). Паспорт экрана: 48..120 Гц.",
                "Driver profile: EXCELLENT (48..120 Hz, step 0 us). Panel EDID: 48..120 Hz."),
            ["PreviewLfcSub"] = (
                "Флаги D3DKMT активны (LowFps: вкл, HighFps: вкл)",
                "D3DKMT flags active (LowFps: on, HighFps: on)"),
            ["PreviewFooter"] = ("Готово к работе", "Ready"),

            ["Hz"] = ("Гц", "Hz"),
            ["LfcOn"] = ("LFC вкл", "LFC on"),
            ["LfcOff"] = ("LFC выкл", "LFC off"),
            ["LfcNa"] = ("LFC н/д", "LFC n/a"),
            ["DriverDescFmt"] = (
                "Профиль драйвера: {0} ({1}, шаг {2} мкс). Паспорт матрицы: {3}..{4} Гц.",
                "Driver profile: {0} ({1}, step {2} us). Panel EDID: {3}..{4} Hz."),
            ["NoDriverHeader"] = ("Нет данных драйвера", "No driver data"),
            ["NoDriverDesc"] = ("Не удалось прочитать профиль Intel Arc Sync.", "Could not read the Intel Arc Sync profile."),
            ["D3dStateFmt"] = (
                "Флаги D3DKMT: LowFps {0}, HighFps {1}",
                "D3DKMT flags: LowFps {0}, HighFps {1}"),
            ["OnShort"] = ("вкл", "on"),
            ["OffShort"] = ("выкл", "off"),
            ["D3dNa"] = ("Интерфейс D3DKMT недоступен", "D3DKMT interface unavailable"),
            ["Synced"] = ("Состояние синхронизировано с драйвером", "Synced with driver"),
            ["AppliedFmt"] = ("Применено: {0} ({1}..{2} Гц)", "Applied: {0} ({1}..{2} Hz)"),
            ["ErrorFmt"] = ("Ошибка: {0}", "Error: {0}"),
            ["TaskDesc"] = (
                "Восстанавливает профиль Intel Arc Sync и состояние LFC при входе в систему",
                "Restores Intel Arc Sync profile and LFC state at Windows logon"),
            ["TaskError"] = (
                "Ошибка: не удалось создать задачу планировщика",
                "Error: could not create scheduled task"),
            ["AutostartOn"] = (
                "Автозапуск включён (задача ArcSyncSwitch)",
                "Autostart enabled (ArcSyncSwitch task)"),
            ["AutostartOff"] = ("Автозапуск отключён", "Autostart disabled"),

            ["ErrNoAdapter"] = ("Адаптер Intel не найден", "Intel adapter not found"),
            ["ErrNoArcDisplay"] = (
                "Дисплей с поддержкой Intel Arc Sync не найден (проверьте родное разрешение)",
                "No Intel Arc Sync display found (check native resolution)"),
            ["ErrNoActiveDisplay"] = ("Активный экран Intel не найден", "No active Intel display found")
        };
    }
}
