using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace ZapretStudio
{
    // Управление внешней утилитой Flowseal/tg-ws-proxy как процессом.
    // Ничего не скачивается и не запускается без явного действия пользователя.
    static partial class Core
    {
        public const string TgProxyReleaseApi = "https://api.github.com/repos/Flowseal/tg-ws-proxy/releases/latest";
        public const string TgProxyReleasePage = "https://github.com/Flowseal/tg-ws-proxy/releases/latest";
        public const string TgProxyRepo = "https://github.com/Flowseal/tg-ws-proxy";

        // Кладём рядом с папкой zapret, в utils/tools, чтобы не мешать самому zapret.
        public static string TgToolsDir { get { return Path.Combine(Root, "utils", "tools"); } }
        public static string TgProxyExe { get { return Path.Combine(TgToolsDir, "TgWsProxy_windows.exe"); } }

        public static bool TgProxyInstalled() { return File.Exists(TgProxyExe); }

        // Локальная версия утилиты из метаданных файла (если заданы), иначе null.
        public static string TgProxyLocalVersion()
        {
            try
            {
                if (!File.Exists(TgProxyExe)) return null;
                var fvi = FileVersionInfo.GetVersionInfo(TgProxyExe);
                string v = fvi.ProductVersion ?? fvi.FileVersion;
                if (!string.IsNullOrEmpty(v))
                {
                    v = v.Trim().TrimStart('v', 'V');
                    string[] p = v.Split('.');
                    if (p.Length == 4 && p[3] == "0")
                        v = p[0] + "." + p[1] + "." + p[2];
                    return v;
                }
            }
            catch { }
            return null;
        }

        // Последняя версия из GitHub Releases (tag_name). Вызывать из фонового потока.
        public static string TgProxyLatestVersion()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Encoding = System.Text.Encoding.UTF8;
                    wc.Headers.Add("User-Agent", "ZapretStudio");
                    string json = wc.DownloadString(TgProxyReleaseApi);
                    var m = System.Text.RegularExpressions.Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                    if (m.Success) return m.Groups[1].Value.Trim();
                }
            }
            catch { }
            return null;
        }

        static Process _tgProc;
        static readonly object _tgLock = new object();

        public static bool TgProxyRunning()
        {
            try
            {
                lock (_tgLock) { if (_tgProc != null && !_tgProc.HasExited) return true; }
            }
            catch { }
            return TgProxyRunningByName();
        }

        static bool TgProxyRunningByName()
        {
            try
            {
                var procs = Process.GetProcessesByName("TgWsProxy_windows");
                try
                {
                    foreach (var p in procs)
                        if (!p.HasExited) return true;
                }
                finally { foreach (var p in procs) p.Dispose(); }
            }
            catch { }
            return false;
        }

        // URL для скачивания подходящей сборки под текущую архитектуру.
        public static string TgProxyDownloadUrl()
        {
            // По умолчанию — обычная x64 сборка Windows 10+.
            string asset = "TgWsProxy_windows.exe";
            try
            {
                var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
                if (arch.IndexOf("ARM", StringComparison.OrdinalIgnoreCase) >= 0)
                    asset = "TgWsProxy_windows_arm64.exe";
            }
            catch { }
            // Прямая ссылка на latest-ассет.
            return "https://github.com/Flowseal/tg-ws-proxy/releases/latest/download/" + asset;
        }

        // Запустить прокси (в трее, отдельное окно утилиты). Явно, видимо для пользователя.
        public static bool TgProxyStart(out string error)
        {
            error = null;
            lock (_tgLock)
            {
                try
                {
                    if (!TgProxyInstalled()) { error = Loc.T("tg.notInstalledErr"); return false; }
                    if (_tgProc != null && !_tgProc.HasExited) return true;
                    if (TgProxyRunningByName()) return true;
                    var psi = new ProcessStartInfo
                    {
                        FileName = TgProxyExe,
                        WorkingDirectory = TgToolsDir,
                        UseShellExecute = true
                    };
                    _tgProc = Process.Start(psi);
                    return true;
                }
                catch (Exception ex) { error = Short(ex.Message); return false; }
            }
        }

        public static void TgProxyStop()
        {
            lock (_tgLock)
            {
                try { if (_tgProc != null && !_tgProc.HasExited) _tgProc.Kill(); } catch { }
                try { if (_tgProc != null) _tgProc.Dispose(); } catch { }
                _tgProc = null;
            }
            try
            {
                var procs = Process.GetProcessesByName("TgWsProxy_windows");
                try { foreach (var p in procs) try { p.Kill(); } catch { } }
                finally { foreach (var p in procs) p.Dispose(); }
            }
            catch { }
        }
    }
}
