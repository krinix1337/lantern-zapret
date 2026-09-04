using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace ZapretStudio
{
    // Прогресс загрузки, передаётся в UI.
    public class DlProgress
    {
        public long BytesRead;
        public long Total;         // -1 если неизвестно
        public double SpeedBps;    // байт/с
        public TimeSpan Elapsed;
        public bool Failed;
        public string Error;
    }

    static partial class Core
    {
        // Резервная ссылка на архив ветки. Обычно используется архив последнего
        // релиза: он содержит готовые Windows-компоненты и не зависит от имени версии.
        public static string ZapretZipUrl { get { return Endpoints.ZapretZipUrl; } }
        public static string ZapretReleaseApi { get { return Endpoints.ZapretReleaseApi; } }

        // Адрес ZIP-ассета последнего релиза. GitHub меняет имя архива вместе с
        // версией, поэтому берём точную ссылку из API. Если API временно недоступен,
        // остаётся рабочий резервный вариант с main.zip.
        public static string ZapretDownloadUrl(string version)
        {
            string normalized = string.IsNullOrEmpty(version) ? null : version.Trim().TrimStart('v', 'V');
            if (string.IsNullOrEmpty(normalized))
            {
                string detected = CheckLatestVersion();
                if (!string.IsNullOrEmpty(detected)) normalized = detected.Trim().TrimStart('v', 'V');
            }
            if (!string.IsNullOrEmpty(normalized))
                return Endpoints.ZapretRepo + "/releases/download/" + normalized
                    + "/zapret-discord-youtube-" + normalized + ".zip";
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    string json = wc.DownloadString(ZapretReleaseApi);
                    var matches = System.Text.RegularExpressions.Regex.Matches(
                        json, "\\\"browser_download_url\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        string url = m.Groups[1].Value;
                        if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return url;
                    }
                }
            }
            catch { }
            return ZapretZipUrl;
        }

        public static string ZapretDownloadUrl()
        {
            return ZapretDownloadUrl(null);
        }

        // Человекочитаемый размер.
        public static string HumanSize(long bytes)
        {
            if (bytes < 0) return "?";
            double b = bytes;
            string[] u = { Loc.T("unit.b"), Loc.T("unit.kb"), Loc.T("unit.mb"), Loc.T("unit.gb") };
            int i = 0;
            while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
            return (i == 0 ? b.ToString("0") : b.ToString("0.0")) + " " + u[i];
        }
        public static string HumanSpeed(double bps)
        {
            return HumanSize((long)bps) + Loc.T("unit.perSec");
        }

        // Синхронная загрузка файла с колбэком прогресса. Вызывать из фонового потока.
        // Возвращает true при успехе.
        public static bool DownloadFile(string url, string destPath, Action<DlProgress> onProgress, Func<bool> isCancelled)
        {
            var pr = new DlProgress { Total = -1 };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string tempPath = destPath + ".part";
            try
            {
                string dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(tempPath)) File.Delete(tempPath);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
                req.AllowAutoRedirect = true;
                req.Timeout = 30000;
                req.ReadWriteTimeout = 60000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    pr.Total = resp.ContentLength;
                    using (var rs = resp.GetResponseStream())
                    using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[81920];
                        long total = 0;
                        int read;
                        long lastTick = 0;
                        while ((read = rs.Read(buf, 0, buf.Length)) > 0)
                        {
                            if (isCancelled != null && isCancelled())
                            {
                                try { fs.Close(); File.Delete(tempPath); } catch { }
                                return false;
                            }
                            fs.Write(buf, 0, read);
                            total += read;
                            long ms = sw.ElapsedMilliseconds;
                            if (ms - lastTick >= 100 || (pr.Total > 0 && total >= pr.Total))
                            {
                                lastTick = ms;
                                pr.BytesRead = total;
                                pr.Elapsed = sw.Elapsed;
                                pr.SpeedBps = ms > 0 ? total / (ms / 1000.0) : 0;
                                // Колбэк рисует UI через Dispatcher: исключение из него
                                // (например, при закрытии окна) не должно убивать загрузку
                                // и поток пула.
                                try { if (onProgress != null) onProgress(pr); } catch { }
                            }
                        }
                        pr.BytesRead = total;
                    }
                }
                // Не портим рабочий файл при обрыве сети: новый файл попадает на
                // место назначения только после полной успешной загрузки.
                string backup = destPath + ".bak";
                if (File.Exists(backup)) File.Delete(backup);
                if (File.Exists(destPath)) File.Move(destPath, backup);
                try
                {
                    File.Move(tempPath, destPath);
                    if (File.Exists(backup)) File.Delete(backup);
                }
                catch
                {
                    if (!File.Exists(destPath) && File.Exists(backup)) File.Move(backup, destPath);
                    throw;
                }
                pr.Elapsed = sw.Elapsed;
                try { if (onProgress != null) onProgress(pr); } catch { }
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                pr.Failed = true; pr.Error = Short(ex.Message);
                try { if (onProgress != null) onProgress(pr); } catch { }
                return false;
            }
        }

        // Распаковать zip zapret в целевую папку. Архив с GitHub содержит один
        // верхний каталог (zapret-discord-youtube-main) — «поднимаем» его содержимое.
        public static bool ExtractZapretZip(string zipPath, string destDir, out string error)
        {
            return ExtractZapretZip(zipPath, destDir, out error, null);
        }

        // Этапы передаются отдельно от прогресса загрузки: распаковка и замена
        // файлов выполняются локально и не имеют достоверного процента байтов.
        public static bool ExtractZapretZip(string zipPath, string destDir, out string error, Action<string> onStage)
        {
            error = null;
            string tmp = Path.Combine(Path.GetTempPath(), "zapret_unz_" + Guid.NewGuid().ToString("N"));
            try
            {
                DeleteDirSafe(tmp);
                Directory.CreateDirectory(tmp);
                if (onStage != null) onStage("extract");
                using (var archive = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (SkipGitEntry(entry.FullName)) continue;
                        string cleanName = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                        string destPath = Path.Combine(tmp, cleanName);
                        string fullDestPath = Path.GetFullPath(destPath);
                        string fullTmp = Path.GetFullPath(tmp);
                        if (!fullDestPath.StartsWith(fullTmp.EndsWith(Path.DirectorySeparatorChar.ToString()) ? fullTmp : fullTmp + Path.DirectorySeparatorChar))
                            continue;

                        if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\") || string.IsNullOrEmpty(entry.Name))
                        {
                            if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
                        }
                        else
                        {
                            string dir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                            using (var srcStream = entry.Open())
                            using (var dstStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                srcStream.CopyTo(dstStream);
                            }
                        }
                    }
                }

                // Определяем корень: если одна папка верхнего уровня с bin внутри — используем её.
                string src = tmp;
                var dirs = Directory.GetDirectories(tmp);
                var files = Directory.GetFiles(tmp);
                if (files.Length == 0 && dirs.Length == 1) src = dirs[0];

                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                if (onStage != null) onStage("replace");
                var copyErrors = new List<string>();
                CopyDir(src, destDir, ref copyErrors);
                DeleteDirSafe(tmp);
                if (copyErrors.Count > 0)
                {
                    error = "Locked files: " + string.Join("; ", copyErrors.Take(3).ToArray());
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                DeleteDirSafe(tmp);
                error = Short(ex.Message);
                return false;
            }
        }

        public static void DeleteDirSafe(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                var di = new DirectoryInfo(dir);
                foreach (var info in di.GetFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    try { info.Attributes = FileAttributes.Normal; } catch { }
                }
                di.Attributes = FileAttributes.Normal;
                Directory.Delete(dir, true);
            }
            catch
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        static bool CopyDir(string src, string dst, ref List<string> errors, int depth = 0)
        {
            if (depth > 32 || !Directory.Exists(src)) return true;
            if (!Directory.Exists(dst)) Directory.CreateDirectory(dst);
            bool ok = true;
            foreach (var f in Directory.GetFiles(src))
            {
                string targetFile = Path.Combine(dst, Path.GetFileName(f));
                try
                {
                    if (File.Exists(targetFile))
                    {
                        try { File.SetAttributes(targetFile, FileAttributes.Normal); } catch { }
                    }
                    File.Copy(f, targetFile, true);
                }
                catch (Exception ex)
                {
                    ok = false;
                    if (errors != null) errors.Add(Path.GetFileName(f) + ": " + ex.Message);
                }
            }
            foreach (var d in Directory.GetDirectories(src))
            {
                string dirName = Path.GetFileName(d);
                if (dirName.Equals(".github", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".service", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!CopyDir(d, Path.Combine(dst, dirName), ref errors, depth + 1)) ok = false;
            }
            return ok;
        }

        // Пометить корень в локальном gui-config рядом с exe, сохраняя остальные настройки.
        //
        // Писать через SaveConfig() нельзя: после SetRoot() ConfigFile указывает на
        // <Root>\utils\gui-config.ini, а LocateRoot() при старте читает файл рядом с
        // exe (Root ещё не известен). Из-за этого путь сохранялся туда, где его
        // никто не искал, и «выбранная папка» не помнилась между запусками.
        // Поэтому строку root пишем в локальный файл напрямую, независимо от
        // текущего ConfigFile и от порядка вызовов SetRoot/RememberRoot.
        public static void RememberRoot(string path)
        {
            try
            {
                Set("root", path);
                SaveConfig();
            }
            catch { }
            try
            {
                string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui-config.ini");
                var lines = new List<string>();
                if (File.Exists(local))
                {
                    foreach (var raw in File.ReadAllLines(local))
                    {
                        string s = raw.Trim();
                        int eq = s.IndexOf('=');
                        if (eq > 0 && s.Substring(0, eq).Trim().Equals("root", StringComparison.OrdinalIgnoreCase))
                            continue;   // старое значение заменяем
                        lines.Add(raw);
                    }
                }
                else lines.Add("# ZapretStudio GUI config");
                lines.Add("root = " + path);
                File.WriteAllText(local, string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine);
            }
            catch (Exception ex) { Warn("RememberRoot: " + ex.Message); }
        }

        static bool SkipGitEntry(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals(".github", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith(".git", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals(".service", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
