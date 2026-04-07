using System.Windows;
using XIVLauncher.Common.Util;

namespace XIVLauncher.Support
{
    public static class SupportLinks
    {
        public static void OpenDiscord(object sender, RoutedEventArgs e)
        {
            PlatformHelpers.OpenBrowser("https://discord.gg/m3s5eDqsMw");
        }

        public static void OpenFaq(object sender, RoutedEventArgs e)
        {
            PlatformHelpers.OpenBrowser("https://goatcorp.github.io/faq/");
        }
    }
}
