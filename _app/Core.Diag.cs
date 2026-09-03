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
        // Ссылки — единый источник в Endpoints (Core.Endpoints.cs).
        public static string VersionUrl { get { return Endpoints.ZapretVersionUrl; } }
        public static string ReleaseUrl { get { return Endpoints.ZapretReleaseUrl; } }
        public static string RepoUrl    { get { return Endpoints.ZapretRepo; } }
        public static string AppReleaseUrl { get { return Endpoints.AppReleaseUrl; } }

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

        // Тот же значок как WPF ImageSource (для окна/панели задач). HIcon копируется
        // в BitmapSource, поэтому исходный Icon можно сразу освободить — не течёт.
        static System.Windows.Media.ImageSource _appIconSrc;
        static bool _appIconSrcTried;
        public static System.Windows.Media.ImageSource AppIconSource()
        {
            if (_appIconSrcTried) return _appIconSrc;
            _appIconSrcTried = true;
            try
            {
                string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                using (var ic = System.Drawing.Icon.ExtractAssociatedIcon(exe))
                {
                    if (ic == null) return null;
                    _appIconSrc = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                        ic.Handle, System.Windows.Int32Rect.Empty,
                        System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                }
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

            // ---- Проверки, перенесённые из service.bat (раздел Diagnostics) ----
            AppendEnvironmentDiagnostics(d);
            return d;
        }

        // Служба установлена и работает?
        static bool ServiceRunningNamed(string name)
        {
            string o = Capture("sc", "query " + name, 8000);
            return o.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool ProcessRunning(string imageWithoutExt)
        {
            try
            {
                var ps = System.Diagnostics.Process.GetProcessesByName(imageWithoutExt);
                foreach (var p in ps) p.Dispose();
                return ps.Length > 0;
            }
            catch { return false; }
        }

        // Блок проверок окружения из :service_diagnostics в service.bat.
        static void AppendEnvironmentDiagnostics(List<DiagItem> d)
        {
            // Base Filtering Engine — обязателен для работы zapret
            bool bfe = ServiceRunningNamed("BFE");
            d.Add(new DiagItem { Name = Loc.T("diag.n.bfe"), Sev = bfe ? Sev.Ok : Sev.Err,
                Value = bfe ? Loc.T("diag.v.bfeOk") : Loc.T("diag.v.bfeOff") });

            // Системный прокси
            bool proxy = false; string proxyAddr = null;
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k != null)
                    {
                        var enable = k.GetValue("ProxyEnable");
                        proxy = enable is int && (int)enable == 1;
                        if (proxy) proxyAddr = Convert.ToString(k.GetValue("ProxyServer"));
                    }
                }
            }
            catch { }
            d.Add(new DiagItem { Name = Loc.T("diag.n.proxy"), Sev = proxy ? Sev.Warn : Sev.Ok,
                Value = proxy ? string.Format(Loc.T("diag.v.proxyOn"), proxyAddr ?? "?") : Loc.T("diag.v.proxyOff") });

            // TCP timestamps (winws использует их для fooling=ts)
            bool ts = TcpTimestampsEnabled();
            d.Add(new DiagItem { Name = Loc.T("diag.n.tcpTs"), Sev = ts ? Sev.Ok : Sev.Warn,
                Value = ts ? Loc.T("diag.v.on") : Loc.T("diag.v.off") });

            // Известные конфликты процессов/служб
            bool adguard = ProcessRunning("AdguardSvc");
            d.Add(new DiagItem { Name = Loc.T("diag.n.adguard"), Sev = adguard ? Sev.Err : Sev.Ok,
                Value = adguard ? Loc.T("diag.v.found") : Loc.T("diag.v.clean") });

            AddServiceConflict(d, "Killer", Loc.T("diag.n.killer"));
            AddServiceConflict(d, "Intel Connectivity", Loc.T("diag.n.intel"));   // Intel Connectivity Network Service
            AddCheckpointConflict(d);
            AddServiceConflict(d, "SmartByte", Loc.T("diag.n.smartbyte"));

            // VPN-службы (потенциальный конфликт)
            string vpn = VpnServicesList();
            d.Add(new DiagItem { Name = Loc.T("diag.n.vpn"), Sev = vpn != null ? Sev.Warn : Sev.Ok,
                Value = vpn != null ? string.Format(Loc.T("diag.v.vpnFound"), vpn) : Loc.T("diag.v.clean") });

            // Другие обходы, конфликтующие за WinDivert
            string conflicts = ConflictingBypasses();
            d.Add(new DiagItem { Name = Loc.T("diag.n.bypassConflict"), Sev = conflicts != null ? Sev.Err : Sev.Ok,
                Value = conflicts != null ? string.Format(Loc.T("diag.v.foundList"), conflicts) : Loc.T("diag.v.clean") });

            // WinDivert активен, а winws не запущен — «висячий» драйвер чужого обхода
            bool winwsRun = IsWinwsRunning();
            if (!winwsRun && WinDivertLoaded())
            {
                d.Add(new DiagItem { Name = Loc.T("diag.n.wdOrphan"), Sev = Sev.Warn,
                    Value = Loc.T("diag.v.wdOrphan") });
            }

            // Зашифрованный DNS настроен хотя бы на одном интерфейсе?
            bool dohIface = DohInterfaceConfigured();
            d.Add(new DiagItem { Name = Loc.T("diag.n.dohIface"), Sev = dohIface ? Sev.Ok : Sev.Info,
                Value = dohIface ? Loc.T("diag.v.on") : Loc.T("diag.v.dohHint") });

            // Исключение Windows Defender
            bool defEx = IsDefenderExclusionSet();
            d.Add(new DiagItem { Name = Loc.T("settings.sec.antivirus"), Sev = defEx ? Sev.Ok : Sev.Warn,
                Value = defEx ? Loc.T("settings.defender.inList") : Loc.T("settings.defender.notIn") });
        }

        // Killer / SmartByte / Intel Connectivity: наличие службы по подстроке имени.
        static void AddServiceConflict(List<DiagItem> d, string substring, string title)
        {
            bool found = ServiceListContains(substring);
            d.Add(new DiagItem { Name = title, Sev = found ? Sev.Err : Sev.Ok,
                Value = found ? Loc.T("diag.v.found") : Loc.T("diag.v.clean") });
        }

        // Check Point: два характерных имени служб.
        static void AddCheckpointConflict(List<DiagItem> d)
        {
            bool found = ServiceListContains("TracSrvWrapper") || ServiceListContains("EPWD");
            d.Add(new DiagItem { Name = Loc.T("diag.n.checkpoint"), Sev = found ? Sev.Err : Sev.Ok,
                Value = found ? Loc.T("diag.v.found") : Loc.T("diag.v.clean") });
        }

        // sc query по всем службам — один вызов вместо перебора имён.
        static string _svcListCache;
        static DateTime _svcListAt;
        static readonly object _svcListLock = new object();
        static bool ServiceListContains(string substring)
        {
            string list = ServiceList();
            return !string.IsNullOrEmpty(list)
                && list.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string ServiceList()
        {
            lock (_svcListLock)
            {
                if (_svcListCache != null && (DateTime.Now - _svcListAt).TotalSeconds < 10)
                    return _svcListCache;
                // Запрос вне лока нельзя: два потока дублировали бы дорогой вызов.
                _svcListAt = DateTime.MinValue; // на время запроса
            }
            string o = Capture("sc", "query state= all", 20000);
            lock (_svcListLock)
            {
                _svcListCache = o;
                _svcListAt = DateTime.Now;
            }
            return o;
        }

        static string VpnServicesList()
        {
            string list = ServiceList();
            if (string.IsNullOrEmpty(list)) return null;
            var names = new List<string>();
            string current = null;
            foreach (var line in list.Split('\n'))
            {
                string t = line.Trim();
                if (t.StartsWith("SERVICE_NAME:", StringComparison.OrdinalIgnoreCase))
                    current = t.Substring("SERVICE_NAME:".Length).Trim();
                else if (t.IndexOf("VPN", StringComparison.OrdinalIgnoreCase) >= 0 && current != null)
                {
                    if (!names.Contains(current)) names.Add(current);
                    current = null;
                }
            }
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : null;
        }

        // GoodbyeDPI / другие winws-инстансы, конфликтующие за драйвер.
        static string ConflictingBypasses()
        {
            string[] names = { "GoodbyeDPI", "discordfix_zapret", "winws1", "winws2" };
            var found = new List<string>();
            foreach (var n in names)
            {
                string o = Capture("sc", "query " + n, 5000);
                if (o.IndexOf("STATE", StringComparison.OrdinalIgnoreCase) >= 0) found.Add(n);
            }
            return found.Count > 0 ? string.Join(", ", found.ToArray()) : null;
        }

        static bool TcpTimestampsEnabled()
        {
            string o = Capture("netsh", "interface tcp show global", 10000);
            foreach (var line in o.Split('\n'))
            {
                if (line.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0
                    && line.IndexOf("enabled", StringComparison.OrdinalIgnoreCase) >= 0
                    && line.IndexOf("disabled", StringComparison.OrdinalIgnoreCase) < 0)
                    return true;
            }
            return false;
        }

        // DoH на интерфейсах: Dnscache\InterfaceSpecificParameters\*\*\DohFlags > 0
        static bool DohInterfaceConfigured()
        {
            try
            {
                using (var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Services\Dnscache\InterfaceSpecificParameters"))
                {
                    if (root == null) return false;
                    foreach (var iface in root.GetSubKeyNames())
                        using (var ik = root.OpenSubKey(iface))
                        {
                            if (ik == null) continue;
                            foreach (var sub in ik.GetSubKeyNames())
                                using (var sk = ik.OpenSubKey(sub))
                                {
                                    var v = sk != null ? sk.GetValue("DohFlags") : null;
                                    if (v is long && (long)v > 0) return true;
                                    if (v is int && (int)v > 0) return true;
                                }
                        }
                }
            }
            catch { }
            return false;
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

        // Адрес установщика из GitHub Releases + (если опубликован) его .sha256.
        // Публикация чек-суммы в релизе позволяет убедиться, что скачанный exe —
        // именно тот, что собрал автор, а не результат подмены по пути.
        public static void AppUpdateAssets(out string installerUrl, out string sha256Url)
        {
            installerUrl = null; sha256Url = null;
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 /*Tls12*/ | (SecurityProtocolType)12288 /*Tls13*/;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Lantern");
                    string json = wc.DownloadString(AppReleaseApi);

                    int assetsIdx = json.IndexOf("\"assets\":");
                    if (assetsIdx >= 0)
                    {
                        string assetsJson = json.Substring(assetsIdx);
                        var blocks = System.Text.RegularExpressions.Regex.Matches(assetsJson, "\\{([^{}]+)\\}");
                        foreach (System.Text.RegularExpressions.Match b in blocks)
                        {
                            var nameM = System.Text.RegularExpressions.Regex.Match(b.Groups[1].Value, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                            var urlM = System.Text.RegularExpressions.Regex.Match(b.Groups[1].Value, "\"browser_download_url\"\\s*:\\s*\"([^\"]+)\"");
                            if (nameM.Success && urlM.Success)
                            {
                                string name = nameM.Groups[1].Value.Replace("\\/", "/");
                                string url = urlM.Groups[1].Value.Replace("\\/", "/");
                                if (installerUrl == null && name.Equals("Lantern-Setup.exe", StringComparison.OrdinalIgnoreCase))
                                    installerUrl = url;
                                if (sha256Url == null && (name.Equals("Lantern-Setup.exe.sha256", StringComparison.OrdinalIgnoreCase)
                                    || name.Equals("Lantern-Setup.exe.sig-sha256", StringComparison.OrdinalIgnoreCase)))
                                    sha256Url = url;
                            }
                        }
                    }
                }
            }
            catch { }

            // Надёжный фолбэк: прямые ссылки на assets последнего релиза GitHub
            if (string.IsNullOrEmpty(installerUrl))
            {
                installerUrl = "https://github.com/krinix1337/lantern-zapret/releases/latest/download/Lantern-Setup.exe";
                if (string.IsNullOrEmpty(sha256Url))
                    sha256Url = "https://github.com/krinix1337/lantern-zapret/releases/latest/download/Lantern-Setup.exe.sha256";
            }
        }

        // Обратная совместимость: только адрес установщика.
        public static string AppInstallerUrl()
        {
            string url, sha;
            AppUpdateAssets(out url, out sha);
            return url;
        }

        // Ожидаемый SHA-256 из текста .sha256-файла ("<hex>" или "<hex>  <file>").
        public static string ParseSha256File(string content)
        {
            if (string.IsNullOrEmpty(content)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(content, "\\b([0-9A-Fa-f]{64})\\b");
            return m.Success ? m.Groups[1].Value : null;
        }

        // Скачать установщик, при наличии опубликованного хеша — проверить и запустить.
        // Вызывать из фонового потока.
        public static bool SelfUpdate(string url, string sha256Url, System.Action<DlProgress> onProgress, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(url)) { error = Loc.T("mw.verFail"); return false; }
            try
            {
                string file = Path.Combine(Path.GetTempPath(), "Lantern-Setup-" + Guid.NewGuid().ToString("N") + ".exe");
                if (!DownloadFile(url, file, onProgress, null)) { error = Loc.T("settings.app.dlFail"); return false; }

                string expected = null;
                if (!string.IsNullOrEmpty(sha256Url))
                {
                    try
                    {
                        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                        using (var wc = new WebClient())
                        {
                            wc.Encoding = System.Text.Encoding.UTF8;
                            wc.Headers.Add("User-Agent", "Lantern");
                            expected = ParseSha256File(wc.DownloadString(sha256Url));
                        }
                    }
                    catch { }
                    if (string.IsNullOrEmpty(expected))
                    {
                        FailUpdate(file);
                        error = Loc.T("appupd.hashUnreadable");
                        return false;
                    }
                    string actual = Sha256OfFile(file);
                    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        FailUpdate(file);
                        Warn(Loc.T("appupd.hashMismatch"));
                        error = Loc.T("appupd.hashMismatch");
                        return false;
                    }
                    Info(Loc.T("appupd.hashOk"));
                }
                else
                {
                    // Релиз без опубликованной суммы — предупредим, но не блокируем
                    // обновление целиком (иначе приложение никогда не обновится).
                    Warn(Loc.T("appupd.hashMissing"));
                }

                Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
                return true;
            }
            catch (Exception ex) { error = Short(ex.Message); return false; }
        }

        static void FailUpdate(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }

        public static string Sha256OfFile(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                var sb = new System.Text.StringBuilder();
                foreach (var b in sha.ComputeHash(fs)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
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
                    return Endpoints.ReleaseNotes(wc.DownloadString(AppReleaseApi));
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
            // приватные диапазоны IPv4 (RFC1918 + link-local + loopback); публичные
            // адреса вида 192.0.2.x или 172.32.x не маскируются.
            text = System.Text.RegularExpressions.Regex.Replace(text,
                @"\b(10\.\d{1,3}\.\d{1,3}\.\d{1,3}|192\.168\.\d{1,3}\.\d{1,3}|172\.(?:1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}|169\.254\.\d{1,3}\.\d{1,3}|127\.\d{1,3}\.\d{1,3}\.\d{1,3})\b",
                "x.x.x.x");
            return text;
        }
    }
}
