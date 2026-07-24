using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ZapretStudio
{
    // ================= Ядро: вся работа с zapret (без изменения его логики) =================
    // Приложение — только исполнительный слой поверх существующих bat/winws/службы.
    static partial class Core
    {
        public static string Root;
        public const string AppVersion = "2.1";
        public const string AppName = "Lantern";   // отображаемое имя приложения
        public const string AppRepo = "https://github.com/krinix1337/lantern-zapret";
        public const string AppReleaseApi = "https://api.github.com/repos/krinix1337/lantern-zapret/releases/latest";

        public static string Bin        { get { return Path.Combine(Root, "bin") + sep; } }
        public static string Lists      { get { return Path.Combine(Root, "lists") + sep; } }
        public static string UtilsDir   { get { return Path.Combine(Root, "utils"); } }
        public static string WinwsExe   { get { return Path.Combine(Root, "bin", "winws.exe"); } }
        public static string WinDivertSys { get { return Path.Combine(Root, "bin", "WinDivert64.sys"); } }
        public static string GameFlag   { get { return Path.Combine(Root, "utils", "game_filter.enabled"); } }
        public static string ConfigFile { get { return Path.Combine(Root, "utils", "gui-config.ini"); } }
        public static string TargetsFile{ get { return Path.Combine(Root, "utils", "targets.txt"); } }
        public static string IpsetFile  { get { return Path.Combine(Root, "lists", "ipset-all.txt"); } }
        public static string LocalVersionFile { get { return Path.Combine(Root, "service.bat"); } }
        public const string ServiceName = "zapret";

        static string sep { get { return Path.DirectorySeparatorChar.ToString(); } }

        // Локальная версия сборки zapret (из service.bat)
        public static string ZapretVersion()
        {
            try
            {
                foreach (var l in File.ReadAllLines(LocalVersionFile))
                {
                    var m = Regex.Match(l, "LOCAL_VERSION=([0-9A-Za-z.]+)");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { }
            return "?";
        }

        public static bool LocateRoot()
        {
            // 1) поиск от каталога exe вверх
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 6 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir, "bin", "winws.exe"))) { Root = dir.TrimEnd(Path.DirectorySeparatorChar); return true; }
                var parent = Directory.GetParent(dir.TrimEnd(Path.DirectorySeparatorChar));
                if (parent == null) break;
                dir = parent.FullName;
            }
            // 2) из сохранённого пути
            try
            {
                var cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui-config.ini");
                if (File.Exists(cfgPath))
                    foreach (var line in File.ReadAllLines(cfgPath))
                        if (line.StartsWith("root=", StringComparison.OrdinalIgnoreCase))
                        {
                            var p = line.Substring(5).Trim();
                            if (File.Exists(Path.Combine(p, "bin", "winws.exe"))) { Root = p; return true; }
                        }
            }
            catch { }
            return false;
        }

        public static void SetRoot(string path) { Root = path.TrimEnd(Path.DirectorySeparatorChar); }
    }
}
