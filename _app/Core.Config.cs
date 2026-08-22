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
        static volatile bool _cfgLoaded;

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
            lock (_cfgLock)
            {
                if (_cfg == null || !_cfgLoaded) return;
                try
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("# ZapretStudio GUI config");
                    foreach (var kv in _cfg) sb.AppendLine(kv.Key + " = " + kv.Value);
                    string dir = Path.GetDirectoryName(ConfigFile);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(ConfigFile, sb.ToString());
                }
                catch (Exception ex) { Fail("SaveConfig: " + ex.Message); }
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
            return sb.ToString();
        }
    }
}
