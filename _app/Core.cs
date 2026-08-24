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
        // Версия читается из метаданных сборки (AssemblyInfo.cs) — не хардкодится.
        static string _appVer;
        public static string AppVersion
        {
            get
            {
                if (_appVer == null)
                {
                    try
                    {
                        var asm = System.Reflection.Assembly.GetEntryAssembly();
                        var attr = (System.Reflection.AssemblyInformationalVersionAttribute)
                            Attribute.GetCustomAttribute(asm, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
                        _appVer = attr != null ? attr.InformationalVersion : "0.0";
                    }
                    catch { _appVer = "0.0"; }
                }
                return _appVer;
            }
        }
        public const string AppName = "Lantern";   // отображаемое имя приложения
        // Ссылки приложения — единый источник в Endpoints (Core.Endpoints.cs).
        public static string AppRepo { get { return Endpoints.AppRepo; } }
        public static string AppReleaseApi { get { return Endpoints.AppReleaseApi; } }

// Корень может быть не найден до первичной загрузки zapret: все пути в этом
// случае откатываются к каталогу exe вместо NRE из Path.Combine(null,...).
static string SafeRoot { get { return string.IsNullOrEmpty(Root) ? AppDomain.CurrentDomain.BaseDirectory : Root; } }

public static string Bin        { get { if (_bin == null) _bin = Path.Combine(SafeRoot, "bin") + sep; return _bin; } }
        public static string Lists      { get { if (_lists == null) _lists = Path.Combine(SafeRoot, "lists") + sep; return _lists; } }
        public static string UtilsDir   { get { if (_utilsDir == null) _utilsDir = Path.Combine(SafeRoot, "utils"); return _utilsDir; } }
        public static string WinwsExe   { get { if (_winwsExe == null) _winwsExe = Path.Combine(SafeRoot, "bin", "winws.exe"); return _winwsExe; } }
        public static string WinDivertSys { get { if (_wdSys == null) _wdSys = Path.Combine(SafeRoot, "bin", "WinDivert64.sys"); return _wdSys; } }
        public static string GameFlag   { get { if (_gameFlag == null) _gameFlag = Path.Combine(SafeRoot, "utils", "game_filter.enabled"); return _gameFlag; } }
        public static string ConfigFile
        {
            get
            {
                if (_configFile == null)
                {
                    if (string.IsNullOrEmpty(Root))
                        _configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui-config.ini");
                    else
                        _configFile = Path.Combine(Root, "utils", "gui-config.ini");
                }
                return _configFile;
            }
        }
        public static string TargetsFile{ get { if (_targetsFile == null) _targetsFile = Path.Combine(SafeRoot, "utils", "targets.txt"); return _targetsFile; } }
        public static string IpsetFile  { get { if (_ipsetFile == null) _ipsetFile = Path.Combine(SafeRoot, "lists", "ipset-all.txt"); return _ipsetFile; } }
        public static string LocalVersionFile { get { if (_localVerFile == null) _localVerFile = Path.Combine(SafeRoot, "service.bat"); return _localVerFile; } }
        public const string ServiceName = "zapret";

        static string sep { get { return Path.DirectorySeparatorChar.ToString(); } }
        static string _bin, _lists, _utilsDir, _winwsExe, _wdSys, _gameFlag, _configFile, _targetsFile, _ipsetFile, _localVerFile;

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
                if (File.Exists(Path.Combine(dir, "bin", "winws.exe"))) { SetRoot(dir); return true; }
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
                            if (File.Exists(Path.Combine(p, "bin", "winws.exe"))) { SetRoot(p); return true; }
                        }
            }
            catch { }
            return false;
        }

        public static void SetRoot(string path) { InvalidatePaths(); Root = path.TrimEnd(Path.DirectorySeparatorChar); }

        static void InvalidatePaths()
        {
            _bin = _lists = _utilsDir = _winwsExe = _wdSys = _gameFlag = _configFile = _targetsFile = _ipsetFile = _localVerFile = null;
        }

        public static string FmtTime(TimeSpan t)
        {
            if (t.TotalMinutes >= 1) return (int)t.TotalMinutes + " min " + t.Seconds + " s";
            return t.Seconds + "," + (t.Milliseconds / 100) + " s";
        }
    }
}
