using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ZapretStudio
{
    // Запуск/остановка winws, служба Windows, статусы, WinDivert. Ничего не скрываем.
    static partial class Core
    {
        public static DateTime? StartedAt; // время запуска (для uptime), пока процессом управляет приложение

        static readonly char BS = (char)92; // обратный слэш без литерала в исходнике
        static string RegKey { get { return "HKLM" + BS + "System" + BS + "CurrentControlSet" + BS + "Services" + BS + "zapret"; } }
        static string Q(string s) { return "\"" + s + "\""; }

        // ---- winws процесс ----
        public static bool IsWinwsRunning()
        {
            try
            {
                var procs = Process.GetProcessesByName("winws");
                try { return procs.Length > 0; }
                finally { foreach (var p in procs) p.Dispose(); }
            }
            catch { return false; }
        }

        public static void EnableTcpTimestamps()
        {
            try { Run("netsh", "interface tcp set global timestamps=enabled", 15000); } catch { }
        }

        public static void StartWinws(string batFileName)
        {
            EnsureUserLists();
            EnableTcpTimestamps();
            string args;
            try { args = BuildArgs(batFileName); }
            catch (Exception ex) { Say(Sev.Err, "StartWinws: " + ex.Message); return; }
            var psi = new ProcessStartInfo
            {
                FileName = WinwsExe, Arguments = args,
                WorkingDirectory = Bin.TrimEnd(Path.DirectorySeparatorChar),
                UseShellExecute = false, CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try { Process.Start(psi); StartedAt = DateTime.Now; }
            catch (Exception ex) { Say(Sev.Err, "StartWinws: " + ex.Message); }
        }

        public static void KillWinws()
        {
            try { Run("taskkill", "/IM winws.exe /F", 15000); } catch { }
            StartedAt = null;
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

        public static void InstallService(string batFileName)
        {
            EnsureUserLists();
            EnableTcpTimestamps();
            RemoveService();
            KillWinws();

            string args;
            try { args = BuildArgs(batFileName); }
            catch (Exception ex) { Say(Sev.Err, "InstallService: " + ex.Message); return; }
            string q = "\\\"";
            string binPath = q + WinwsExe + q + " " + args.Replace("\"", "\\\"");
            Run("sc", "create " + ServiceName + " binPath= \"" + binPath + "\" DisplayName= \"zapret\" start= auto", 20000);
            Run("sc", "description " + ServiceName + " \"Zapret DPI bypass software\"", 15000);
            Run("sc", "start " + ServiceName, 20000);
            // отметим стратегию в реестре (как в service.bat)
            string name = PrettyName(batFileName);
            Run("reg", "add " + Q(RegKey) + " /v zapret-discord-youtube /t REG_SZ /d " + Q(name) + " /f", 15000);
            StartedAt = DateTime.Now;
        }

        public static void StartService() { Run("sc", "start " + ServiceName, 20000); StartedAt = DateTime.Now; }
        public static void StopService()  { Run("sc", "stop " + ServiceName, 20000); StartedAt = null; }

        public static void RemoveService()
        {
            Run("net", "stop " + ServiceName, 20000);
            Run("sc", "delete " + ServiceName, 15000);
            KillWinws();
            Run("net", "stop WinDivert", 15000);
            Run("sc", "delete WinDivert", 15000);
            Run("net", "stop WinDivert14", 15000);
            Run("sc", "delete WinDivert14", 15000);
            StartedAt = null;
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
        public static bool TgAutostartEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    return key != null && key.GetValue(TgRunKey) != null;
            }
            catch { return false; }
        }

        public static void SetTgAutostart(bool enable)
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
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
        static void Run(string file, string args, int timeoutMs)
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
                    if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } }
                }
            }
            catch { }
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
