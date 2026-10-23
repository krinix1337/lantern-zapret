using System;
using System.Collections.Generic;
using System.IO;

namespace ZapretStudio
{
    // Событие журнала
    public class LogEvent
    {
        public DateTime Time;
        public Sev Level;   // Info/Ok/Warn/Err
        public string Text;
    }

    static partial class Core
    {
        static Dictionary<string,string> _cfg = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        static readonly object _cfgLock = new object();
        static readonly object _cfgWriteLock = new object();
        static volatile bool _cfgLoaded;

        // Мёртвые ключи удалённых тем. SaveConfig сериализует весь словарь, поэтому
        // без явной чистки они лежали бы в gui-config.ini вечно.
        static readonly string[] StaleKeys = { "peter_backdrop", "peter_volume", "midnight_flavor" };

        public static void LoadConfig()
        {
            var d = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(ConfigFile))
                    foreach (var l in File.ReadAllLines(ConfigFile))
                    {
                        string s = l.Trim();
                        if (s.Length == 0 || s.StartsWith("#") || s.StartsWith("[")) continue;
                        int eq = s.IndexOf('=');
                        if (eq <= 0) continue;
                        d[s.Substring(0, eq).Trim()] = s.Substring(eq + 1).Trim();
                    }
            }
            catch (Exception ex) { Fail("LoadConfig: " + ex.Message); }
            foreach (var k in StaleKeys) d.Remove(k);
            lock (_cfgLock) { _cfg = d; _cfgLoaded = true; }
        }

        public static string Get(string key, string dflt)
        {
            if (!_cfgLoaded) LoadConfig();
            lock (_cfgLock)
            {
                string v; return _cfg.TryGetValue(key, out v) ? v : dflt;
            }
        }
        public static bool GetBool(string key, bool dflt)
        {
            string v = Get(key, null);
            if (v == null) return dflt;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        public static void Set(string key, string val)
        {
            if (!_cfgLoaded) LoadConfig();
            lock (_cfgLock) { _cfg[key] = val; }
        }
        public static void SetBool(string key, bool val) { Set(key, val ? "1" : "0"); }

        // Убрать ключ совсем: Set(key, null) оставил бы в файле строку «key = »,
        // а это уже не «ключа нет», а пустое значение.
        public static void Unset(string key)
        {
            if (!_cfgLoaded) LoadConfig();
            lock (_cfgLock) { _cfg.Remove(key); }
        }

        public static int GetInt(string key, int dflt)
        {
            string v = Get(key, null);
            int n;
            if (v != null && int.TryParse(v, out n)) return n;
            return dflt;
        }
        public static void SetInt(string key, int val) { Set(key, val.ToString()); }

        public static void SaveConfig()
        {
            string text, target;
            lock (_cfgLock)
            {
                if (_cfg == null || !_cfgLoaded) return;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# ZapretStudio GUI config");
                foreach (var kv in _cfg) sb.AppendLine(kv.Key + " = " + kv.Value);
                text = sb.ToString();
                target = ConfigFile;
            }
            // Диск вне _cfgLock, иначе GetBool("reduce_motion") на пути
            // отрисовки вставал в очередь за медленным или сетевым диском.
            // Отдельный лок нужен, чтобы два потока не писали один и тот же .tmp.
            lock (_cfgWriteLock)
            {
                try { WriteFileAtomic(target, text); }
                catch (Exception ex) { Fail("SaveConfig: " + ex.Message); }
            }
        }

        // File.WriteAllText уссекает файл до нуля ещё до записи, поэтому обрыв
        // питания или Kill оставлял пустой gui-config.ini, а вместе с ним терялись
        // root, lang, theme, last_strategy и ipset_enabled. Пишем временный файл рядом
        // и переносим его атомарно, сохраняя предыдущую версию в .bak.
        public static void WriteFileAtomic(string path, string text)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            if (!File.Exists(path)) { File.Move(tmp, path); return; }
            try
            {
                File.Replace(tmp, path, path + ".bak", false);
            }
            catch
            {
                // File.Replace недоступен на FAT, в сетевых папках и при занятом .bak.
                // Откат обязателен: на любом шаге одна из копий остаётся целой.
                string bak = path + ".bak";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(path, bak);
                try { File.Move(tmp, path); }
                catch { File.Move(bak, path); throw; }
            }
        }

        // ---- Журнал событий (в памяти) ----
        public static readonly List<LogEvent> Log = new List<LogEvent>();
        public static event Action<LogEvent> OnLog;

        public static void Say(Sev level, string text)
        {
            var e = new LogEvent { Time = DateTime.Now, Level = level, Text = text };
            lock (Log) { Log.Add(e); if (Log.Count > 5000) Log.RemoveAt(0); }
            var h = OnLog; if (h != null) h(e);
        }
        public static void Info(string t) { Say(Sev.Info, t); }
        public static void Good(string t) { Say(Sev.Ok, t); }
        public static void Warn(string t) { Say(Sev.Warn, t); }
        public static void Fail(string t) { Say(Sev.Err, t); }

        // Диагностический дамп с маскированием
        public static string Diagnostics()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format(Loc.T("diag.head"), AppName));
            sb.AppendLine(string.Format(Loc.T("diag.guiVer"), AppVersion));
            sb.AppendLine(string.Format(Loc.T("diag.zapret"), ZapretVersion()));
            sb.AppendLine(string.Format(Loc.T("diag.folder"), Mask(Root ?? Loc.T("diag.none"))));
            sb.AppendLine(string.Format(Loc.T("diag.admin"), IsAdmin() ? Loc.T("diag.yes") : Loc.T("diag.no")));
            sb.AppendLine();
            foreach (var d in RunDiagnostics())
                sb.AppendLine("[" + d.Sev + "] " + d.Name + ": " + Mask(d.Value));
            sb.AppendLine();
            sb.AppendLine(Loc.T("diag.events"));
            lock (Log)
                for (int i = Math.Max(0, Log.Count - 40); i < Log.Count; i++)
                    sb.AppendLine(Log[i].Time.ToString("HH:mm:ss") + " [" + Log[i].Level + "] " + Mask(Log[i].Text));

            // Хвост вывода самого winws — ключ к диагностике «почему не работает».
            string winwsOut = WinwsLogTail(60);
            if (!string.IsNullOrEmpty(winwsOut))
            {
                sb.AppendLine();
                sb.AppendLine(Loc.T("diag.winwsOutput"));
                sb.AppendLine(winwsOut);
            }
            return sb.ToString();
        }
    }
}
