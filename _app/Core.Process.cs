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

        static readonly char BS = (char)92; // обратный слэш без литерала в исходнике
        static string RegKey { get { return "HKLM" + BS + "System" + BS + "CurrentControlSet" + BS + "Services" + BS + "zapret"; } }
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
            var procs = RootWinwsProcesses();
            try
            {
                return procs.Count > 0;
            }
            finally { foreach (var p in procs) p.Dispose(); }
        }

        // Системные TCP timestamps не нужны для работы winws. Не меняем глобальные
        // параметры Windows без отдельного действия пользователя.
        public static void EnableTcpTimestamps()
        {
            // Оставлено как совместимая точка вызова для старых сценариев.
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
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try
            {
                using (var p = Process.Start(psi))
                {
                    if (p == null) { Say(Sev.Err, "StartWinws: process was not created"); return false; }
                    // Process.Start успешен даже если winws немедленно завершился
                    // из-за неверных аргументов или отсутствующего драйвера.
                    System.Threading.Thread.Sleep(150);
                    if (p.HasExited)
                    {
                        Say(Sev.Err, "StartWinws: winws exited with code " + p.ExitCode);
                        return false;
                    }
                    StartedAt = DateTime.Now;
                    return true;
                }
            }
            catch (Exception ex) { Say(Sev.Err, "StartWinws: " + ex.Message); return false; }
        }

        public static bool KillWinws()
        {
            bool ok = true;
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

        // ---- Служба ----
        public static bool ServiceExists()
        {
            string o = Capture("sc", "query \"" + ServiceName + "\"", 15000);
            return o.IndexOf("STATE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // running / stopped / notinstalled
        public static string ServiceState()
        {
            string o = Capture("sc", "query \"" + ServiceName + "\"", 15000);
            if (o.IndexOf("STATE", StringComparison.OrdinalIgnoreCase) < 0) return "notinstalled";
            if (o.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0) return "running";
            return "stopped";
        }

        // Стратегия, с которой установлена служба (из реестра, как делает service.bat)
        public static string ServiceStrategy()
        {
            string o = Capture("reg", "query " + Q(RegKey) + " /v zapret-discord-youtube", 15000);
            foreach (var line in o.Split('\n'))
            {
                var t = line.Trim();
                int k = t.IndexOf("REG_SZ", StringComparison.OrdinalIgnoreCase);
                if (k >= 0) return t.Substring(k + 6).Trim();
            }
            return null;
        }

        public static bool InstallService(string batFileName)
        {
            EnsureUserLists();
            RemoveService();
            KillWinws();

            string args;
            try { args = BuildArgs(batFileName); }
            catch (Exception ex) { Say(Sev.Err, "InstallService: " + ex.Message); return false; }
            string q = "\\\"";
            string binPath = q + WinwsExe + q + " " + args.Replace("\"", "\\\"");
            if (!Run("sc", "create " + ServiceName + " binPath= \"" + binPath + "\" DisplayName= \"zapret\" start= auto", 20000)) return false;
            if (!Run("sc", "description " + ServiceName + " \"Zapret DPI bypass software\"", 15000)) return false;
            if (!Run("sc", "start " + ServiceName, 20000)) return false;
            // отметим стратегию в реестре (как в service.bat)
            string name = PrettyName(batFileName);
            if (!Run("reg", "add " + Q(RegKey) + " /v zapret-discord-youtube /t REG_SZ /d " + Q(name) + " /f", 15000)) return false;
            StartedAt = DateTime.Now;
            return true;
        }

        public static bool StartService() { bool ok = Run("sc", "start " + ServiceName, 20000); if (ok) StartedAt = DateTime.Now; return ok; }
        public static bool StopService()  { bool ok = Run("sc", "stop " + ServiceName, 20000); if (ok) StartedAt = null; return ok; }

        public static bool RemoveService()
        {
            Run("net", "stop " + ServiceName, 20000);
            bool existed = ServiceExists();
            bool ok = !existed || Run("sc", "delete " + ServiceName, 15000);
            KillWinws();
            StartedAt = null;
            return ok;
        }

        // ---- Проверка: работает ли WinDivert-сервис (значит драйвер загружен) ----
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
                    if (key == null) return;
                    if (enable)
                    {
                        string exe = System.Reflection.Assembly.GetEntryAssembly().Location;
                        key.SetValue(AppRunKey, "\"" + exe + "\"");
                    }
                    else key.DeleteValue(AppRunKey, false);
                }
            }
            catch { }
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
                    if (key == null) return;
                    if (enable)
                        key.SetValue(TgRunKey, "\"" + TgProxyExe + "\"");
                    else
                        key.DeleteValue(TgRunKey, false);
                }
            }
            catch { }
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
        static bool Run(string file, string args, int timeoutMs)
        {
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
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return false; }
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
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
    }
}
