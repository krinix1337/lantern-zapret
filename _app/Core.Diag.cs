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

        // Перезапуск приложения с запросом прав администратора (UAC)
        public static bool RestartAsAdmin()
        {
            try
            {
                string exe;
                using (var cur = Process.GetCurrentProcess())
                    exe = cur.MainModule.FileName;

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    // №62: новый экземпляр стартует раньше, чем этот отпустит мьютекс,
                    // поэтому он должен дождаться его, а не «огонить и забыть».
                    Arguments = "--tookover",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                // Порядок важен: сначала освобождаем мьютекс, затем гасим окно.
                // ForceShutdown нужен, чтобы OnClosing не свернул окно в трей и
                // не отменил Shutdown() (№61).
                App.ForceShutdown = true;
                App.ReleaseMutexForRestart();
                System.Windows.Application.Current.Shutdown();
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Пользователь нажал «Нет» в окне подтверждения контроля учетных записей (UAC)
                return false;
            }
            catch (Exception ex)
            {
                Fail("RestartAsAdmin: " + ex.Message);
                return false;
            }
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
            // Дорогие проверки вычисляем ОДИН раз: IsAdmin(), File.Exists(WinwsExe)
            // и HostsHasYouTube() раньше вызывались по два раза каждая.
            bool admin = IsAdmin();
            bool winwsFile = File.Exists(WinwsExe);
            bool hostsYt = HostsHasYouTube();

            d.Add(new DiagItem { Name = Loc.T("diag.n.admin"), Sev = admin ? Sev.Ok : Sev.Warn,
                Value = admin ? Loc.T("diag.v.adminYes") : Loc.T("diag.v.adminNo") });

            bool root = !string.IsNullOrEmpty(Root) && winwsFile;
            d.Add(new DiagItem { Name = Loc.T("diag.n.root"), Sev = root ? Sev.Ok : Sev.Err,
                Value = root ? Root : Loc.T("diag.v.notFound") });

            d.Add(new DiagItem { Name = Loc.T("diag.n.winws"), Sev = winwsFile ? Sev.Ok : Sev.Err,
                Value = winwsFile ? Loc.T("diag.v.present") : Loc.T("diag.v.absent") });

            bool wd = WinDivertFilePresent();
            d.Add(new DiagItem { Name = Loc.T("diag.n.wdFile"), Sev = wd ? Sev.Ok : Sev.Err,
                Value = wd ? Loc.T("diag.v.present") : Loc.T("diag.v.wdQuar") });

            bool wl = WinDivertLoadedCached();
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

            string gmLabel = GameModeLabel();
            bool gameInactiveDueToIpset = !IpsetEnabled && GameMode != "off";
            if (gameInactiveDueToIpset)
                d.Add(new DiagItem { Name = Loc.T("diag.n.gameFilter"), Sev = Sev.Warn, Value = gmLabel + " (" + Loc.T("filters.ipset.off.hidesGame") + ")" });
            else
                d.Add(new DiagItem { Name = Loc.T("diag.n.gameFilter"), Sev = Sev.Info, Value = gmLabel });
            d.Add(new DiagItem { Name = Loc.T("diag.n.ipset"), Sev = Sev.Info, Value = IpsetStatusLabel() });

            bool tgt = File.Exists(TargetsFile);
            d.Add(new DiagItem { Name = Loc.T("diag.n.targets"), Sev = tgt ? Sev.Ok : Sev.Warn,
                Value = tgt ? string.Format(Loc.T("diag.v.targetCount"), LoadTargets().Count) : Loc.T("diag.v.targetsAbsent") });

            d.Add(new DiagItem { Name = Loc.T("diag.n.hosts"), Sev = hostsYt ? Sev.Warn : Sev.Ok,
                Value = hostsYt ? Loc.T("diag.v.hostsFound") : Loc.T("diag.v.hostsClean") });

            d.Add(new DiagItem { Name = Loc.T("diag.n.localVer"), Sev = Sev.Info, Value = ZapretVersion() });

            // ---- Проверки, перенесённые из service.bat (раздел Diagnostics) ----
            AppendEnvironmentDiagnostics(d);
            return d;
        }

        // Служба установлена и работает?
        // №2a: через ServiceController, а не разбором `sc query` — на русской
        // Windows метки локализованы, вывод идёт в CP866, и заодно не нужен
        // запуск внешнего процесса на каждую проверку.
        static bool ServiceRunningNamed(string name)
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(name))
                {
                    var st = sc.Status;
                    return st == System.ServiceProcess.ServiceControllerStatus.Running
                        || st == System.ServiceProcess.ServiceControllerStatus.StartPending;
                }
            }
            catch { return false; }
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
            if (!winwsRun && WinDivertLoadedCached())
            {
                d.Add(new DiagItem { Name = Loc.T("diag.n.wdOrphan"), Sev = Sev.Warn,
                    Value = Loc.T("diag.v.wdOrphan") });
            }

            // Зашифрованный DNS настроен хотя бы на одном интерфейсе?
            bool dohIface = DohInterfaceConfigured();
            d.Add(new DiagItem { Name = Loc.T("diag.n.dohIface"), Sev = dohIface ? Sev.Ok : Sev.Info,
                Value = dohIface ? Loc.T("diag.v.on") : Loc.T("diag.v.dohHint") });

            // Исключение Windows Defender
            var defStatus = GetDefenderExclusionStatus();
            if (!defStatus.HasValue)
            {
                d.Add(new DiagItem { Name = Loc.T("settings.sec.antivirus"), Sev = Sev.Ok,
                    Value = Loc.T("settings.defender.disabled") });
            }
            else
            {
                bool defEx = defStatus.Value;
                d.Add(new DiagItem { Name = Loc.T("settings.sec.antivirus"), Sev = defEx ? Sev.Ok : Sev.Warn,
                    Value = defEx ? Loc.T("settings.defender.inList") : Loc.T("settings.defender.notIn") });
            }

            // Путь к папке приложения (кириллица и OneDrive могут ломать драйвер winws)
            string rootPath = Root ?? "";
            bool hasNonAscii = false;
            foreach (char ch in rootPath) { if (ch > 127) { hasNonAscii = true; break; } }
            if (hasNonAscii)
            {
                d.Add(new DiagItem { Name = Loc.T("diag.n.pathCyrillic"), Sev = Sev.Warn,
                    Value = Loc.T("diag.v.pathCyrillic") });
            }
            if (rootPath.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                d.Add(new DiagItem { Name = Loc.T("diag.n.pathOneDrive"), Sev = Sev.Warn,
                    Value = Loc.T("diag.v.pathOneDrive") });
            }
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

        // №2b: список служб берём через ServiceController, а не разбором
        // `sc query state= all`. Прежний код искал английскую метку
        // «SERVICE_NAME:», которой на русской Windows нет («Имя_службы:»),
        // вызов занимал до 20 секунд и на время запроса сбрасывал кэш в
        // DateTime.MinValue — второй поток шёл параллельно тем же путём (№45).
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
            }
            var sb = new System.Text.StringBuilder();
            try
            {
                foreach (var s in System.ServiceProcess.ServiceController.GetServices())
                {
                    try { sb.Append(s.ServiceName).Append('\n').Append(s.DisplayName).Append('\n'); }
                    catch { }
                    finally { s.Dispose(); }
                }
            }
            catch { }
            string o = sb.ToString();
            lock (_svcListLock) { _svcListCache = o; _svcListAt = DateTime.Now; }
            return o;
        }

        static string VpnServicesList()
        {
            var names = new List<string>();
            try
            {
                foreach (var s in System.ServiceProcess.ServiceController.GetServices())
                {
                    try
                    {
                        string n = (s.ServiceName ?? "") + " " + (s.DisplayName ?? "");
                        if (n.IndexOf("VPN", StringComparison.OrdinalIgnoreCase) >= 0 && !names.Contains(s.ServiceName))
                            names.Add(s.ServiceName);
                    }
                    catch { }
                    finally { s.Dispose(); }
                }
            }
            catch { }
            return names.Count > 0 ? string.Join(", ", names.ToArray()) : null;
        }

        // GoodbyeDPI / другие winws-инстансы, конфликтующие за драйвер.
        // №2a: существование службы — через ServiceController. Поиск «STATE» в
        // выводе sc.exe не срабатывал на русской Windows («Состояние : 4 RUNNING»),
        // а основной реальный конфликт (GoodbyeDPI) — процесс, а не служба.
        static string ConflictingBypasses()
        {
            string[] names = { "GoodbyeDPI", "discordfix_zapret", "winws1", "winws2" };
            var found = new List<string>();
            foreach (var n in names)
            {
                try
                {
                    using (var sc = new System.ServiceProcess.ServiceController(n))
                    {
                        var st = sc.Status;   // InvalidOperationException, если службы нет
                        found.Add(n + " (" + st + ")");
                    }
                }
                catch { }
            }
            if (ProcessRunning("GoodbyeDPI") && !AnyStartsWith(found, "GoodbyeDPI"))
                found.Add("GoodbyeDPI (process)");
            // чужой winws из другой папки zapret держит тот же WinDivert
            try
            {
                foreach (var p in Process.GetProcessesByName("winws"))
                {
                    try { if (!IsRootWinws(p)) found.Add("winws (pid " + p.Id + ", foreign path)"); }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return found.Count > 0 ? string.Join(", ", found.ToArray()) : null;
        }

        static bool AnyStartsWith(List<string> items, string prefix)
        {
            foreach (var s in items)
                if (s != null && s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // №2c: netsh печатает «Метки времени RFC 1323 : allowed» — метка
        // локализована, а прежнее условие требовало подстроки «timestamp» и
        // «enabled», поэтому карточка вечно горела жёлтой при работающих метках.
        // Строку ищем по номеру RFC (он не локализуется), значение — ASCII.
        static bool TcpTimestampsEnabled()
        {
            string o = CaptureUtf8("netsh", "interface tcp show global", 10000);
            foreach (var line in o.Split('\n'))
            {
                if (line.IndexOf("1323", StringComparison.Ordinal) < 0) continue;
                int colon = line.LastIndexOf(':');
                if (colon < 0) continue;
                string val = line.Substring(colon + 1).Trim().ToLowerInvariant();
                if (val.Length == 0) continue;
                // «disabled» — единственный вариант «выключено»; enabled/allowed работают
                return val != "disabled";
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
        //
        // sha256Url заполняется ТОЛЬКО если такой ассет реально есть в релизе.
        // Раньше сюда подставлялась ссылка .../releases/latest/download/
        // Lantern-Setup.exe.sha256 «на всякий случай»; в релизах этого файла нет,
        // она отдавала 404, и обновление всегда падало с «не удалось прочитать
        // .sha256» — уже после скачивания 5,5 МБ.
        public static void AppUpdateAssets(out string installerUrl, out string sha256Url)
        {
            installerUrl = null; sha256Url = null;
            string json = FetchReleaseJson(AppReleaseApi);
            if (!string.IsNullOrEmpty(json))
            {
                installerUrl = Endpoints.ReleaseAssetUrl(json, Endpoints.AppInstallerAsset);
                sha256Url = Endpoints.ReleaseAssetUrl(json, Endpoints.AppInstallerSha256Asset);
            }

            // API мог быть недоступен (лимит запросов, блокировка api.github.com) —
            // тогда берём прямую ссылку «последний релиз». Хеш при этом остаётся
            // неизвестным: SelfUpdate предупредит, но не станет выдумывать URL.
            if (string.IsNullOrEmpty(installerUrl))
                installerUrl = Endpoints.AppInstallerLatestUrl;
        }

        // JSON последнего релиза из GitHub API или null.
        // Это единственный источник контрольных сумм чужих релизов: digest
        // публикуется на каждый ассет, отдельных SHA256SUMS-файлов там нет.
        public static string FetchReleaseJson(string releaseApi)
        {
            try
            {
                using (var wc = new TimedWebClient { TimeoutMilliseconds = 20000 })
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "Lantern");
                    wc.Headers.Add("Accept", "application/vnd.github+json");
                    return wc.DownloadString(releaseApi);
                }
            }
            catch (Exception ex) { Warn("release api " + releaseApi + ": " + Short(ex.Message)); return null; }
        }

        // Последние релизы для кнопки «Что обновлялось». null — API недоступен
        // (лимит запросов, блокировка), пустой список — релизов нет. UI обязан
        // различать эти два случая, а не показывать пустое окно.
        public static List<Endpoints.ReleaseInfo> FetchReleaseList(string releasesApi)
        {
            string json = FetchReleaseJson(releasesApi);
            if (string.IsNullOrEmpty(json)) return null;
            return Endpoints.Releases(json);
        }

        // Сверка файла с ожидаемой суммой. Ожидаемый хеш обязателен: без него
        // проверка невозможна, и вызывающий код должен решить, что делать сам.
        public static bool VerifySha256(string file, string expected, out string error)
        {
            error = null;
            string actual;
            try { actual = Sha256OfFile(file); }
            catch (Exception ex) { error = Short(ex.Message); return false; }
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                error = string.Format(Loc.T("dl.hashMismatch"), expected, actual);
                Warn(error);
                return false;
            }
            Info(Loc.T("dl.hashOk"));
            return true;
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
                Info(string.Format(Loc.T("appupd.source"), url));
                string file = Path.Combine(Path.GetTempPath(), "Lantern-Setup-" + Guid.NewGuid().ToString("N") + ".exe");
                if (!DownloadFile(url, file, onProgress, null)) { error = Loc.T("settings.app.dlFail"); return false; }

                string expected = null;
                if (!string.IsNullOrEmpty(sha256Url))
                {
                    try
                    {
                        using (var wc = new TimedWebClient { TimeoutMilliseconds = 10000 })
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
                    // Релиз без опубликованной суммы — блокируем обновление,
                    // чтобы не запускать непроверенный установщик от администратора.
                    FailUpdate(file);
                    error = Loc.T("appupd.hashMissing");
                    return false;
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
                string json = FetchReleaseJson(AppReleaseApi);
                if (!string.IsNullOrEmpty(json)) return Endpoints.ReleaseNotes(json);
            }
            catch { }
            return null;
        }

        // Проверка обязательных файлов движка по манифесту SHA256SUMS.txt.
        // Пользовательские .bat, списки и настройки не считаются повреждением.
        public static List<string> EngineIntegrityIssues()
        {
            var issues = new List<string>();
            if (string.IsNullOrEmpty(Root))
            {
                issues.Add(string.Format(Loc.T("updates.integrity.missingFile"), "bin/winws.exe"));
                return issues;
            }

            string manifest = Path.Combine(Root, "SHA256SUMS.txt");
            var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(manifest))
            {
                try
                {
                    foreach (string raw in File.ReadAllLines(manifest))
                    {
                        string line = raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        int sp = line.IndexOf(' ');
                        if (sp <= 0) continue;
                        string hash = line.Substring(0, sp).Trim().ToLowerInvariant();
                        string rel = line.Substring(sp).Trim().TrimStart('*').Trim();
                        if (rel.StartsWith("./")) rel = rel.Substring(2);
                        rel = rel.Replace('/', Path.DirectorySeparatorChar);
                        if (!rel.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
                        string shown = rel.Replace(Path.DirectorySeparatorChar, '/');
                        checkedPaths.Add(rel);
                        string full = Path.Combine(Root, rel);
                        if (!File.Exists(full))
                        {
                            issues.Add(string.Format(Loc.T("updates.integrity.missingFile"), shown));
                            continue;
                        }
                        try
                        {
                            string actual = Sha256OfFile(full);
                            if (!string.Equals(actual, hash, StringComparison.OrdinalIgnoreCase))
                                issues.Add(string.Format(Loc.T("updates.integrity.corruptFile"), shown));
                        }
                        catch (Exception ex)
                        {
                            issues.Add(string.Format(Loc.T("updates.integrity.unreadableFile"), shown, Short(ex.Message)));
                        }
                    }
                }
                catch (Exception ex)
                {
                    issues.Add(string.Format(Loc.T("updates.integrity.manifestUnreadable"), Short(ex.Message)));
                }
            }
            else
            {
                issues.Add(Loc.T("updates.integrity.manifestMissing"));
            }

            string[] required = { Path.Combine("bin", "winws.exe"), Path.Combine("bin", "WinDivert64.sys"), Path.Combine("bin", "WinDivert.dll") };
            foreach (string rel in required)
            {
                if (checkedPaths.Contains(rel)) continue;
                if (File.Exists(Path.Combine(Root, rel))) continue;
                issues.Add(string.Format(Loc.T("updates.integrity.missingFile"), rel.Replace(Path.DirectorySeparatorChar, '/')));
            }
            return issues;
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

        // ==================== Инструменты быстрого исправления и диагностики ====================

        // Очистка кэша Discord (устраняет бесконечное RTC Connecting при смене стратегий)
        public static int ClearDiscordCache()
        {
            string[] procNames = { "Discord", "DiscordCanary", "DiscordDevelopment", "DiscordPTB" };
            foreach (var pName in procNames)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(pName))
                    {
                        try { p.Kill(); p.WaitForExit(2000); } catch { }
                        finally { p.Dispose(); }
                    }
                }
                catch { }
            }

            int deletedFolders = 0;
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] discordVariants = { "discord", "discordcanary", "discordptb", "discorddevelopment" };
            string[] cacheSubDirs = { "Cache", "Code Cache", "GPUCache", "DawnCache" };

            foreach (var variant in discordVariants)
            {
                string baseDir = Path.Combine(appData, variant);
                if (Directory.Exists(baseDir))
                {
                    foreach (var sub in cacheSubDirs)
                    {
                        string target = Path.Combine(baseDir, sub);
                        if (Directory.Exists(target))
                        {
                            try { DeleteDirSafe(target); deletedFolders++; } catch { }
                        }
                    }
                }
            }
            return deletedFolders;
        }

        // Сброс и удаление зависшего драйвера WinDivert.
        // №19: раньше возвращал true безусловно — кнопка всегда рапортовала «успешно»,
        // даже когда драйвер оставался в памяти и требовал перезагрузки.
        public static bool ResetWinDivertDriver(out string error)
        {
            error = null;
            if (!TryBeginWinwsOperation())
            {
                error = Loc.T("mw.busy");
                Warn(error);
                return false;
            }
            try
            {
                if (IsWinwsRunning()) KillWinws();
                if (ServiceState() == "running") { StopService(); WaitServiceStopped(4000); }

                string err;
                Run("sc", "stop windivert", 5000, out err);
                Run("sc", "stop windivert14", 5000, out err);
                Run("net", "stop windivert", 5000, out err);

                Run("sc", "delete windivert", 5000, out err);
                Run("sc", "delete windivert14", 5000, out err);

                InvalidateWinDivertCache();
                if (WinDivertLoaded())
                {
                    error = Loc.T("check.diag.wdResetBusy");
                    Warn(error);
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = Short(ex.Message);
                Fail("ResetWinDivertDriver: " + error);
                return false;
            }
            finally
            {
                EndWinwsOperation();
            }
        }

        // Принудительное включение TCP Timestamps (необходимо для fooling=ts)
        public static bool EnableTcpTimestamps()
        {
            try
            {
                string outStr = CaptureUtf8("netsh", "interface tcp set global timestamps=enabled", 8000);
                return outStr.IndexOf("Ok", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       outStr.IndexOf("ОК", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch (Exception ex)
            {
                Fail("EnableTcpTimestamps: " + ex.Message);
                return false;
            }
        }

        // Отключение системного прокси
        public static bool DisableSystemProxy()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true))
                {
                    if (k != null)
                    {
                        k.SetValue("ProxyEnable", 0, Microsoft.Win32.RegistryValueKind.DWord);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Fail("DisableSystemProxy: " + ex.Message);
                return false;
            }
        }

        // Проверка наличия службы zapret, зарегистрированной из другой папки
        public static string GetForeignServiceImagePath()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + ServiceName))
                {
                    if (k == null) return null;
                    var val = k.GetValue("ImagePath");
                    if (val == null) return null;
                    string imagePath = val.ToString();
                    if (string.IsNullOrEmpty(imagePath)) return null;

                    string normRoot = Path.GetFullPath(Root).TrimEnd('\\', '/');
                    if (imagePath.IndexOf(normRoot, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return imagePath;
                    }
                }
            }
            catch { }
            return null;
        }

        public static bool TakeoverForeignService(string strategyFile)
        {
            // №1: сначала убеждаемся, что есть чем заменить службу, и только потом
            // её снимаем. Прежний порядок (RemoveService первой строкой) при
            // неверном имени файла оставлял систему без службы вообще: вызывающая
            // сторона передавала отображаемое имя без «.bat» («general (ALT)»),
            // InstallService падал на FileNotFoundException, а служба уже удалена.
            if (string.IsNullOrEmpty(strategyFile))
            {
                var files = GetStrategyFiles();
                if (files.Count == 0)
                {
                    Fail("Takeover: no strategy files in " + SafeRoot);
                    return false;
                }
                strategyFile = files[0];
            }
            else if (!File.Exists(Path.Combine(SafeRoot, strategyFile)))
            {
                // SafeRoot, а не Root: Root == null бросил бы ArgumentNullException
                Fail("Takeover: strategy file not found: " + strategyFile);
                return false;
            }
            RemoveService();
            return InstallService(strategyFile);
        }

        public static bool RemoveForeignService()
        {
            return RemoveService();
        }

        // Тест устойчивости к DPI Freeze (зависание потока после 16-20 пакетов)
        public static void RunDpiFreezeTest(Action<bool, string, int, long> callback)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var sw = Stopwatch.StartNew();
                string targetUrl = "https://www.youtube.com/s/desktop/28dcbbff/jsbin/desktop_polymer.vflset/desktop_polymer.js";
                int targetBytes = 65536; // 64 КБ
                int totalRead = 0;
                bool success = false;
                string detail = "";

                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(targetUrl);
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                    req.Timeout = 8000;
                    req.ReadWriteTimeout = 6000;
                    req.AddRange(0, targetBytes - 1);

                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var stream = resp.GetResponseStream())
                    {
                        byte[] buf = new byte[4096];
                        int r;
                        while ((r = stream.Read(buf, 0, buf.Length)) > 0)
                        {
                            totalRead += r;
                            if (totalRead >= targetBytes) break;
                        }
                    }

                    if (totalRead >= 32768)
                    {
                        success = true;
                        detail = string.Format(Loc.T("check.diag.dpiFreezeOkDetail"), totalRead / 1024, sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        detail = string.Format(Loc.T("check.diag.dpiFreezeStallDetail"), totalRead / 1024);
                    }
                }
                catch (WebException wex)
                {
                    if (totalRead > 0)
                    {
                        detail = string.Format(Loc.T("check.diag.dpiFreezeDetectedDetail"), totalRead / 1024, wex.Status);
                    }
                    else
                    {
                        detail = string.Format(Loc.T("check.diag.dpiFreezeNoConn"), wex.Status);
                    }
                }
                catch (Exception ex)
                {
                    detail = ex.Message;
                }
                finally
                {
                    sw.Stop();
                    if (callback != null) callback(success, detail, totalRead, sw.ElapsedMilliseconds);
                }
            });
        }

        // Сброс до заводских настроек
        public static void FactoryReset()
        {
            string err;
            FactoryReset(out err);
        }

        public static bool FactoryReset(out string error)
        {
            error = null;
            try
            {
                StopService();
                KillWinws();
                if (TgProxyRunning()) TgProxyStop();

                string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui-config.ini");
                string utilsCfg = !string.IsNullOrEmpty(Root) ? Path.Combine(Root, "utils", "gui-config.ini") : null;
                try { if (File.Exists(local)) File.Delete(local); } catch (Exception ex) { error = ex.Message; }
                try { if (utilsCfg != null && File.Exists(utilsCfg)) File.Delete(utilsCfg); } catch (Exception ex) { if (error == null) error = ex.Message; }

                lock (_cfgLock)
                {
                    _cfg = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                lock (Log) { Log.Clear(); }
                Theme.Apply(ThemeMode.Dark);
                return error == null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Fail("FactoryReset: " + ex.Message);
                return false;
            }
        }
    }
}
