using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZapretStudio
{
    // Настройки, которые tg-ws-proxy хранит в config.json. Читаются как есть:
    // Lantern ничего туда не пишет, чтобы не спорить с настройками утилиты.
    public sealed class TgProxySettings
    {
        public string Host;
        public int Port;
        public string Secret;

        // 0.0.0.0 — адрес прослушивания, а не адрес для Telegram. Для ссылки
        // берём loopback: прокси на этой же машине, порт всё равно слушается.
        public string LinkHost
        {
            get
            {
                string h = (Host ?? "").Trim();
                if (h.Length == 0 || h == "0.0.0.0" || h == "*" || h == "::") return "127.0.0.1";
                return h;
            }
        }

        public string Endpoint { get { return LinkHost + ":" + Port; } }

        // Формат ссылки повторяет tg_proxy_url() из tg-ws-proxy.
        public string Link { get { return "tg://proxy?server=" + LinkHost + "&port=" + Port + "&secret=dd" + Secret; } }
    }

    // Клиент Telegram, найденный на машине. Path — exe, который можно запустить
    // со ссылкой в аргументах. UWP-клиенты (Unigram) так не адресуются.
    public sealed class TgProxyClient
    {
        public string Name;
        public string Path;
        public bool IsDefault;
    }

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

        public static void TgProxyMarkInstalled(string version, string sha256)
        {
            string v = (version ?? "").Trim().TrimStart('v', 'V');
            string fp = TgProxyFingerprint();
            if (v.Length == 0 || fp == null) return;
            string hash = (sha256 ?? "").Trim().ToLowerInvariant();
            Set("tg_installed", v + "|" + fp + (hash.Length == 64 ? "|" + hash : ""));
            SaveConfig();
        }

        static string TgProxyStoredHash()
        {
            string raw = Get("tg_installed", "");
            if (string.IsNullOrEmpty(raw)) return null;
            string[] parts = raw.Split('|');
            if (parts.Length < 3) return null;
            string hash = parts[2].Trim().ToLowerInvariant();
            return hash.Length == 64 ? hash : null;
        }

        // Повреждение установленного TG-прокси: файл есть, но пустой либо не
        // совпадает с SHA-256, сохранённым при установке. Старые установки без
        // хэша проверяются только по наличию файла.
        public static List<string> TgProxyIntegrityIssues()
        {
            var issues = new List<string>();
            if (!TgProxyInstalled()) return issues;
            try
            {
                FileInfo fi = new FileInfo(TgProxyExe);
                if (fi.Length <= 0)
                {
                    issues.Add(string.Format(Loc.T("updates.integrity.corruptFile"), "TgWsProxy_windows.exe"));
                    return issues;
                }
                string stored = TgProxyStoredHash();
                if (stored != null)
                {
                    string actual = Sha256OfFile(TgProxyExe);
                    if (!string.Equals(actual, stored, StringComparison.OrdinalIgnoreCase))
                        issues.Add(string.Format(Loc.T("updates.integrity.corruptFile"), "TgWsProxy_windows.exe"));
                }
            }
            catch (Exception ex)
            {
                issues.Add(string.Format(Loc.T("updates.integrity.unreadableFile"), "TgWsProxy_windows.exe", Short(ex.Message)));
            }
            return issues;
        }

        static string TgProxyStampVersion()
        {
            string raw = Get("tg_installed", "");
            if (string.IsNullOrEmpty(raw)) return null;
            // Формат: version|fingerprint[|sha256]
            string[] parts = raw.Split('|');
            if (parts.Length < 2) return null;
            string fp = TgProxyFingerprint();
            if (fp == null || fp != parts[1]) return null;
            return parts[0];
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

        // Апстрим хранит настройки в %APPDATA%\TgWsProxy\config.json; в
        // portable-режиме — в TgWsProxy_data рядом с exe. Путь повторяет
        // _app_dir() из tg-ws-proxy, иначе Lantern читал бы чужой конфиг.
        public static string TgProxyConfigPath()
        {
            try
            {
                string portable = Path.Combine(TgToolsDir, "TgWsProxy_data");
                if (Directory.Exists(portable)) return Path.Combine(portable, "config.json");
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                if (string.IsNullOrEmpty(appData)) return null;
                return Path.Combine(appData, "TgWsProxy", "config.json");
            }
            catch { return null; }
        }

        static bool IsTgProxySecret(string secret)
        {
            if (secret == null || secret.Length != 32) return false;
            for (int i = 0; i < secret.Length; i++)
            {
                char c = secret[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }

        // Прочитать актуальные host/port/secret. Ошибка — человекочитаемая:
        // её показываем тостом, техническую причину оставляем в журнале.
        public static bool TryTgProxySettings(out TgProxySettings settings, out string error)
        {
            settings = null;
            error = null;
            string path = TgProxyConfigPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                error = Loc.T("ov.tg.cfgMissing");
                return false;
            }
            try
            {
                string json = File.ReadAllText(path);
                return ParseTgProxySettings(json, out settings, out error);
            }
            catch (Exception ex)
            {
                error = Short(ex.Message);
                return false;
            }
        }

        // Отдельно от файла — чтобы Selftest проверял разбор без похода в %APPDATA%.
        public static bool ParseTgProxySettings(string json, out TgProxySettings settings, out string error)
        {
            settings = null;
            error = null;
            if (string.IsNullOrEmpty(json))
            {
                error = Loc.T("ov.tg.cfgInvalid");
                return false;
            }
            string host = Endpoints.JsonField(json, "host");
            string portText = Endpoints.JsonNumberField(json, "port");
            if (string.IsNullOrEmpty(portText)) portText = Endpoints.JsonField(json, "port");
            string secret = Endpoints.JsonField(json, "secret");
            int port;
            if (string.IsNullOrEmpty(host)) host = "127.0.0.1";
            if (string.IsNullOrEmpty(portText) || !int.TryParse(portText, out port) || port < 1 || port > 65535)
            {
                error = Loc.T("ov.tg.cfgInvalid");
                return false;
            }
            if (!IsTgProxySecret(secret))
            {
                error = Loc.T("ov.tg.cfgInvalid");
                return false;
            }
            settings = new TgProxySettings { Host = host.Trim(), Port = port, Secret = secret.ToLowerInvariant() };
            return true;
        }

        // Последняя версия из GitHub Releases (tag_name). Вызывать из фонового потока.
        public static string TgProxyLatestVersion()
        {
            return FetchLatestTag(TgProxyReleaseApi, TgProxyReleasePage);
        }

        static Process _tgProc;
        static readonly object _tgLock = new object();
        // Суммарное ожидание завершения процессов прокси в TgProxyStop.
        const int StopBudgetMs = 3000;
        static volatile bool _tgStopping;

        public static bool TgProxyRunning()
        {
            if (_tgStopping) return false;
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

        // Имя ассета релиза под текущую платформу. Апстрим собирает четыре
        // варианта, и прежняя угадайка «x64 или arm64» ставила 64-битную сборку
        // для Windows 10+ на 32-битную систему и на Windows 7/8.
        public static string TgProxyAssetName()
        {
            try
            {
                // В 32-битном процессе на 64-битной Windows PROCESSOR_ARCHITECTURE
                // равен x86, настоящая архитектура лежит в PROCESSOR_ARCHITEW6432.
                string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
                if (string.IsNullOrEmpty(arch)) arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") ?? "";
                if (arch.IndexOf("ARM", StringComparison.OrdinalIgnoreCase) >= 0) return "TgWsProxy_windows_arm64.exe";
                if (arch.IndexOf("64", StringComparison.OrdinalIgnoreCase) < 0) return "TgWsProxy_windows_7_32bit.exe";
                if (Environment.OSVersion.Version.Major < 10) return "TgWsProxy_windows_7_64bit.exe";
            }
            catch { }
            return "TgWsProxy_windows.exe";
        }

        // Скачать TG-прокси из релиза, сверить SHA-256 и только потом заменить файл.
        // Версию помечает сам: прежняя метка ставилась сразу после DownloadFile,
        // поэтому обрезанный exe закреплялся как «установленная версия X», кнопка
        // «Обновить» исчезала, а TgProxyStart его потом запускал.
        // Локальное имя файла менять нельзя: TgProxyRunningByName() ищет процесс
        // строго по имени «TgWsProxy_windows».
        // Вызывать из фонового потока.
        public static bool TgProxyInstall(Action<DlProgress> onProgress, out string error)
        {
            error = null;
            string asset = TgProxyAssetName();
            string json = FetchReleaseJson(TgProxyReleaseApi);
            if (string.IsNullOrEmpty(json)) { error = Loc.T("dl.hashMissing"); return false; }

            string url = Endpoints.ReleaseAssetUrl(json, asset);
            if (string.IsNullOrEmpty(url))
            {
                error = string.Format(Loc.T("tg.noAsset"), asset, TgProxyReleasePage);
                Warn(error);
                return false;
            }
            string expected = Endpoints.ReleaseAssetDigest(json, asset);
            string tag = Endpoints.JsonField(json, "tag_name");

            string tmp = Path.Combine(Path.GetTempPath(), "TgWsProxy-" + Guid.NewGuid().ToString("N") + ".exe");
            bool replaced = false;
            try
            {
                Info(string.Format(Loc.T("tg.dlSource"), asset, url));
                if (!DownloadVerified(url, expected, tmp, onProgress, null, out error)) return false;
                try { Directory.CreateDirectory(TgToolsDir); } catch { }
                ReplaceWithBackup(tmp, TgProxyExe);
                replaced = true;
                string installedHash = null;
                try { installedHash = Sha256OfFile(TgProxyExe); } catch { }
                TgProxyMarkInstalled(tag, installedHash);
                return true;
            }
            catch (Exception ex) { error = Short(ex.Message); Warn("TgProxyInstall: " + error); return false; }
            finally { if (!replaced) { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } } }
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
            _tgStopping = true;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                lock (_tgLock)
                {
                    try { if (_tgProc != null && !_tgProc.HasExited) _tgProc.Kill(); } catch { }
                }
                try
                {
                    string expectedExe = null;
                    try { expectedExe = Path.GetFullPath(TgProxyExe); } catch { }

                    var procs = Process.GetProcessesByName("TgWsProxy_windows");
                    try
                    {
                        foreach (var p in procs)
                        {
                            try
                            {
                                bool match;
                                if (string.IsNullOrEmpty(expectedExe)) match = true;
                                else
                                {
                                    string fn = null;
                                    bool unreadable = false;
                                    try { fn = p.MainModule != null ? p.MainModule.FileName : null; }
                                    catch { unreadable = true; }

                                    if (!unreadable && !string.IsNullOrEmpty(fn))
                                        match = string.Equals(Path.GetFullPath(fn), expectedExe, StringComparison.OrdinalIgnoreCase);
                                    else
                                    {
                                        // MAIN_MODULE недоступна (32-битный процесс
                                        // из 64-битного, отказ в правах), поэтому молча убивать
                                        // всё подряд нельзя, но и оставить наш прокси
                                        // висеть нельзя. Убиваем по совпадению имени
                                        // и обязательно пишем в журнал, что это был
                                        // неатрибутированный процесс.
                                        match = true;
                                        Warn(string.Format(Loc.T("tg.stopUnmatched"), p.Id));
                                    }
                                }
                                if (!match) continue;
                                p.Kill();
                                // Общий бюджет ожидания вместо 3000 + 2000 мс
                                // подряд на каждый процесс.
                                int left = StopBudgetMs - (int)sw.ElapsedMilliseconds;
                                if (left > 0) p.WaitForExit(left);
                            }
                            catch { }
                        }
                    }
                    finally { foreach (var p in procs) p.Dispose(); }
                }
                catch { }
                lock (_tgLock) { try { if (_tgProc != null) _tgProc.Dispose(); } catch { } _tgProc = null; }
            }
            finally
            {
                _tgStopping = false;
            }
        }

        // ---------- Клиенты Telegram ----------

        // Найти установленные Win32-клиенты. UWP (Unigram) сюда не попадает:
        // у него нет exe, который можно запустить со ссылкой в аргументах;
        // для него работает только системный обработчик tg://.
        public static List<TgProxyClient> TgProxyClients()
        {
            var list = new List<TgProxyClient>();
            string def = TgProtocolHandlerExe();
            foreach (string p in TgKnownClientPaths()) AddTgClient(list, p, def);
            if (!string.IsNullOrEmpty(def)) AddTgClient(list, def, def);
            try { AddStartMenuTgClients(list, def); } catch { }
            list.Sort(delegate(TgProxyClient a, TgProxyClient b)
            {
                if (a.IsDefault != b.IsDefault) return a.IsDefault ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        static IEnumerable<string> TgKnownClientPaths()
        {
            var paths = new List<string>();
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrEmpty(appData))
            {
                paths.Add(Path.Combine(appData, "Telegram Desktop", "Telegram.exe"));
                paths.Add(Path.Combine(appData, "AyuGram Desktop", "AyuGram.exe"));
                paths.Add(Path.Combine(appData, "64Gram Desktop", "64Gram.exe"));
                paths.Add(Path.Combine(appData, "Kotatogram Desktop", "Kotatogram.exe"));
            }
            if (!string.IsNullOrEmpty(local))
            {
                paths.Add(Path.Combine(local, "Telegram Desktop", "Telegram.exe"));
                paths.Add(Path.Combine(local, "Programs", "Telegram Desktop", "Telegram.exe"));
                paths.Add(Path.Combine(local, "AyuGram Desktop", "AyuGram.exe"));
                paths.Add(Path.Combine(local, "Programs", "AyuGram Desktop", "AyuGram.exe"));
                paths.Add(Path.Combine(local, "64Gram Desktop", "64Gram.exe"));
                paths.Add(Path.Combine(local, "Programs", "64Gram Desktop", "64Gram.exe"));
                paths.Add(Path.Combine(local, "Kotatogram Desktop", "Kotatogram.exe"));
                paths.Add(Path.Combine(local, "Programs", "Kotatogram Desktop", "Kotatogram.exe"));
            }
            if (!string.IsNullOrEmpty(docs)) paths.Add(Path.Combine(docs, "AyuGram", "AyuGram.exe"));
            if (!string.IsNullOrEmpty(user)) paths.Add(Path.Combine(user, "Documents", "AyuGram", "AyuGram.exe"));
            if (!string.IsNullOrEmpty(pf))
            {
                paths.Add(Path.Combine(pf, "Telegram Desktop", "Telegram.exe"));
                paths.Add(Path.Combine(pf, "AyuGram Desktop", "AyuGram.exe"));
            }
            if (!string.IsNullOrEmpty(pf86))
            {
                paths.Add(Path.Combine(pf86, "Telegram Desktop", "Telegram.exe"));
                paths.Add(Path.Combine(pf86, "AyuGram Desktop", "AyuGram.exe"));
            }
            return paths;
        }

        static void AddStartMenuTgClients(List<TgProxyClient> list, string def)
        {
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs")
            };
            foreach (string root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                string[] links;
                try { links = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories); }
                catch { continue; }
                foreach (string lnk in links)
                {
                    string n = Path.GetFileNameWithoutExtension(lnk).ToLowerInvariant();
                    if (n.IndexOf("telegram") < 0 && n.IndexOf("ayugram") < 0 &&
                        n.IndexOf("64gram") < 0 && n.IndexOf("kotatogram") < 0) continue;
                    if (n.IndexOf("uninstall") >= 0 || n.IndexOf("updat") >= 0) continue;
                    string target = ResolveShortcut(lnk);
                    if (!string.IsNullOrEmpty(target)) AddTgClient(list, target, def);
                }
            }
        }

        static string ResolveShortcut(string lnk)
        {
            object shell = null, shortcut = null;
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) return null;
                shell = Activator.CreateInstance(t);
                shortcut = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                if (shortcut == null) return null;
                object target = shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                return target as string;
            }
            catch { return null; }
            finally
            {
                try { if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut); } catch { }
                try { if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell); } catch { }
            }
        }

        static string TgProtocolHandlerExe()
        {
            string[] keys =
            {
                @"HKEY_CURRENT_USER\Software\Classes\tg\shell\open\command",
                @"HKEY_LOCAL_MACHINE\Software\Classes\tg\shell\open\command"
            };
            foreach (string key in keys)
            {
                try
                {
                    string cmd = Registry.GetValue(key, "", null) as string;
                    string exe = TgExeFromCommand(cmd);
                    if (!string.IsNullOrEmpty(exe) && File.Exists(exe) && !IsSystemLauncher(exe)) return exe;
                }
                catch { }
            }
            return null;
        }

        static string TgExeFromCommand(string cmd)
        {
            if (string.IsNullOrEmpty(cmd)) return null;
            cmd = cmd.Trim();
            string path = null;
            if (cmd[0] == '"')
            {
                int end = cmd.IndexOf('"', 1);
                if (end > 1) path = cmd.Substring(1, end - 1);
            }
            else
            {
                int pos = cmd.ToLowerInvariant().IndexOf(".exe", StringComparison.Ordinal);
                if (pos >= 0) path = cmd.Substring(0, pos + 4);
            }
            if (string.IsNullOrEmpty(path)) return null;
            try { return Environment.ExpandEnvironmentVariables(path.Trim()); }
            catch { return null; }
        }

        // Обработчик может быть системным загрузчиком UWP (LaunchWinApp) или
        // explorer — это не клиент, его нельзя выделить в списке.
        static bool IsSystemLauncher(string exe)
        {
            try
            {
                string name = Path.GetFileName(exe).ToLowerInvariant();
                return name == "explorer.exe" || name == "launchwinapp.exe" ||
                       name == "rundll32.exe" || name == "cmd.exe" || name == "powershell.exe";
            }
            catch { return false; }
        }

        static void AddTgClient(List<TgProxyClient> list, string path, string def)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
                if (!File.Exists(path)) return;
                string exeName = Path.GetFileName(path).ToLowerInvariant();
                if (!exeName.EndsWith(".exe", StringComparison.Ordinal)) return;
                if (exeName.IndexOf("unins") >= 0 || exeName.IndexOf("uninstall") >= 0 ||
                    exeName.IndexOf("updat") >= 0 || exeName.IndexOf("crash") >= 0) return;
                string full = Path.GetFullPath(path);
                foreach (TgProxyClient c in list)
                    if (string.Equals(c.Path, full, StringComparison.OrdinalIgnoreCase)) return;

                bool isDefault = false;
                if (!string.IsNullOrEmpty(def))
                {
                    try { isDefault = string.Equals(Path.GetFullPath(def), full, StringComparison.OrdinalIgnoreCase); }
                    catch { }
                }
                list.Add(new TgProxyClient { Name = TgClientName(full), Path = full, IsDefault = isDefault });
            }
            catch { }
        }

        static string TgClientName(string path)
        {
            try
            {
                FileVersionInfo vi = FileVersionInfo.GetVersionInfo(path);
                string d = (vi.FileDescription ?? "").Trim();
                if (d.Length > 0 && d.Length <= 48) return d;
            }
            catch { }
            try { return Path.GetFileNameWithoutExtension(path); }
            catch { return "Telegram"; }
        }

        // Открыть ссылку в клиенте по умолчанию (через системный обработчик tg://).
        public static bool TgOpenLinkDefault(string link, out string error)
        {
            error = null;
            try
            {
                Process.Start(new ProcessStartInfo { FileName = link, UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                error = Short(ex.Message);
                return false;
            }
        }

        // Открыть ссылку в конкретном exe. Desktop-клиенты и форки принимают
        // tg:// аргументом и передают его уже запущенному экземпляру.
        public static bool TgOpenInClient(string exe, string link, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    error = Loc.T("ov.tg.clientMissing");
                    return false;
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "\"" + link + "\"",
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                error = Short(ex.Message);
                return false;
            }
        }

        // Автозапуск TG-Proxy вместе с Lantern при открытии приложения
        public static bool TgAutostartAppEnabled
        {
            get { return GetBool("tg_autostart_app", false); }
            set { SetBool("tg_autostart_app", value); SaveConfig(); }
        }
    }
}
