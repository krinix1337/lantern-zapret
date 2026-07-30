using System;
using System.IO;
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
        public bool Done;
        public bool Failed;
        public string Error;
        public string Phase;       // "download" | "extract" | "done"
    }

    static partial class Core
    {
        // Резервная ссылка на архив ветки. Обычно используется архив последнего
        // релиза: он содержит готовые Windows-компоненты и не зависит от имени версии.
        public const string ZapretZipUrl = "https://github.com/Flowseal/zapret-discord-youtube/archive/refs/heads/main.zip";
        public const string ZapretReleaseApi = "https://api.github.com/repos/Flowseal/zapret-discord-youtube/releases/latest";

        // Адрес ZIP-ассета последнего релиза. GitHub меняет имя архива вместе с
        // версией, поэтому берём точную ссылку из API. Если API временно недоступен,
        // остаётся рабочий резервный вариант с main.zip.
        public static string ZapretDownloadUrl()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Lantern");
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
            var pr = new DlProgress { Total = -1, Phase = "download" };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string tempPath = destPath + ".part";
            try
            {
                string dir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (File.Exists(tempPath)) File.Delete(tempPath);
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "ZapretStudio";
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
                                if (onProgress != null) onProgress(pr);
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
                if (onProgress != null) onProgress(pr);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                pr.Failed = true; pr.Error = Short(ex.Message);
                if (onProgress != null) onProgress(pr);
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
            try
            {
                string tmp = destDir + "_unz_tmp";
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                Directory.CreateDirectory(tmp);
                if (onStage != null) onStage("extract");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tmp);

                // Определяем корень: если одна папка верхнего уровня с bin внутри — используем её.
                string src = tmp;
                var dirs = Directory.GetDirectories(tmp);
                var files = Directory.GetFiles(tmp);
                if (files.Length == 0 && dirs.Length == 1) src = dirs[0];

                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                if (onStage != null) onStage("replace");
                CopyDir(src, destDir);
                try { Directory.Delete(tmp, true); } catch { }
                return true;
            }
            catch (Exception ex) { error = Short(ex.Message); return false; }
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), true);
            foreach (var d in Directory.GetDirectories(src))
                CopyDir(d, Path.Combine(dst, Path.GetFileName(d)));
        }

        // Пометить корень в локальном gui-config рядом с exe, чтобы найти после перезапуска.
        public static void RememberRoot(string path)
        {
            try
            {
                var cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gui-config.ini");
                File.WriteAllText(cfgPath, "root=" + path + Environment.NewLine);
            }
            catch { }
        }
    }
}
