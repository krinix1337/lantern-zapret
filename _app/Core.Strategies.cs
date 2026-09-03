using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ZapretStudio
{
    // Стратегии, игровой фильтр, IPSet-фильтр, пользовательские списки.
    static partial class Core
    {
        public static List<string> GetStrategyFiles()
        {
            try
            {
                return Directory.GetFiles(Root, "general*.bat")
                    .Select(Path.GetFileName)
                    .OrderBy(NaturalKey, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        static readonly Regex _naturalKeyRegex = new Regex("[0-9]+", RegexOptions.Compiled);
        // public: покрывается юнит-тестом SelfTest.
        public static string NaturalKey(string s)
        { return _naturalKeyRegex.Replace(s, m => m.Value.PadLeft(8, '0')); }

        // Категория по имени файла (General / ALT / FAKE / SIMPLE / Другая)
        public static string CategoryOf(string bat)
        {
            string n = bat.ToUpperInvariant();
            if (n.Contains("FAKE")) return "FAKE";
            if (n.Contains("SIMPLE")) return "SIMPLE";
            if (n.Contains("ALT")) return "ALT";
            if (n == "GENERAL.BAT") return "General";
            return Loc.T("cat.other");
        }

        // Нейтральное описание: НЕ выдумываем назначение по имени.
        public static string DescriptionOf(string bat)
        {
            string cat = CategoryOf(bat);
            switch (cat)
            {
                case "General": return Loc.T("desc.general");
                case "ALT":     return Loc.T("desc.alt");
                case "FAKE":    return Loc.T("desc.fake");
                case "SIMPLE":  return Loc.T("desc.simple");
                default:        return Loc.T("desc.custom");
            }
        }

        public static string PrettyName(string bat)
        {
            string n = bat;
            if (n.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
            return n;
        }

        // ---------- Game filter ----------
        public static string GameMode
        {
            get
            {
                try
                {
                    if (!File.Exists(GameFlag)) return "off";
                    string v = File.ReadAllText(GameFlag).Trim().ToLowerInvariant();
                    if (v == "all" || v == "tcp" || v == "udp") return v;
                    return "udp";
                }
                catch { return "off"; }
            }
            set
            {
                try
                {
                    if (value == "off") { if (File.Exists(GameFlag)) File.Delete(GameFlag); }
                    else File.WriteAllText(GameFlag, value);
                }
                catch { }
            }
        }

        public static string GameModeLabel()
        {
            switch (GameMode)
            {
                case "all": return Loc.T("game.on.all");
                case "tcp": return Loc.T("game.on.tcp");
                case "udp": return Loc.T("game.on.udp");
                default: return Loc.T("game.off");
            }
        }

        static void GameValues(out string tcp, out string udp)
        {
            switch (GameMode)
            {
                case "all": tcp = "1024-65535"; udp = "1024-65535"; break;
                case "tcp": tcp = "1024-65535"; udp = "12";         break;
                case "udp": tcp = "12";         udp = "1024-65535"; break;
                default:    tcp = "12";         udp = "12";         break;
            }
        }

        // ---------- IPSet filter (loaded / none / any) ----------
        const string IpsetSentinel = "203.0.113.113/32";

        public static string IpsetStatus()
        {
            try
            {
                if (!IpsetEnabled) return "none";
                if (!File.Exists(IpsetFile)) return "any";
                var lines = File.ReadAllLines(IpsetFile).Where(l => l.Trim().Length > 0).ToList();
                if (lines.Count == 0) return "any";
                if (lines.Any(l => l.Contains(IpsetSentinel))) return "none";
                return "loaded";
            }
            catch { return "any"; }
        }

        // Те же три состояния, что в service.bat: загруженный список, пустой
        // список (any) и служебная запись-заглушка (none). При переходе в none
        // оригинальный список сохраняется рядом, чтобы его можно было вернуть.
        public static void SetIpsetMode(string mode)
        {
            try
            {
                string backup = IpsetFile + ".backup";
                string current = IpsetStatus();
                if (mode == "loaded")
                {
                    if (File.Exists(backup))
                    {
                        if (File.Exists(IpsetFile)) File.Delete(IpsetFile);
                        File.Move(backup, IpsetFile);
                    }
                }
                else if (mode == "none")
                {
                    if (current == "loaded" && File.Exists(IpsetFile))
                    {
                        if (File.Exists(backup)) File.Delete(backup);
                        File.Move(IpsetFile, backup);
                    }
                    File.WriteAllText(IpsetFile, IpsetSentinel + Environment.NewLine);
                }
                else if (mode == "any")
                {
                    File.WriteAllText(IpsetFile, "");
                }
                IpsetEnabled = true;
            }
            catch (Exception ex) { Warn("SetIpsetMode: " + ex.Message); }
        }

        // IPSet — отдельная пользовательская настройка. Наличие файла не означает,
        // что пользователь хочет добавлять его к аргументам winws.
        public static bool IpsetEnabled
        {
            get { return GetBool("ipset_enabled", true); }
            set { SetBool("ipset_enabled", value); SaveConfig(); }
        }

        public static int IpsetCount()
        {
            try
            {
                if (!File.Exists(IpsetFile)) return 0;
                return File.ReadAllLines(IpsetFile).Count(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("#"));
            }
            catch { return 0; }
        }

        public static string IpsetStatusLabel()
        {
            switch (IpsetStatus())
            {
                case "loaded": return string.Format(Loc.T("ipset.loaded"), IpsetCount());
                case "none": return Loc.T("ipset.none");
                default: return Loc.T("ipset.empty");
            }
        }

        // ---------- Пользовательские списки ----------
        public static void EnsureUserLists()
        {
            try
            {
                string p;
                p = Path.Combine(Lists, "ipset-exclude-user.txt");
                if (!File.Exists(p)) File.WriteAllText(p, IpsetSentinel + "\r\n");
                p = Path.Combine(Lists, "list-general-user.txt");
                if (!File.Exists(p)) File.WriteAllText(p, "# Never leave this file empty\r\ndomain.example.abc\r\n");
                p = Path.Combine(Lists, "list-exclude-user.txt");
                if (!File.Exists(p)) File.WriteAllText(p, "domain.example.abc\r\n");
            }
            catch { }
        }

        // ---------- Сборка аргументов winws из .bat ----------
        public static string BuildArgs(string batFileName)
        {
            string full = Path.Combine(Root, batFileName);
            string[] lines = File.ReadAllLines(full);

            int start = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].IndexOf("winws.exe", StringComparison.OrdinalIgnoreCase) >= 0) { start = i; break; }
            if (start < 0) throw new Exception(string.Format(Loc.T("strat.noWinws"), batFileName));

            var sb = new StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                string t = lines[i].TrimEnd();
                bool cont = t.EndsWith("^");
                if (cont) t = t.Substring(0, t.Length - 1);
                sb.Append(t).Append(' ');
                if (!cont) break;
            }
            string cmd = sb.ToString();

            int idx = cmd.IndexOf("winws.exe", StringComparison.OrdinalIgnoreCase);
            cmd = cmd.Substring(idx + 9);
            if (cmd.StartsWith("\"")) cmd = cmd.Substring(1);

            string tcp, udp; GameValues(out tcp, out udp);
            cmd = cmd.Replace("%BIN%", Bin)
                     .Replace("%LISTS%", Lists)
                     .Replace("%GameFilterTCP%", tcp)
                     .Replace("%GameFilterUDP%", udp);

            cmd = UnescapeCaret(cmd);
            if (!IpsetEnabled)
            {
                // В bat-файлах каждая группа правил разделена --new.
                // В первой группе правил (parts[0]) находятся глобальные фильтры драйвера (--wf-tcp / --wf-udp).
                // Удалять её целиком нельзя — удаляем только аргумент --ipset.
                // Последующие секции с --ipset убираются целиком, чтобы их десинк-фильтры не применились ко всему трафику.
                var parts = Regex.Split(cmd, @"\s+--new\s+");
                var kept = new List<string>();
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (part.IndexOf("--ipset=", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (i == 0)
                        {
                            string cleaned = Regex.Replace(part, @"--ipset=""?[^""\s]+""?\s*", "");
                            kept.Add(cleaned.Trim());
                        }
                    }
                    else kept.Add(part);
                }
                cmd = string.Join(" --new ", kept.ToArray());
            }
            return cmd.Trim();
        }

        // cmd.exe разворачивает ^X -> X. Продолжения строк уже удалены выше.
        static string UnescapeCaret(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '^' && i + 1 < s.Length) { sb.Append(s[i + 1]); i++; }
                else if (c != '^') sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
