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
        public static string TgProxyReleaseApi { get { return Endpoints.TgProxyReleaseApi; } }
        public static string TgProxyReleasePage { get { return Endpoints.TgProxyReleasePage; } }
        public static string TgProxyRepo { get { return Endpoints.TgProxyRepo; } }

        // Кладём рядом с папкой zapret, в utils/tools, чтобы не мешать самому zapret.
        // Root может быть пуст до первичной настройки — не роняем Path.Combine(null,...).
        public static string TgToolsDir
        {
            get { return Path.Combine(string.IsNullOrEmpty(Root) ? AppDomain.CurrentDomain.BaseDirectory : Root, "utils", "tools"); }
        }
        public static string TgProxyExe { get { return Path.Combine(TgToolsDir, "TgWsProxy_windows.exe"); } }

        public static bool TgProxyInstalled() { return File.Exists(TgProxyExe); }

        // Апстрим не обновляет метаданные версии в TgWsProxy_windows.exe: в
        // релизе 1.10.1 файл по-прежнему помечен как 1.10.0.0. Поэтому после
        // успешной установки запоминаем тег релиза вместе с отпечатком файла
        // (размер и время записи). Пока отпечаток совпадает, метка достовернее
        // метаданных; если файл подменили вручную — метка игнорируется.
        static string TgProxyFingerprint()
        {
            try
            {
                var fi = new FileInfo(TgProxyExe);
                if (!fi.Exists) return null;
                return fi.Length.ToString() + ":" + fi.LastWriteTimeUtc.ToString("yyyyMMddHHmmss");
            }
            catch { return null; }
        }

        public static void TgProxyMarkInstalled(string version)
        {
            string v = (version ?? "").Trim().TrimStart('v', 'V');
            string fp = TgProxyFingerprint();
            if (v.Length == 0 || fp == null) return;
            Set("tg_installed", v + "|" + fp);
            SaveConfig();
        }

        static string TgProxyStampVersion()
        {
            string raw = Get("tg_installed", "");
            if (string.IsNullOrEmpty(raw)) return null;
            int bar = raw.IndexOf('|');
            if (bar <= 0) return null;
            string fp = TgProxyFingerprint();
            if (fp == null || fp != raw.Substring(bar + 1)) return null;
            return raw.Substring(0, bar);
        }

        // Локальная версия утилиты: сначала метка установки, затем метаданные файла.
        public static string TgProxyLocalVersion()
        {
            string stamp = TgProxyStampVersion();
            if (!string.IsNullOrEmpty(stamp)) return stamp;
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
            return FetchLatestTag(TgProxyReleaseApi, TgProxyReleasePage);
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
