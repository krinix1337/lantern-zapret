using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public const string AppReleaseUrl = "https://github.com/krinix1337/lantern-zapret/releases/latest";

        // Включается только вместе с реализацией проверки подписи и жёстко
        // закреплённым публичным ключом издателя. На текущих релизах такого
        // манифеста нет, поэтому все пути установки работают fail-closed.
        public static bool VerifiedUpdateManifestAvailable { get { return false; } }

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

        public static string FetchLatestTag(string releaseApi, string fallbackReleasePage, string fallbackRawUrl = null)
        {
            // 1. Попытка через официальный GitHub API
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                var req = (HttpWebRequest)WebRequest.Create(releaseApi);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                req.Timeout = 8000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var m = System.Text.RegularExpressions.Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success)
                    {
                        string tag = m.Groups[1].Value.Trim().TrimStart('v', 'V');
                        if (!string.IsNullOrEmpty(tag)) return tag;
                    }
                }
            }
            catch { }

            // 2. Резервная попытка через HTTP 302 Location страницы latest-релиза (без лимитов GitHub API)
            try
            {
                if (!string.IsNullOrEmpty(fallbackReleasePage))
                {
                    var req = (HttpWebRequest)WebRequest.Create(fallbackReleasePage);
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                    req.AllowAutoRedirect = false;
                    req.Timeout = 8000;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    {
                        string loc = resp.Headers["Location"];
                        if (!string.IsNullOrEmpty(loc))
                        {
                            int idx = loc.LastIndexOf('/');
                            if (idx >= 0 && idx < loc.Length - 1)
                            {
                                string tag = loc.Substring(idx + 1).Trim().TrimStart('v', 'V');
                                if (!string.IsNullOrEmpty(tag)) return tag;
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Резервный опрос через version.txt (если задан)
            if (!string.IsNullOrEmpty(fallbackRawUrl))
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(fallbackRawUrl);
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                    req.Timeout = 8000;
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8))
                    {
                        string v = reader.ReadToEnd().Trim().TrimStart('v', 'V');
                        if (!string.IsNullOrEmpty(v)) return v;
                    }
                }
                catch { }
            }

            return null;
        }

        // ---- Обновления zapret: надёжный опрос через API + 302 + version.txt ----
        public static string CheckLatestVersion()
        {
            return FetchLatestTag(ZapretReleaseApi, "https://github.com/Flowseal/zapret-discord-youtube/releases/latest", VersionUrl);
        }

        // ---- Самообновление приложения (Lantern) ----
        public static string AppLatestVersion()
        {
            return FetchLatestTag(AppReleaseApi, Core.AppReleaseUrl);
        }

        public static string AppInstallerUrl()
        {
            // Берём адрес именно из GitHub Releases, а не из захардкоженной ссылки.
            // Так приложение всегда использует ассет опубликованного релиза.
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString(AppReleaseApi);
                    var m = System.Text.RegularExpressions.Regex.Match(json,
                        "\\\"name\\\"\\s*:\\s*\\\"Lantern-Setup\\.exe\\\"[\\s\\S]*?\\\"browser_download_url\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                    if (m.Success) return m.Groups[1].Value.Replace("\\/", "/");
                }
            }
            catch { }
            return null;
        }

        // Скачать установщик и запустить. Вызывать из фонового потока.
        public static bool SelfUpdate(string url, System.Action<DlProgress> onProgress, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(url)) { error = Loc.T("mw.verFail"); return false; }
            try
            {
                string file = Path.Combine(Path.GetTempPath(), "Lantern-Setup-" + Guid.NewGuid().ToString("N") + ".exe");
                if (!DownloadFile(url, file, onProgress, null)) { error = Loc.T("tg.dlFail"); return false; }
                Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
                return true;
            }
            catch (Exception ex) { error = Short(ex.Message); return false; }
        }

        // Текст changelog из последнего релиза (body). Для показа после обновления.
        public static string AppReleaseNotes()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString(AppReleaseApi);
                    var m = System.Text.RegularExpressions.Regex.Match(json, "\"body\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (m.Success)
                        return FormatReleaseNotes(JsonUnescape(m.Groups[1].Value));
                }
            }
            catch { }
            return null;
        }

        // GitHub API возвращает тело Release как JSON-строку. MessageBox не умеет
        // Markdown, поэтому превращаем текст в обычный читаемый список. Заодно
        // восстанавливаем старые заметки, где UTF-8 уже был ошибочно сохранён как
        // Windows-1252 (типичные символы "Ð" и "Ñ" на экране).
        static string JsonUnescape(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            try { text = System.Text.RegularExpressions.Regex.Unescape(text); }
            catch { text = text.Replace("\\n", "\n").Replace("\\r", "").Replace("\\\"", "\""); }
            return RepairUtf8Mojibake(text);
        }

        static string RepairUtf8Mojibake(string text)
        {
            if (string.IsNullOrEmpty(text) || (text.IndexOf('Ð') < 0 && text.IndexOf('Ñ') < 0 && text.IndexOf('ð') < 0))
                return text;
            try
            {
                string fixedText = System.Text.Encoding.UTF8.GetString(System.Text.Encoding.GetEncoding(1252).GetBytes(text));
                // Применяем замену только когда результат действительно похож на русский
                // текст или содержит emoji. Так не портятся обычные западноевропейские буквы.
                foreach (char c in fixedText)
                    if ((c >= 'А' && c <= 'я') || char.IsSurrogate(c)) return fixedText;
            }
            catch { }
            return text;
        }

        static string FormatReleaseNotes(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var lines = text.Replace("\r\n", "\n").Split('\n');
            var result = new System.Text.StringBuilder();
            foreach (string raw in lines)
            {
                string line = raw.TrimEnd();
                int hashes = 0;
                while (hashes < line.Length && line[hashes] == '#') hashes++;
                if (hashes > 0 && hashes < line.Length && line[hashes] == ' ')
                    line = line.Substring(hashes + 1).TrimStart();
                if (line.StartsWith("- ")) line = "• " + line.Substring(2);
                result.AppendLine(line);
            }
            return result.ToString().Trim();
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
