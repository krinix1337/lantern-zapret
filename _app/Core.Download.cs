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
        public static string ZapretReleaseApi { get { return Endpoints.ZapretReleaseApi; } }

        // Адрес ZIP-ассета последнего релиза и его SHA-256 из поля digest GitHub API.
        // GitHub меняет имя архива вместе с версией, поэтому берём точную ссылку
        // из API; digest публикуется там же, отдельного файла сумм в релизах нет.
        // №33: раньше архив скачивался и распаковывался без всякой сверки, а
        // SHA256SUMS.txt внутри него искать бесполезно — апстрим его не публикует.
        public static bool ZapretZipAssets(out string url, out string sha256)
        {
            url = null; sha256 = null;
            string json = FetchReleaseJson(ZapretReleaseApi);
            if (string.IsNullOrEmpty(json)) return false;
            foreach (var a in Endpoints.ReleaseAssets(json))
            {
                if (!a.Key.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                url = a.Value;
                sha256 = Endpoints.ReleaseAssetDigest(json, a.Key);
                return true;
            }
            return false;
        }

        // Скачать файл и сверить с ожидаемой суммой. Ошибка загрузки остаётся в
        // журнале и в прерванном прогрессе (error == null) — у вызывающего кода
        // уже есть свои тексты на этот случай.
        public static bool DownloadVerified(string url, string expectedSha256, string destPath,
            Action<DlProgress> onProgress, Func<bool> isCancelled, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(expectedSha256)) { error = Loc.T("dl.hashMissing"); Warn(error); return false; }
            if (!DownloadFile(url, destPath, onProgress, isCancelled)) return false;
            if (!VerifySha256(destPath, expectedSha256, out error))
            {
                try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
                return false;
            }
            return true;
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
                        // №33: цикл завершается по read == 0, поэтому оборванное
                        // соединение выглядело как успешная загрузка укороченного
                        // файла. Content-Length известен не всегда (chunked) — тогда
                        // сверять не с чем.
                        if (pr.Total > 0 && total != pr.Total)
                            throw new Exception(string.Format(Loc.T("dl.sizeMismatch"),
                                HumanSize(total), HumanSize(pr.Total)));
                    }
                }
                // Не портим рабочий файл при обрыве сети: новый файл попадает на
                // место назначения только после полной успешной загрузки.
                ReplaceWithBackup(tempPath, destPath);
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
                        string baseDir = fullTmp.EndsWith(Path.DirectorySeparatorChar.ToString()) ? fullTmp : fullTmp + Path.DirectorySeparatorChar;
                        if (!fullDestPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
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

                // №8: пред-проверка блокировок до первой записи. Раньше каталог
                // подменялся пофайлово, и занятым winws.exe/WinDivert64.sys обновление
                // обрывалось на середине — установка оставалась собранной из двух версий.
                //
                // Проверка повторяется: даже после KillWinws() файл остаётся занятым
                // ещё долю секунды (отсоединение секции образа, скан Defender, служба в
                // STOP_PENDING). Одиночная проверка отменяла обновление с «выключите
                // обход» ровно тогда, когда приложение обход уже остановило само.
                var targets = CollectTargets(src, destDir);
                var locked = new List<string>();
                for (int attempt = 0; attempt < LockCheckTries; attempt++)
                {
                    locked = LockedNames(targets);
                    if (locked.Count == 0) break;
                    if (attempt + 1 < LockCheckTries) System.Threading.Thread.Sleep(LockCheckDelayMs);
                }
                if (locked.Count > 0)
                {
                    DeleteDirSafe(tmp);
                    error = string.Format(Loc.T("dl.lockedFiles"), string.Join(", ", locked.Take(6).ToArray()));
                    return false;
                }

                if (onStage != null) onStage("replace");
                var copyErrors = new List<string>();
                CopyStaged(targets, copyErrors);
                DeleteDirSafe(tmp);
                if (copyErrors.Count > 0)
                {
                    error = WritePartialReport(destDir, copyErrors);
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

        // Заменить файл с откатом: старая копия уходит в .bak и возвращается на
        // место, если перенос не удался.
        // №40: раньше удаление .bak стояло в одном try с переносом. Если старый
        // файл держал антивирус, catch не откатывался (новый файл уже на месте) и
        // пробрасывал исключение — «Загрузка не удалась» при успешно обновлённом
        // файле, а кнопка «Обновить» оставалась висеть.
        public static void ReplaceWithBackup(string src, string dest)
        {
            string backup = dest + ".bak";
            try { if (File.Exists(backup)) File.Delete(backup); } catch { }
            if (File.Exists(dest)) File.Move(dest, backup);
            try { File.Move(src, dest); }
            catch
            {
                if (!File.Exists(dest) && File.Exists(backup)) File.Move(backup, dest);
                throw;
            }
            try { if (File.Exists(backup)) File.Delete(backup); }
            catch (Exception bex) { Info(string.Format(Loc.T("dl.bakKept"), backup, Short(bex.Message))); }
        }

        // Файл занят (winws держит драйвер, exe открыт антивирусом) — File.Copy
        // на нём упадёт, поэтому проверять нужно до начала замены.
        static bool Locked(string path)
        {
            if (!File.Exists(path)) return false;
            try { using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { } return false; }
            catch { return true; }
        }

        // Сколько раз и с каким шагом перепроверять занятость файлов перед заменой.
        const int LockCheckTries = 7;
        const int LockCheckDelayMs = 400;

        static List<string> LockedNames(List<string[]> targets)
        {
            var locked = new List<string>();
            foreach (var t in targets) if (Locked(t[1])) locked.Add(Path.GetFileName(t[1]));
            return locked;
        }

        // Пары «источник → цель» в порядке копирования. service.bat идёт последним:
        // Core.ZapretVersion() читает версию из него, и при прежнем алфавитном порядке
        // он обновлялся раньше бинарников из bin\ — номер версии обгонял содержимое.
        static List<string[]> CollectTargets(string src, string dst)
        {
            var list = new List<string[]>();
            var tail = new List<string[]>();
            CollectTargetsRec(src, dst, list, tail, 0);
            list.AddRange(tail);
            return list;
        }

        static void CollectTargetsRec(string src, string dst, List<string[]> list, List<string[]> tail, int depth)
        {
            if (depth > 32 || !Directory.Exists(src)) return;
            foreach (var f in Directory.GetFiles(src))
            {
                var pair = new string[] { f, Path.Combine(dst, Path.GetFileName(f)) };
                if (depth == 0 && Path.GetFileName(f).Equals("service.bat", StringComparison.OrdinalIgnoreCase)) tail.Add(pair);
                else list.Add(pair);
            }
            foreach (var d in Directory.GetDirectories(src))
            {
                string dirName = Path.GetFileName(d);
                if (dirName.Equals(".github", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals(".service", StringComparison.OrdinalIgnoreCase))
                    continue;
                CollectTargetsRec(d, Path.Combine(dst, dirName), list, tail, depth + 1);
            }
        }

        static void CopyStaged(List<string[]> targets, List<string> errors)
        {
            foreach (var p in targets)
            {
                try
                {
                    string dir = Path.GetDirectoryName(p[1]);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    if (File.Exists(p[1]))
                    {
                        try { File.SetAttributes(p[1], FileAttributes.Normal); } catch { }
                    }
                    // Повтор на занятом файле: пред-проверка и реальная запись — это
                    // разные моменты, между ними winws может подняться службой снова.
                    // Один промах не должен превращать обновление в «применено частично».
                    Exception last = null;
                    for (int i = 0; i < 4; i++)
                    {
                        try { File.Copy(p[0], p[1], true); last = null; break; }
                        catch (Exception exCopy)
                        {
                            last = exCopy;
                            if (i < 3) System.Threading.Thread.Sleep(250);
                        }
                    }
                    if (last != null) throw last;
                }
                catch (Exception ex)
                {
                    if (errors != null) errors.Add(Path.GetFileName(p[0]) + ": " + ex.Message);
                }
            }
        }

        // №8: список незаменённых файлов сохраняется рядом с каталогом и попадает в
        // журнал — иначе «Центр обновлений» показывал только «Не удалось распаковать»,
        // а установка молча оставалась гибридной.
        static string WritePartialReport(string destDir, List<string> errors)
        {
            string path = Path.Combine(destDir, "zapret_update.partial.txt");
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Lantern: update applied partially / обновление применено частично");
                sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                foreach (var e in errors) sb.AppendLine(e);
                WriteFileAtomic(path, sb.ToString());
            }
            catch (Exception ex) { path = null; Warn("partial report: " + ex.Message); }
            string msg = string.Format(Loc.T("dl.partialReport"),
                string.Join("; ", errors.Take(3).ToArray()), path ?? "-");
            Warn(msg);
            return msg;
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
            // №50: оба catch глотали ошибку одинаково, и каталоги zapret_unz_<guid>
            // оставались в %TEMP% навсегда без единого следа в журнале.
            if (Directory.Exists(dir)) Warn(string.Format(Loc.T("dl.tmpLeft"), dir));
        }

        // Свободное место на диске, где лежит каталог; -1, если узнать не удалось.
        public static long FreeSpaceOf(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return -1;
                string root = Path.GetPathRoot(Path.GetFullPath(path));
                if (string.IsNullOrEmpty(root)) return -1;
                var di = new DriveInfo(root);
                return di.IsReady ? di.AvailableFreeSpace : -1;
            }
            catch { return -1; }
        }

        // Пометить корень в локальном gui-config рядом с exe, сохраняя остальные настройки.
        //
        // Писать через SaveConfig() нельзя: после SetRoot() ConfigFile указывает на
        // <Root>\utils\gui-config.ini, а LocateRoot() при старте читает файл рядом с
        // exe (Root ещё не известен). Из-за этого путь сохранялся туда, где его
        // никто не искал, и «выбранная папка» не помнилась между запусками.
        // Поэтому строку root пишем в локальный файл напрямую, независимо от
        // текущего ConfigFile и от порядка вызовов SetRoot/RememberRoot.
        // №41: словарь конфигурации при этом не трогаем — прежние Set("root", …) +
        // SaveConfig() записывали содержимое файла рядом с exe в <Root>\utils\gui-config.ini.
        public static void RememberRoot(string path)
        {
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
                WriteFileAtomic(local, string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine);
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
