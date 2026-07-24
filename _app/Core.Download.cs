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
        // Ссылки на релизы zapret (архив исходного проекта).
        public const string ZapretZipUrl = "https://github.com/Flowseal/zapret-discord-youtube/archive/refs/heads/main.zip";

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
            try
            {
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
                    using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[81920];
                        long total = 0;
                        int read;
                        long lastTick = 0;
                        while ((read = rs.Read(buf, 0, buf.Length)) > 0)
                        {
                            if (isCancelled != null && isCancelled())
                            {
                                try { fs.Close(); File.Delete(destPath); } catch { }
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
                pr.Elapsed = sw.Elapsed;
                if (onProgress != null) onProgress(pr);
                return true;
            }
            catch (Exception ex)
            {
                pr.Failed = true; pr.Error = Short(ex.Message);
                if (onProgress != null) onProgress(pr);
                return false;
            }
        }

        // Распаковать zip zapret в целевую папку. Архив с GitHub содержит один
        // верхний каталог (zapret-discord-youtube-main) — «поднимаем» его содержимое.
        public static bool ExtractZapretZip(string zipPath, string destDir, out string error)
        {
            error = null;
            try
            {
                string tmp = destDir + "_unz_tmp";
                if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
                Directory.CreateDirectory(tmp);
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, tmp);

                // Определяем корень: если одна папка верхнего уровня с bin внутри — используем её.
                string src = tmp;
                var dirs = Directory.GetDirectories(tmp);
                var files = Directory.GetFiles(tmp);
                if (files.Length == 0 && dirs.Length == 1) src = dirs[0];

                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
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
