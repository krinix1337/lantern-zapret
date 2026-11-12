using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ZapretStudio
{
    // Запуск/остановка winws, служба Windows, статусы, WinDivert. Ничего не скрываем.
    static partial class Core
    {
        public static DateTime? StartedAt; // время запуска (для uptime), пока процессом управляет приложение
        static int _winwsOperation;
        static int _lastKnownWinwsPid = -1;
        static Process _winwsProcess;

        // ---- Кольцевой буфер вывода winws (stdout/stderr) ----
        // Окно winws скрыто и его логи раньше терялись: буфер хранит последние
        // строки для диагностики «почему обход не работает».
        const int WinwsLogCap = 400;
        static readonly object _winwsLogLock = new object();
        static readonly List<string> _winwsLog = new List<string>(WinwsLogCap);
        static bool _winwsLogTruncated;

        public static void WinwsLogAppend(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            lock (_winwsLogLock)
            {
                if (_winwsLog.Count >= WinwsLogCap)
                {
                    _winwsLog.RemoveAt(0);
                    _winwsLogTruncated = true;
                }
                _winwsLog.Add(line);
            }
        }

        public static void WinwsLogClear()
        {
            lock (_winwsLogLock) { _winwsLog.Clear(); _winwsLogTruncated = false; }
        }

        public static string WinwsLogTail(int maxLines)
        {
            lock (_winwsLogLock)
            {
                var sb = new System.Text.StringBuilder();
                if (_winwsLogTruncated && _winwsLog.Count > 0)
                    sb.AppendLine("...");
                int start = Math.Max(0, _winwsLog.Count - maxLines);
                for (int i = start; i < _winwsLog.Count; i++)
                    sb.AppendLine(_winwsLog[i]);
                return sb.ToString();
            }
        }

        public static string WinwsLogAll()
        {
            return WinwsLogTail(WinwsLogCap);
        }

        // Один экземпляр winws нельзя безопасно одновременно перезапускать,
        // тестировать и переключать watchdog-ом.
        public static bool TryBeginWinwsOperation()
        {
            bool ok = System.Threading.Interlocked.CompareExchange(ref _winwsOperation, 1, 0) == 0;
            if (ok) System.Threading.Interlocked.Increment(ref _winwsOpSeq);
            return ok;
        }
        public static void EndWinwsOperation()
        {
            System.Threading.Interlocked.Increment(ref _winwsOpSeq);
            System.Threading.Interlocked.Exchange(ref _winwsOperation, 0);
        }

        // Счётчик намеренных переходов winws (начало/конец операции). Монитор
        // падений сравнивает его между тиками: если за 15 с была операция
        // (стоп, тест стратегии, обновление), исчезновение winws — не падение.
        // Раньше монитор смотрел только на «операция идёт прямо сейчас», и после
        // кнопки «Остановить» следующий тик сообщал «обход упал».
        static int _winwsOpSeq;
        public static int WinwsOperationSeq
        {
            get { return System.Threading.Thread.VolatileRead(ref _winwsOpSeq); }
        }
        // Для действий, которые меняют состояние winws, но не держат блокировку
        // всю дорогу (например, скачивание обновления между остановкой и запуском).
        public static void MarkWinwsTransition()
        {
            System.Threading.Interlocked.Increment(ref _winwsOpSeq);
        }

        // PID процессов winws, которые завершаем сами (Kill даёт код -1). Их
        // Exited не пишется в журнал как ошибка.
        static readonly HashSet<int> _intentionalExit = new HashSet<int>();
        static void MarkIntentionalExit(int pid)
        {
            lock (_intentionalExit)
            {
                if (_intentionalExit.Count > 64) _intentionalExit.Clear();
                _intentionalExit.Add(pid);
            }
        }
        static bool ConsumeIntentionalExit(int pid)
        {
            lock (_intentionalExit) { return _intentionalExit.Remove(pid); }
        }
        // Идёт бенчмарк/тест/переключение winws? Монитору падений такие
        // переходы не считаются падением. Чтение int атомарно само по себе.
        public static bool WinwsOperationActive()
        {
            return _winwsOperation != 0;
        }

        static string RegKey { get { return @"HKLM\System\CurrentControlSet\Services\zapret"; } }
        static string Q(string s) { return "\"" + s + "\""; }

        // Экранирование ОДНОГО аргумента командной строки по правилам
        // CommandLineToArgvW: обрамляем кавычками, внутренние кавычки пишем как \",
        // а идущие перед кавычкой обратные слэши удваиваем.
        //
        // Нужно для sc create: значение binPath само содержит кавычки (пути к
        // hostlist/bin-файлам в аргументах winws). Прежний код экранировал только
        // внешнюю пару кавычек, поэтому sc.exe разбирал строку не так, как
        // задумано, и служба не устанавливалась — гарантированно ломаясь на пути
        // по умолчанию «C:\Program Files\Lantern\zapret» из-за пробела.
        internal static string Arg(string value)
        {
            if (value == null) value = "";
            var sb = new System.Text.StringBuilder(value.Length + 8);
            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                int slashes = 0;
                while (i < value.Length && value[i] == '\\') { slashes++; i++; }
                if (i == value.Length) { sb.Append('\\', slashes * 2); break; }
                if (value[i] == '"') sb.Append('\\', slashes * 2 + 1).Append('"');
                else sb.Append('\\', slashes).Append(value[i]);
            }
            sb.Append('"');
            return sb.ToString();
        }

        // ---- winws процесс ----
        // Проверяем только экземпляры winws из выбранной папки zapret. Нельзя
        // управлять одноимённым процессом, который запустила другая программа.
        static bool IsRootWinws(Process p)
        {
            try
            {
                string file = p.MainModule.FileName;
                return string.Equals(Path.GetFullPath(file), Path.GetFullPath(WinwsExe), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        static List<Process> RootWinwsProcesses()
        {
            var result = new List<Process>();
            try
            {
                foreach (var p in Process.GetProcessesByName("winws"))
                {
                    if (IsRootWinws(p)) result.Add(p);
                    else p.Dispose();
                }
            }
            catch { }
            return result;
        }

        // winws.exe, запущенные НЕ из текущего корня zapret. KillWinws их не
        // трогает (см. IsRootWinws), но драйвер WinDivert у них общий: два winws
        // применяют десинк к одним и тем же пакетам.
        public static List<string> ForeignWinwsPaths()
        {
            var result = new List<string>();
            try
            {
                foreach (var p in Process.GetProcessesByName("winws"))
                {
                    try
                    {
                        if (IsRootWinws(p)) continue;
                        string file = null;
                        try { file = p.MainModule.FileName; } catch { }
                        result.Add(string.IsNullOrEmpty(file) ? "winws.exe (pid " + p.Id + ")" : file);
                    }
                    catch { }
                    finally { try { p.Dispose(); } catch { } }
                }
            }
            catch { }
            return result;
        }

        static DateTime _lastKnownWinwsStart;

        static void RememberWinws(Process p)
        {
            DateTime st = DateTime.MinValue;
            try { st = p.StartTime; } catch { }
            _lastKnownWinwsStart = st;
            _lastKnownWinwsPid = p.Id;
        }

        public static bool IsWinwsRunning()
        {
            int pid = _lastKnownWinwsPid;
            if (pid > 0)
            {
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        // Сверяем и время старта: PID мог достаться другому
                        // процессу (например, чужому winws), и быстрый путь
                        // отвечал бы «работает» за уже умерший экземпляр.
                        DateTime st = DateTime.MinValue;
                        try { st = p.StartTime; } catch { }
                        if (!p.HasExited && p.ProcessName.Equals("winws", StringComparison.OrdinalIgnoreCase)
                            && (_lastKnownWinwsStart == DateTime.MinValue || st == _lastKnownWinwsStart))
                            return true;
                    }
                }
                catch { }
                _lastKnownWinwsPid = -1;
            }

            var procs = RootWinwsProcesses();
            try
            {
                if (procs.Count > 0)
                {
                    RememberWinws(procs[0]);
                    return true;
                }
                return false;
            }
            finally { foreach (var p in procs) p.Dispose(); }
        }

        public static DateTime? WinwsProcessStartTime()
        {
            var procs = RootWinwsProcesses();
            try
            {
                if (procs.Count > 0)
                {
                    try { return procs[0].StartTime; } catch { }
                }
                return null;
            }
            finally
            {
                foreach (var p in procs) { try { p.Dispose(); } catch { } }
            }
        }

        public static bool StartWinws(string batFileName)
        {
            EnsureUserLists();
            string args;
            try { args = BuildArgs(batFileName); }
            catch (Exception ex) { Say(Sev.Err, "StartWinws: " + ex.Message); return false; }

            // service.bat на каждом запуске стратегии вызывает :tcp_enable —
            // netsh interface tcp set global timestamps=enabled. Группы правил с
            // --dpi-desync-fooling=ts без меток времени RFC 1323 не работают,
            // поэтому одна и та же стратегия из bat шла, а из приложения — нет.
            // Держим поведение bat.
            if (args.IndexOf("fooling=ts", StringComparison.OrdinalIgnoreCase) >= 0 && !TcpTimestampsEnabled())
            {
                if (EnableTcpTimestamps()) Good(Loc.T("winws.tsEnabled"));
                else Warn(Loc.T("winws.tsFailed"));
            }

            // Чужой winws (вторая копия zapret — например bat из репозитория при
            // запущенном приложении из C:\Program Files\Lantern\zapret) не
            // убивается KillWinws, потому что IsRootWinws сверяет точный путь exe.
            // Два winws применяют десинк к одним и тем же пакетам, обход ломается,
            // а в приложении всё выглядит запущенным. Молча убивать чужой процесс
            // нельзя — предупреждаем.
            var foreignPaths = ForeignWinwsPaths();
            if (foreignPaths.Count > 0)
                Warn(string.Format(Loc.T("winws.foreign"), foreignPaths.Count, string.Join("; ", foreignPaths.ToArray())));

            WinwsLogClear();
            var psi = new ProcessStartInfo
            {
                FileName = WinwsExe, Arguments = args,
                WorkingDirectory = Bin.TrimEnd(Path.DirectorySeparatorChar),
                UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            try
            {
                var p = Process.Start(psi);
                if (p == null) { Say(Sev.Err, "StartWinws: process was not created"); return false; }
                // Логи winws — в кольцевой буфер (см. журнал → «Вывод winws»).
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { WinwsLogAppend(e.Data); };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e) { WinwsLogAppend(e.Data); };
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                int pid = p.Id;
                var gate = new object();
                bool startup = true;
                p.EnableRaisingEvents = true;
                p.Exited += delegate
                {
                    // Пока идёт стартовая проверка, о падении сообщает она сама —
                    // иначе одна и та же ошибка писалась в журнал дважды.
                    lock (gate) { if (startup) return; }
                    // Kill() из KillWinws завершает процесс с кодом -1: это штатная
                    // остановка, а не ошибка.
                    if (ConsumeIntentionalExit(pid)) return;
                    int code = 0;
                    try { code = p.ExitCode; } catch { }
                    if (code != 0)
                    {
                        string tail = WinwsLogTail(2);
                        Say(Sev.Err, "winws exited with code " + code + (string.IsNullOrEmpty(tail) ? "" : ": " + tail.Trim()));
                    }
                };
                // Process.Start успешен даже если winws немедленно завершился
                // из-за неверных аргументов или отсутствующего драйвера.
                System.Threading.Thread.Sleep(300);
                bool exited;
                lock (gate)
                {
                    startup = false;
                    exited = p.HasExited;
                    // Exited может прийти уже после выхода из lock — помечаем PID,
                    // чтобы обработчик не продублировал сообщение ниже.
                    if (exited) MarkIntentionalExit(pid);
                }
                if (exited)
                {
                    string tail = WinwsLogTail(2);
                    Say(Sev.Err, "StartWinws: winws exited with code " + p.ExitCode + (string.IsNullOrEmpty(tail) ? "" : ": " + tail.Trim()));
                    p.Dispose();
                    return false;
                }
                if (_winwsProcess != null) { try { _winwsProcess.Dispose(); } catch { } }
                _winwsProcess = p;
                RememberWinws(p);
                StartedAt = DateTime.Now;
                return true;
            }
            catch (Exception ex) { Say(Sev.Err, "StartWinws: " + ex.Message); return false; }
        }

        public static bool KillWinws()
        {
            bool ok = true;
            var owned = _winwsProcess;
            _winwsProcess = null;
            _lastKnownWinwsPid = -1;
            // Dispose() процесс не завершает. winws обязан быть мёртв до
            // RemoveService()/sc delete, иначе служба остаётся «помеченной на
            // удаление», а драйвер WinDivert держит файлы в {app}\zapret\bin.
            if (owned != null)
            {
                try
                {
                    if (!owned.HasExited) { MarkIntentionalExit(owned.Id); owned.Kill(); }
                    if (!owned.WaitForExit(5000)) ok = false;
                }
                catch { ok = false; }
                try { owned.Dispose(); } catch { }
            }
            var procs = RootWinwsProcesses();
            try
            {
                foreach (var p in procs)
                {
                    try
                    {
                        if (!p.HasExited) { MarkIntentionalExit(p.Id); p.Kill(); }
                        if (!p.WaitForExit(5000)) ok = false;
                    }
                    catch { ok = false; }
                }
            }
            finally { foreach (var p in procs) p.Dispose(); }
            StartedAt = null;
            // Здесь больше НЕclear'ится кольцо WinwsLog*. KillWinws() вызывается
            // из проб и автоподбора (Core.Watchdog.cs, View.Check.cs), то есть журнал
            // стирался ровно перед тем, как его показывали в диагностике упавшей
            // стратегии. Буфер очищается перед следующим запуском в StartWinws.
            return ok;
        }

        // ---- WinDivert ----
        public static bool WinDivertFilePresent()
        {
            try { return File.Exists(WinDivertSys); } catch { return false; }
        }

        // ---- Служба (через прямой Win32 ServiceController) ----
        public static bool ServiceExists()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    // Чтение Status бросает InvalidOperationException, если служба не установлена.
                    var status = sc.Status;
                    return true;
                }
            }
            catch (InvalidOperationException) { return false; }
            catch { return false; }
        }

        // running / stopped / notinstalled
        public static string ServiceState()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    var status = sc.Status;
                    if (status == System.ServiceProcess.ServiceControllerStatus.Running ||
                        status == System.ServiceProcess.ServiceControllerStatus.StartPending)
                        return "running";
                    return "stopped";
                }
            }
            catch (InvalidOperationException) { return "notinstalled"; }
            catch { return "stopped"; }
        }

        // Стратегия, с которой установлена служба (из реестра, как делает service.bat).
        // reg query — дорогой запуск процесса: кэшируем на несколько секунд,
        // страницы статуса опрашивают это значение каждую секунду.
        static string _serviceStrategyCache;
        static bool _serviceStrategyCached;
        static DateTime _serviceStrategyAt;
        static readonly object _serviceStrategyLock = new object();
        public static void InvalidateServiceStrategyCache()
        {
            lock (_serviceStrategyLock)
            {
                _serviceStrategyCached = false;
                _serviceStrategyCache = null;
            }
        }
        public static string ServiceStrategy()
        {
            lock (_serviceStrategyLock)
            {
                if (_serviceStrategyCached && (DateTime.Now - _serviceStrategyAt).TotalSeconds < 4)
                    return _serviceStrategyCache;
            }
            string result = null;
            string o = Capture("reg", "query " + Q(RegKey) + " /v zapret-discord-youtube", 15000);
            foreach (var line in o.Split('\n'))
            {
                var t = line.Trim();
                int k = t.IndexOf("REG_SZ", StringComparison.OrdinalIgnoreCase);
                if (k >= 0) { result = t.Substring(k + 6).Trim(); break; }
            }
            lock (_serviceStrategyLock)
            {
                _serviceStrategyCache = result;
                _serviceStrategyCached = true;
                _serviceStrategyAt = DateTime.Now;
            }
            return result;
        }

        public static bool InstallService(string batFileName)
        {
            EnsureUserLists();
            RemoveService();
            KillWinws();
            WinwsLogClear();

            string args;
            try { args = BuildArgs(batFileName); }
            catch (Exception ex) { Say(Sev.Err, "InstallService: " + ex.Message); return false; }

            // ImagePath службы: "<путь к winws.exe>" <аргументы>. Путь берём в
            // кавычки — он может содержать пробелы; всю строку целиком передаём
            // sc.exe как один аргумент через Arg().
            string binPath = "\"" + WinwsExe + "\" " + args;
            string err;
            if (!Run("sc", "create " + ServiceName + " binPath= " + Arg(binPath) + " DisplayName= \"zapret\" start= auto", 20000, out err))
            {
                Say(Sev.Err, "InstallService: sc create failed (" + (err ?? "?") + ")");
                return false;
            }
            // Описание — косметика: его сбой не повод бросать установку.
            Run("sc", "description " + ServiceName + " \"Zapret DPI bypass software\"", 15000, out err);
            // отметим стратегию в реестре (как в service.bat) — до старта, чтобы
            // метка была и тогда, когда старт придётся повторить вручную
            string name = PrettyName(batFileName);
            if (!Run("reg", "add " + Q(RegKey) + " /v zapret-discord-youtube /t REG_SZ /d " + Q(name) + " /f", 15000, out err))
                Say(Sev.Warn, "InstallService: reg add failed (" + (err ?? "?") + ")");
            InvalidateServiceStrategyCache();
            if (!Run("sc", "start " + ServiceName, 20000, out err))
            {
                // Раньше здесь просто возвращали false: служба оставалась
                // установленной с start= auto и поднималась после перезагрузки
                // со стратегией, которая только что не запустилась.
                Say(Sev.Err, "InstallService: sc start failed (" + (err ?? "?") + "), removing service");
                Run("sc", "delete " + ServiceName, 15000, out err);
                InvalidateServiceStrategyCache();
                return false;
            }
            StartedAt = DateTime.Now;
            return true;
        }

