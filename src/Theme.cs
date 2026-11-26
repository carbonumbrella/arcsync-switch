using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace ArcSyncSwitch
{
    public static class Theme
    {
        public static bool Light
        {
            get
            {
                try { return (int)(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) ?? 0) == 1; }
                catch { return false; }
            }
        }

        public static void Apply(ResourceDictionary r, bool? forceLight = null)
        {
            bool isLight = forceLight ?? Light;
            void Set(string key, string hex) => r[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            if (isLight)
            {
                Set("Bg", "#F7F7F8"); Set("Fg", "#1B1B1F"); Set("Sub", "#6B6B73");
                Set("Card", "#FFFFFF"); Set("Border", "#E3E3E8"); Set("Ghost", "#E8E8ED");
            }
            else
            {
                Set("Bg", "#202024"); Set("Fg", "#F3F3F5"); Set("Sub", "#A4A4AE");
                Set("Card", "#2E2E35"); Set("Border", "#38383F"); Set("Ghost", "#34343B");
            }
            Set("Accent", "#3E7BFA");
            Set("Danger", "#E5484D");
            Set("Ok", "#34C759");
        }
    }
}
