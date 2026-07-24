using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Principal;

namespace ZapretStudio
{
    public class DiagItem
    {
        public string Name;
        public Sev Sev;      // Ok/Warn/Bad/Info
        public string Value; // краткий результат
    }

    static partial class Core
    {
        public const string VersionUrl = "https://raw.githubusercontent.com/Flowseal/zapret-discord-youtube/main/.service/version.txt";
        public const string ReleaseUrl = "https://github.com/Flowseal/zapret-discord-youtube/releases/latest";
        public const string RepoUrl    = "https://github.com/Flowseal/zapret-discord-youtube";

        public static bool IsAdmin()
        {
            try
            {
                var id = WindowsIdentity.GetCurrent();
                var pr = new WindowsPrincipal(id);
                return pr.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        // Иконка приложения, встроенная в exe (-win32icon). Кэшируется.
        static System.Drawing.Icon _appIcon;
        static bool _appIconTried;
        public static System.Drawing.Icon AppIcon()
        {
            if (_appIconTried) return _appIcon;
            _appIconTried = true;
            try
            {
                string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                _appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
            }
            catch { _appIcon = null; }
            return _appIcon;
        }

        // Тот же значок как WPF ImageSource (для окна/панели задач).
        static System.Windows.Media.ImageSource _appIconSrc;
        static bool _appIconSrcTried;
        public static System.Windows.Media.ImageSource AppIconSource()
        {
            if (_appIconSrcTried) return _appIconSrc;
            _appIconSrcTried = true;
            try
            {
                var ic = AppIcon();
                if (ic == null) return null;
                _appIconSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    ic.Handle, System.Windows.Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            catch { _appIconSrc = null; }
            return _appIconSrc;
        }

        // Полная диагностика — реальные проверки окружения.
        public static List<DiagItem> RunDiagnostics()
        {
            var d = new List<DiagItem>();
            d.Add(new DiagItem { Name = Loc.T("diag.n.admin"), Sev = IsAdmin() ? Sev.Ok : Sev.Warn,
                Value = IsAdmin() ? Loc.T("diag.v.adminYes") : Loc.T("diag.v.adminNo") });

            bool root = !string.IsNullOrEmpty(Root) && File.Exists(WinwsExe);
            d.Add(new DiagItem { Name = Loc.T("diag.n.root"), Sev = root ? Sev.Ok : Sev.Err,
                Value = root ? Root : Loc.T("diag.v.notFound") });

            d.Add(new DiagItem { Name = Loc.T("diag.n.winws"), Sev = File.Exists(WinwsExe) ? Sev.Ok : Sev.Err,
                Value = File.Exists(WinwsExe) ? Loc.T("diag.v.present") : Loc.T("diag.v.absent") });

            bool wd = WinDivertFilePresent();
            d.Add(new DiagItem { Name = Loc.T("diag.n.wdFile"), Sev = wd ? Sev.Ok : Sev.Err,
                Value = wd ? Loc.T("diag.v.present") : Loc.T("diag.v.wdQuar") });

            bool wl = WinDivertLoaded();
            d.Add(new DiagItem { Name = Loc.T("diag.n.wdLoaded"), Sev = wl ? Sev.Ok : Sev.Info,
                Value = wl ? Loc.T("diag.v.loaded") : Loc.T("diag.v.wdNotLoaded") });

            bool run = IsWinwsRunning();
            d.Add(new DiagItem { Name = Loc.T("diag.n.winwsProc"), Sev = run ? Sev.Ok : Sev.Info,
                Value = run ? Loc.T("diag.v.running") : Loc.T("diag.v.stopped") });

            string ss = ServiceState();
            d.Add(new DiagItem { Name = Loc.T("diag.n.service"), Sev = ss == "running" ? Sev.Ok : Sev.Info,
                Value = ss == "running" ? Loc.T("diag.v.svcRunning") : ss == "stopped" ? Loc.T("diag.v.svcStopped") : Loc.T("diag.v.svcAbsent") });

            int strat = GetStrategyFiles().Count;
            d.Add(new DiagItem { Name = Loc.T("diag.n.strats"), Sev = strat > 0 ? Sev.Ok : Sev.Err,
                Value = strat > 0 ? string.Format(Loc.T("diag.v.stratCount"), strat) : Loc.T("diag.v.stratNone") });

            d.Add(new DiagItem { Name = Loc.T("diag.n.gameFilter"), Sev = Sev.Info, Value = GameModeLabel() });
            d.Add(new DiagItem { Name = Loc.T("diag.n.ipset"), Sev = Sev.Info, Value = IpsetStatusLabel() });

            bool tgt = File.Exists(TargetsFile);
            d.Add(new DiagItem { Name = Loc.T("diag.n.targets"), Sev = tgt ? Sev.Ok : Sev.Warn,
                Value = tgt ? string.Format(Loc.T("diag.v.targetCount"), LoadTargets().Count) : Loc.T("diag.v.targetsAbsent") });

            d.Add(new DiagItem { Name = Loc.T("diag.n.hosts"), Sev = HostsHasYouTube() ? Sev.Warn : Sev.Ok,
                Value = HostsHasYouTube() ? Loc.T("diag.v.hostsFound") : Loc.T("diag.v.hostsClean") });

            d.Add(new DiagItem { Name = Loc.T("diag.n.localVer"), Sev = Sev.Info, Value = ZapretVersion() });
            return d;
        }

        static string HostsPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts"); }
        }
        public static bool HostsHasYouTube()
        {
            try
            {
                if (!File.Exists(HostsPath)) return false;
                foreach (var l in File.ReadAllLines(HostsPath))
                {
                    string s = l.Trim();
                    if (s.StartsWith("#") || s.Length == 0) continue;
                    string low = s.ToLowerInvariant();
                    if (low.Contains("youtube.com") || low.Contains("youtu.be")) return true;
                }
            }
            catch { }
            return false;
        }

        // ---- Обновления (только проверка версии, ничего не скачиваем без подтверждения) ----
        public static string CheckLatestVersion()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "ZapretStudio");
                    string v = wc.DownloadString(VersionUrl).Trim();
                    return v;
                }
            }
            catch { return null; }
        }

        // ---- Самообновление приложения (Lantern) ----
        public static string AppLatestVersion()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString(AppReleaseApi);
                    var m = System.Text.RegularExpressions.Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) return m.Groups[1].Value.Trim();
                }
            }
            catch { }
            return null;
        }

        public static string AppInstallerUrl()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString(AppReleaseApi);
                    var m = System.Text.RegularExpressions.Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]+\\.exe)\"");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch { }
            return null;
        }

        // Скачать установщик и запустить. Вызывать из фонового потока.
        public static bool SelfUpdate(string url, out string error)
        {
            error = null;
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "Lantern-Setup.exe");
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Lantern");
                    wc.DownloadFile(url, tmp);
                }
                if (!File.Exists(tmp)) { error = "File not saved"; return false; }
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tmp,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        // Текст changelog из последнего релиза (body). Для показа после обновления.
        public static string AppReleaseNotes()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString(AppReleaseApi);
                    var m = System.Text.RegularExpressions.Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (m.Success)
                        return m.Groups[1].Value.Replace("\\n", "\n").Replace("\\r", "").Replace("\\\"", "\"");
                }
            }
            catch { }
            return null;
        }

        // ---- Маскирование для диагностики (имя пользователя, пути, локальные IP) ----
        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string user = Environment.UserName;
            if (!string.IsNullOrEmpty(user))
                text = System.Text.RegularExpressions.Regex.Replace(text, System.Text.RegularExpressions.Regex.Escape(user), "USER",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            string prof = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(prof))
                text = text.Replace(prof, "%USERPROFILE%");
            // локальные IPv4
            text = System.Text.RegularExpressions.Regex.Replace(text,
                @"\b(10|192|172|169)\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "x.x.x.x");
            return text;
        }
    }
}