public static bool StartService() { string err; bool ok = Run("sc", "start " + ServiceName, 20000, out err); if (ok) StartedAt = DateTime.Now; InvalidateServiceStrategyCache(); return ok; }
        public static bool StopService()  { string err; bool ok = Run("sc", "stop " + ServiceName, 20000, out err); if (ok) StartedAt = null; InvalidateServiceStrategyCache(); return ok; }

        // «sc stop» отвечает сразу, а служба в этот момент ещё STOP_PENDING: winws.exe
        // живёт до завершения. Без ожидания остановка считалась успешной, проверка
        // блокировок видела занятые файлы и обновление отменялось с «выключите
        // обход» — хотя обход как раз остановили. Ждём реальный STOPPED.
        public static bool WaitServiceStopped(int timeoutMs)
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController(ServiceName))
                {
                    if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped) return true;
                    try
                    {
                        sc.WaitForStatus(System.ServiceProcess.ServiceControllerStatus.Stopped,
                            TimeSpan.FromMilliseconds(timeoutMs));
                    }
                    catch { }
                    return sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped;
                }
            }
            catch { return false; }
        }

        public static bool RemoveService()
        {
            string err;
            Run("net", "stop " + ServiceName, 20000, out err);
            bool existed = ServiceExists();
            bool ok = !existed || Run("sc", "delete " + ServiceName, 15000, out err);
            KillWinws();
            Run("net", "stop WinDivert", 10000, out err);
            Run("sc", "delete WinDivert", 10000, out err);
            Run("net", "stop WinDivert14", 10000, out err);
            Run("sc", "delete WinDivert14", 10000, out err);
            InvalidateServiceStrategyCache();
            InvalidateWinDivertCache();
            StartedAt = null;
            return ok;
        }

        // ---- Проверка: работает ли WinDivert-сервис (значит драйвер загружен) ----
        // sc query — запуск процесса; страницы статуса опрашивают это каждую
        // секунду, поэтому короткое кэширование убирает постоянные спавны.
        static bool? _wdLoadedCache;
        static DateTime _wdLoadedAt;
        static readonly object _wdLock = new object();
        public static void InvalidateWinDivertCache() { lock (_wdLock) _wdLoadedCache = null; }
        public static bool WinDivertLoadedCached()
        {
            lock (_wdLock)
                if (_wdLoadedCache.HasValue && (DateTime.Now - _wdLoadedAt).TotalSeconds < 4)
                    return _wdLoadedCache.Value;
            bool v = WinDivertLoaded();
            lock (_wdLock) { _wdLoadedCache = v; _wdLoadedAt = DateTime.Now; }
            return v;
        }
        public static bool WinDivertLoaded()
        {
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController("WinDivert"))
                {
                    var status = sc.Status;
                    return status == System.ServiceProcess.ServiceControllerStatus.Running ||
                           status == System.ServiceProcess.ServiceControllerStatus.StopPending ||
                           status == System.ServiceProcess.ServiceControllerStatus.StartPending;
                }
            }
            catch { }
            try
            {
                using (var sc = new System.ServiceProcess.ServiceController("WinDivert14"))
                {
                    var status = sc.Status;
                    return status == System.ServiceProcess.ServiceControllerStatus.Running ||
                           status == System.ServiceProcess.ServiceControllerStatus.StopPending ||
                           status == System.ServiceProcess.ServiceControllerStatus.StartPending;
                }
            }
            catch { return false; }
        }

        // Перед заменой bin/WinDivert64.sys драйвер обязан быть выгружен:
        // KillWinws снимает winws.exe, но служба WinDivert остаётся запущенной
        // и держит файл. Обновление падало на проверке блокировок уже после
        // скачивания, и помочь могло только ручное удаление WinDivert.
        public static bool PrepareWinDivertForUpdate(out string error)
        {
            error = null;
            string driver = Path.Combine(Root ?? "", "bin", "WinDivert64.sys");
            if (!WinDivertLoaded() && !Locked(driver)) return true;

            string err;
            Run("sc", "stop windivert", 8000, out err);
            Run("sc", "stop windivert14", 8000, out err);
            Run("net", "stop windivert", 8000, out err);
            InvalidateWinDivertCache();
            if (WaitWinDivertUnloaded(driver, 6000)) { Info(Loc.T("mw.updWdStopped")); return true; }

            // Служба не отдала драйвер (зависший WinDivert) — удаляем её.
            // winws создаст службу заново при следующем запуске.
            Run("sc", "delete windivert", 5000, out err);
            Run("sc", "delete windivert14", 5000, out err);
            InvalidateWinDivertCache();
            if (WaitWinDivertUnloaded(driver, 4000)) { Info(Loc.T("mw.updWdStopped")); return true; }

            error = Loc.T("mw.updWdFail");
            return false;
        }

        static bool WaitWinDivertUnloaded(string driver, int timeoutMs)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < until)
            {
                InvalidateWinDivertCache();
                if (!WinDivertLoaded() && !Locked(driver)) return true;
                System.Threading.Thread.Sleep(200);
            }
            InvalidateWinDivertCache();
            return !WinDivertLoaded() && !Locked(driver);
        }

        // ---- TG-Proxy автозагрузка (реестр HKCU\...\Run) ----
        const string TgRunKey = "TgWsProxy";
        const string AppRunKey = "Lantern";
        const string AppTaskName = "LanternAutostart";
        const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        // Автозапуск самого приложения.
        //
        // Раньше это была запись в HKCU\...\Run, но манифест Lantern требует
        // requireAdministrator: при включённом UAC оболочка не может поднять права
        // для автозапуска и просто не запускает такой exe. Штатное решение для
        // admin-приложения — задача планировщика с «Run with highest privileges».
        static bool? _autostartCache;
        static DateTime _autostartAt;
        static readonly object _autostartLock = new object();

        public static bool AppAutostartEnabled()
        {
            lock (_autostartLock)
                if (_autostartCache.HasValue && (DateTime.Now - _autostartAt).TotalSeconds < 5)
                    return _autostartCache.Value;

            bool v = false;
            // schtasks печатает имя задачи в stdout только если она существует;
            // сообщение об отсутствии («ОШИБКА: Указанная задача не найдена»)
            // уходит в stderr, поэтому смотрим строго stdout — CaptureOut.
            string o = CaptureOut("schtasks", "/Query /TN " + Q(AppTaskName), 8000);
            if (o.IndexOf(AppTaskName, StringComparison.OrdinalIgnoreCase) >= 0) v = true;
            if (!v)
            {
                // Старый способ (до перехода на планировщик) — учитываем, чтобы
                // галочка в настройках не «слетала» после обновления.
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath))
                        v = key != null && key.GetValue(AppRunKey) != null;
                }
                catch { v = false; }
            }
            lock (_autostartLock) { _autostartCache = v; _autostartAt = DateTime.Now; }
            return v;
        }

        public static bool AppAutostartInTray()
        {
            return GetBool("autostart_tray", false);
        }

        public static void SetAppAutostartInTray(bool inTray)
        {
            SetBool("autostart_tray", inTray);
            SaveConfig();
            if (AppAutostartEnabled())
            {
                SetAppAutostart(true);
            }
        }

        public static void SetAppAutostart(bool enable)
        {
            lock (_autostartLock) _autostartCache = null;
            string err;
            // Старую запись в Run убираем всегда: она либо не работала, либо
            // дублировала бы задачу планировщика.
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                    if (key != null) key.DeleteValue(AppRunKey, false);
            }
            catch { }

            if (!enable)
            {
                Run("schtasks", "/Delete /F /TN " + Q(AppTaskName), 15000, out err);
                lock (_autostartLock) { _autostartCache = false; _autostartAt = DateTime.Now; }
                return;
            }

            string exe = null;
            try { exe = System.Reflection.Assembly.GetEntryAssembly().Location; }
            catch { }
            if (string.IsNullOrEmpty(exe)) { Warn("SetAppAutostart: exe path is unknown"); return; }

            bool inTray = AppAutostartInTray();
            string taskCmd = "\"" + exe + "\"" + (inTray ? " --tray" : "");

            // /RL HIGHEST — запуск с полными правами администратора (без запроса UAC),
            // /SC ONLOGON — при входе пользователя. /RU и /RP не указываем: задача
            // создаётся для текущего пользователя и не требует пароля.
            bool created = Run("schtasks", "/Create /F /TN " + Q(AppTaskName) +
                     " /TR " + Arg(taskCmd) + " /SC ONLOGON /RL HIGHEST", 15000, out err);
            bool fallbackOk = false;
            if (!created)
            {
                Warn("SetAppAutostart: schtasks failed (" + (err ?? "?") + ")");
                // Планировщик недоступен — возвращаемся к HKCU\Run как к
                // «лучше, чем ничего»: сработает, если UAC отключён.
                // CreateSubKey, а не OpenSubKey: ключа Run может просто не быть.
                try
                {
                    using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunRegistryPath))
                    {
                        if (key == null) throw new Exception(RunRegistryPath + " not openable");
                        key.SetValue(AppRunKey, taskCmd);
                        fallbackOk = true;
                    }
                }
                catch (Exception ex) { Warn("SetAppAutostart: " + ex.Message); }
            }
            // Кэш обязан отражать реальное состояние. Раньше _autostartCache = true
            // ставился безусловно — при отказе и планировщика, и реестра тумблер
            // показывал «включено», хотя автозапуска не было.
            lock (_autostartLock) { _autostartCache = created || fallbackOk; _autostartAt = DateTime.Now; }
        }

        public static bool TgAutostartEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath))
                    return key != null && key.GetValue(TgRunKey) != null;
            }
            catch { return false; }
        }

        public static void SetTgAutostart(bool enable)
        {
            if (enable && !TgProxyInstalled())
            {
                Warn("SetTgAutostart: TgWsProxy is not installed");
                return;
            }
            try
            {
                // CreateSubKey, а не OpenSubKey: ключа Run может не быть вовсе
                // (так же сделано для автозапуска самого приложения).
                using (var key = enable
                    ? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunRegistryPath)
                    : Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null)
                    {
                        if (enable) Warn("SetTgAutostart: registry key not openable");
                        return;
                    }
                    if (enable)
                    {
                        // Запуск через cmd.exe start /d задаёт рабочую папку TgToolsDir,
                        // чтобы прокси корректно находил свои локальные файлы, а не System32.
                        string cmd = "cmd.exe /c start /d \"" + TgToolsDir + "\" \"\" \"" + Path.GetFileName(TgProxyExe) + "\"";
                        key.SetValue(TgRunKey, cmd);
                    }
                    else
                    {
                        key.DeleteValue(TgRunKey, false);
                    }
                }
            }
            catch (Exception ex) { Warn("SetTgAutostart: " + ex.Message); }
        }

        // ---- Открыть папку в проводнике ----
        public static void OpenFolder(string path)
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
        }
        // Файлы остаются с повышенными правами намеренно: так открывают на
        // правку списки в Program Files, и Блокнот без прав их не сохранит.
        public static void OpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
        }
        public static void OpenUrl(string url)
        {
            if (ShellOpenUnelevated(url)) return;
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }

        // ---- Запуск без прав администратора ----
        // Приложение работает с requireAdministrator, и всё, что оно запускает
        // через Process.Start, наследует повышенный токен: браузер, Блокнот и
        // Telegram открывались «от администратора». Telegram, запущенный так,
        // нарушает drag&drop и не принимает ссылки от обычных программ (UIPI).

        // Ссылка или файл — через explorer.exe: он передаёт открытие уже
        // запущенной оболочке пользователя, и обработчик стартует с её правами.
        public static bool ShellOpenUnelevated(string target)
        {
            if (string.IsNullOrEmpty(target) || target.IndexOf('"') >= 0) return false;
            if (!IsAdmin()) return false;
            if (GetShellWindow() == IntPtr.Zero) return false;   // нет оболочки (Explorer не запущен)
            try
            {
                string explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                if (!File.Exists(explorer)) return false;
                using (Process.Start(new ProcessStartInfo { FileName = explorer, Arguments = "\"" + target + "\"", UseShellExecute = false })) { }
                return true;
            }
            catch { return false; }
        }

        // exe с аргументами — с токеном процесса оболочки (Explorer). Если не
        // вышло (нет оболочки, нет привилегии), возвращает false, и вызывающий
        // запускает как раньше.
        public static bool StartUnelevated(string exe, string args, string workDir)
        {
            if (!IsAdmin() || string.IsNullOrEmpty(exe)) return false;
            IntPtr hProc = IntPtr.Zero, hTok = IntPtr.Zero, hPrimary = IntPtr.Zero;
            try
            {
                IntPtr shell = GetShellWindow();
                if (shell == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(shell, out pid);
                if (pid == 0) return false;
                hProc = OpenProcess(0x0400 /*PROCESS_QUERY_INFORMATION*/, false, pid);
                if (hProc == IntPtr.Zero) return false;
                if (!OpenProcessToken(hProc, 0x0002 /*TOKEN_DUPLICATE*/, out hTok)) return false;
                // TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY | TOKEN_DUPLICATE | TOKEN_ADJUST_DEFAULT | TOKEN_ADJUST_SESSIONID
                if (!DuplicateTokenEx(hTok, 0x0008 | 0x0001 | 0x0002 | 0x0080 | 0x0100, IntPtr.Zero, 2 /*SecurityImpersonation*/, 1 /*TokenPrimary*/, out hPrimary))
                    return false;
                var si = new STARTUPINFO();
                si.cb = System.Runtime.InteropServices.Marshal.SizeOf(typeof(STARTUPINFO));
                PROCESS_INFORMATION pi;
                string cmd = "\"" + exe + "\"" + (string.IsNullOrEmpty(args) ? "" : " " + args);
                if (string.IsNullOrEmpty(workDir)) workDir = Path.GetDirectoryName(exe);
                if (!CreateProcessWithTokenW(hPrimary, 0, null, new System.Text.StringBuilder(cmd), 0, IntPtr.Zero, workDir, ref si, out pi))
                    return false;
                CloseHandle(pi.hThread);
                CloseHandle(pi.hProcess);
                return true;
            }
            catch { return false; }
            finally
            {
                if (hPrimary != IntPtr.Zero) CloseHandle(hPrimary);
                if (hTok != IntPtr.Zero) CloseHandle(hTok);
                if (hProc != IntPtr.Zero) CloseHandle(hProc);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        struct STARTUPINFO
        {
            public int cb;
            public string lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct PROCESS_INFORMATION
        {
            public IntPtr hProcess, hThread;
            public int dwProcessId, dwThreadId;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern IntPtr GetShellWindow();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
        static extern bool DuplicateTokenEx(IntPtr existing, uint access, IntPtr attrs, int impersonationLevel, int tokenType, out IntPtr newToken);
        [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern bool CreateProcessWithTokenW(IntPtr token, int logonFlags, string appName, System.Text.StringBuilder cmdLine,
            int creationFlags, IntPtr env, string currentDir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr h);

// ---- helpers: скрытый запуск (async read + timeout + kill) ----
        static bool Run(string file, string args, int timeoutMs, out string error)
        {
            error = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file, Arguments = args, UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { error = "timeout"; try { p.Kill(); } catch { } return false; }
                    p.WaitForExit();
                    int code = p.ExitCode;
                    if (code != 0) error = "exit code " + code;
                    return code == 0;
                }
            }
            catch (Exception ex) { error = Short(ex.Message); return false; }
        }
        // Консольные утилиты пишут в кодовой странице консоли (на русской
        // Windows это CP866), а не в UTF-8, прежний Encoding.UTF8 превращал
        // русский вывод sc.exe/schtasks в знаки замены и ломал диагностику.
        // Исключение составляет netsh: он при перенаправлении отдаёт UTF-8 (проверено
        // побайтово), для него CaptureUtf8.
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint GetConsoleOutputCP();
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern uint GetOEMCP();

        static System.Text.Encoding _consoleEnc;
        static System.Text.Encoding ConsoleEncoding
        {
            get
            {
                if (_consoleEnc == null)
                {
                    // У GUI-процесса своей консоли нет, и GetConsoleOutputCP()
                    // возвращает 0, а GetEncoding(0) — ANSI (1251) вместо OEM (866):
                    // русский вывод sc/schtasks превращался в кракозябры. Дочерние
                    // консольные утилиты (CreateNoWindow) пишут в OEM-кодировке.
                    try
                    {
                        uint cp = GetConsoleOutputCP();
                        if (cp == 0) cp = GetOEMCP();
                        _consoleEnc = System.Text.Encoding.GetEncoding((int)cp);
                    }
                    catch { _consoleEnc = System.Text.Encoding.Default; }
                }
                return _consoleEnc;
            }
        }

        // stdout+stderr одним буфером, OEM-кодировка (sc, reg, schtasks, net).
        static string Capture(string file, string args, int timeoutMs)
        {
            return CaptureCore(file, args, timeoutMs, ConsoleEncoding, true);
        }
        // Только stdout: «есть ли задача/служба» нельзя определять по тексту ошибки.
        static string CaptureOut(string file, string args, int timeoutMs)
        {
            return CaptureCore(file, args, timeoutMs, ConsoleEncoding, false);
        }
        // netsh → UTF-8.
        static string CaptureUtf8(string file, string args, int timeoutMs)
        {
            return CaptureCore(file, args, timeoutMs, new System.Text.UTF8Encoding(false), true);
        }

        static string CaptureCore(string file, string args, int timeoutMs, System.Text.Encoding enc, bool mergeStdErr)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file, Arguments = args, UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = enc, StandardErrorEncoding = enc
                };
                var so = new System.Text.StringBuilder();
                var se = new System.Text.StringBuilder();
                // Счётчик незакрытых ридеров: e.Data == null — документированный
                // признак конца потока. Ждём их явно, но ограниченно.
                object drainSync = new object();
                int drainPending = 2;
                using (var p = Process.Start(psi))
                {
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) { lock (so) so.AppendLine(e.Data); }
                        else lock (drainSync) { drainPending--; System.Threading.Monitor.PulseAll(drainSync); }
                    };
                    p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                    {
                        if (e.Data != null) { lock (se) se.AppendLine(e.Data); }
                        else lock (drainSync) { drainPending--; System.Threading.Monitor.PulseAll(drainSync); }
                    };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } }
                    // Прежний p.WaitForExit() без тайм-аута ждал вечно, если
                    // дочерний процесс унаследовал handle перенаправленного потока.
                    // Совсем не ждать тоже нельзя: вывод вернётся обрезанным, а его
                    // парсят ServiceStrategy/TcpTimestampsEnabled. WaitAll не
                    // подходит: он бросает исключение в STA-потоке (UI).
                    int deadline = Environment.TickCount + 3000;
                    lock (drainSync)
                    {
                        while (drainPending > 0)
                        {
                            int left = deadline - Environment.TickCount;
                            if (left <= 0) break;
                            System.Threading.Monitor.Wait(drainSync, left);
                        }
                    }
                    lock (so)
                    {
                        if (!mergeStdErr) return so.ToString();
                        lock (se) return so.ToString() + se.ToString();
                    }
                }
            }
            catch { return ""; }
        }

        // Добавление папки приложения в исключения Windows Defender, чтобы антивирус не удалял WinDivert и zapret.exe
        public static bool AddDefenderExclusion()
        {
            if (string.IsNullOrEmpty(Root)) return false;
            try
            {
                string cleanRoot = Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("'", "''");
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Add-MpPreference -ExclusionPath '" + cleanRoot + "'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        public static bool IsDefenderExclusionSet()
        {
            if (string.IsNullOrEmpty(Root)) return false;
            try
            {
                string cleanRoot = Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace("'", "''");
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$p = @((Get-MpPreference).ExclusionPath) | ForEach-Object { $_.TrimEnd('\\') }; if ($p -contains '" + cleanRoot + "') { exit 0 } else { exit 1 }\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return false; }
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        // Проверка: работает ли служба Защитника Windows (WinDefend).
        // Если у пользователя установлен сторонний антивирус (Касперский, Dr.Web и т.д.), Защитник отключён.
        public static bool IsDefenderServiceRunning()
        {
            try
            {
                string o = Capture("sc", "query WinDefend", 4000);
                return !string.IsNullOrEmpty(o) && o.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        // null = Защитник отключён (сторонний антивирус), true = в исключениях, false = не в исключениях
        public static bool? GetDefenderExclusionStatus()
        {
            if (!IsDefenderServiceRunning()) return null;
            return IsDefenderExclusionSet();
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr proc, IntPtr min, IntPtr max);

        // Освобождение неиспользуемой памяти процесса (тримминг рабочего набора в Windows)
        public static void TrimMemory()
        {
            try
            {
                GC.Collect(1, GCCollectionMode.Optimized);
                using (var cur = Process.GetCurrentProcess())
                {
                    SetProcessWorkingSetSize(cur.Handle, (IntPtr)(-1), (IntPtr)(-1));
                }
            }
            catch { }
        }
    }
}
