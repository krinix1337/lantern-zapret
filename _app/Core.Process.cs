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
            return System.Threading.Interlocked.CompareExchange(ref _winwsOperation, 1, 0) == 0;
        }
        public static void EndWinwsOperation()
        {
            System.Threading.Interlocked.Exchange(ref _winwsOperation, 0);
        }
        // Идёт бенчмарк/тест/переключение winws? Монитору падений такие
        // переходы не считаются падением. Чтение int атомарно само по себе.
        public static bool WinwsOperationActive()
        {
            return _winwsOperation != 0;
        }

        static string RegKey { get { return @"HKLM\System\CurrentControlSet\Services\zapret"; } }
        static string Q(string s) { return "\"" + s + "\""; }

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

        public static bool IsWinwsRunning()
        {
            if (_lastKnownWinwsPid > 0)
            {
                try
                {
                    using (var p = Process.GetProcessById(_lastKnownWinwsPid))
                    {
                        if (!p.HasExited && p.ProcessName.Equals("winws", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                catch { _lastKnownWinwsPid = -1; }
            }

            var procs = RootWinwsProcesses();
            try
            {
                if (procs.Count > 0)
                {
                    _lastKnownWinwsPid = procs[0].Id;
                    return true;
                }
                return false;
            }
            finally { foreach (var p in procs) p.Dispose(); }
        }

        // Системные TCP timestamps не нужны для работы winws. Не меняем глобальные
        // параметры Windows без отдельного действия пользователя.
[Obsolete]
        public static void EnableTcpTimestamps()
        {
        }

        public static bool StartWinws(string batFileName)
        {
            EnsureUserLists();
            string args;
            try { args = BuildArgs(batFileName); }
            catch (Exception ex) { Say(Sev.Err, "StartWinws: " + ex.Message); return false; }
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
                // Process.Start успешен даже если winws немедленно завершился
                // из-за неверных аргументов или отсутствующего драйвера.
                System.Threading.Thread.Sleep(150);
                if (p.HasExited)
                {
                    Say(Sev.Err, "StartWinws: winws exited with code " + p.ExitCode);
                    p.Dispose();
                    return false;
                }
                if (_winwsProcess != null) { try { _winwsProcess.Dispose(); } catch { } }
                _winwsProcess = p;
                _lastKnownWinwsPid = p.Id;
                StartedAt = DateTime.Now;
                return true;
            }
            catch (Exception ex) { Say(Sev.Err, "StartWinws: " + ex.Message); return false; }
        }

        public static bool KillWinws()
        {
            bool ok = true;
            _lastKnownWinwsPid = -1;
            if (_winwsProcess != null)
            {
                try { _winwsProcess.Dispose(); } catch { }
                _winwsProcess = null;
            }
            var procs = RootWinwsProcesses();
            try
            {
                foreach (var p in procs)
                {
                    try { p.Kill(); if (!p.WaitForExit(5000)) ok = false; }
                    catch { ok = false; }
                }
            }
            finally { foreach (var p in procs) p.Dispose(); }
            StartedAt = null;
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
        static DateTime _serviceStrategyAt;
        static readonly object _serviceStrategyLock = new object();
        public static void InvalidateServiceStrategyCache() { lock (_serviceStrategyLock) _serviceStrategyCache = null; }
        public static string ServiceStrategy()
        {
            lock (_serviceStrategyLock)
            {
                if (_serviceStrategyCache != null && (DateTime.Now - _serviceStrategyAt).TotalSeconds < 4)
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
                _serviceStrategyAt = DateTime.Now;
            }
            return result;
        }

        public static bool InstallService(string batFileName)
        {
            EnsureUserLists();
            RemoveService();
            KillWinws();

            string args;
            try { args = BuildArgs(batFileName); }
            catch (Exception ex) { Say(Sev.Err, "InstallService: " + ex.Message); return false; }
            string escapedBinPath = "\\\"" + WinwsExe + "\\\" " + args;
            string err;
            if (!Run("sc", "create " + ServiceName + " binPath= " + Q(escapedBinPath) + " DisplayName= \"zapret\" start= auto", 20000, out err)) return false;
            if (!Run("sc", "description " + ServiceName + " \"Zapret DPI bypass software\"", 15000, out err)) return false;
            if (!Run("sc", "start " + ServiceName, 20000, out err)) return false;
            // отметим стратегию в реестре (как в service.bat)
            string name = PrettyName(batFileName);
            if (!Run("reg", "add " + Q(RegKey) + " /v zapret-discord-youtube /t REG_SZ /d " + Q(name) + " /f", 15000, out err)) return false;
            InvalidateServiceStrategyCache();
            StartedAt = DateTime.Now;
            return true;
        }

public static bool StartService() { string err; bool ok = Run("sc", "start " + ServiceName, 20000, out err); if (ok) StartedAt = DateTime.Now; InvalidateServiceStrategyCache(); return ok; }
        public static bool StopService()  { string err; bool ok = Run("sc", "stop " + ServiceName, 20000, out err); if (ok) StartedAt = null; InvalidateServiceStrategyCache(); return ok; }

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
            string o = Capture("sc", "query WinDivert", 15000);
            return o.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0
                || o.IndexOf("STOP_PENDING", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ---- TG-Proxy автозагрузка (реестр HKCU\...\Run) ----
        const string TgRunKey = "TgWsProxy";
        const string AppRunKey = "Lantern";
        const string RunRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public static bool AppAutostartEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath))
                    return key != null && key.GetValue(AppRunKey) != null;
            }
            catch { return false; }
        }

        public static void SetAppAutostart(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null) { Warn("SetAppAutostart: registry key not found"); return; }
                    if (enable)
                    {
                        string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                        key.SetValue(AppRunKey, "\"" + exe + "\"");
                    }
                    else key.DeleteValue(AppRunKey, false);
                }
            }
            catch (Exception ex) { Warn("SetAppAutostart: " + ex.Message); }
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
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunRegistryPath, true))
                {
                    if (key == null) { Warn("SetTgAutostart: registry key not found"); return; }
                    if (enable)
                        key.SetValue(TgRunKey, "\"" + TgProxyExe + "\"");
                    else
                        key.DeleteValue(TgRunKey, false);
                }
            }
            catch (Exception ex) { Warn("SetTgAutostart: " + ex.Message); }
        }

        // ---- Открыть папку в проводнике ----
        public static void OpenFolder(string path)
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
        }
        public static void OpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); } catch { }
        }
        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }

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
        static string Capture(string file, string args, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = file, Arguments = args, UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };
                var sb = new System.Text.StringBuilder();
                using (var p = Process.Start(psi))
                {
                    p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return ""; }
                    // Дать async-ридерам завершить
                    p.WaitForExit();
                    lock (sb) return sb.ToString();
                }
            }
            catch { return ""; }
        }

        // Добавление папки приложения в исключения Windows Defender, чтобы антивирус не удалял WinDivert и zapret.exe
        public static bool AddDefenderExclusion()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Add-MpPreference -ExclusionPath '" + Root.Replace("'", "''") + "'\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(10000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        public static bool IsDefenderExclusionSet()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$p = (Get-MpPreference).ExclusionPath; if ($p -contains '" + Root.Replace("'", "''") + "') { exit 0 } else { exit 1 }\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(5000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }
    }
}
