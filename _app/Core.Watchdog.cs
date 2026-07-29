using System;
using System.Collections.Generic;
using System.Threading;

namespace ZapretStudio
{
    // Автоподбор лучшей стратегии и фоновый мониторинг (автопереключение).
    static partial class Core
    {
        // Результат проверки одной стратегии.
        public class StratScore
        {
            public string File;
            public int Ok;
            public int Total;
            public double AvgMs;
        }

        // Быстрые пробы для мониторинга (меньше чем StratProbes — только ключевые).
        static List<Target> QuickProbes()
        {
            var l = new List<Target>();
            AddQ(l, "Discord", "https://discord.com");
            AddQ(l, "YouTube", "https://www.youtube.com");
            AddQ(l, "YT CDN", "https://i.ytimg.com");
            return l;
        }
        static void AddQ(List<Target> l, string name, string url)
        {
            var t = new Target { Key = name, Name = name, Group = "quick", Kind = "HTTP", Url = url };
            try { t.Host = new Uri(url).Host; } catch { t.Host = url; }
            l.Add(t);
        }

        // Проверить одну стратегию: запустить winws, прогнать пробы через curl (3 протокола), вернуть счёт.
        // Логика как в test zapret.ps1: ожидание 5с, curl -I с HTTP/1.1 + TLS1.2 + TLS1.3.
        public static StratScore TestStrategy(string batFile, List<Target> probes, Func<bool> cancel)
        {
            var sc = new StratScore { File = batFile, Total = probes.Count };
            if (!TryBeginWinwsOperation()) return sc;
            try
            {
                KillWinws();
                StartWinws(batFile);
                Thread.Sleep(5000); // как в test zapret.ps1 — 5 секунд на инициализацию winws
                long msSum = 0; int msCount = 0;
                foreach (var t in probes)
                {
                    if (cancel != null && cancel()) break;
                    if (t.Kind == "PING")
                    {
                        var pr = TestPing(t.Host, 5000);
                        if (pr.State == "reachable") { sc.Ok++; if (pr.Ms >= 0) { msSum += pr.Ms; msCount++; } }
                    }
                    else
                    {
                        var cr = CurlCheck(t.Url, 5);
                        if (cr.Verdict == "ok") { sc.Ok++; if (cr.Ms >= 0) { msSum += cr.Ms; msCount++; } }
                    }
                }
                if (msCount > 0) sc.AvgMs = (double)msSum / msCount;
            }
            catch { }
            finally { try { KillWinws(); } catch { } EndWinwsOperation(); }
            return sc;
        }

        // ---- Автопереключение (watchdog) ----
        public static bool WatchdogEnabled
        {
            get { return GetBool("watchdog_enabled", false); }
            set { SetBool("watchdog_enabled", value); SaveConfig(); }
        }
        public static int WatchdogIntervalMin
        {
            get { return GetInt("watchdog_interval", 5); }
            set { SetInt("watchdog_interval", value); SaveConfig(); }
        }

        // Проверить, работает ли текущая стратегия. Возвращает true если всё ок.
        public static bool QuickCheck()
        {
            var probes = QuickProbes();
            int ok = 0;
            foreach (var t in probes)
            {
                var res = TestTarget(t, 5000);
                if (res.State == "reachable") ok++;
            }
            return ok > 0; // хотя бы один эндпоинт доступен
        }

        // Найти следующую рабочую стратегию (перебор по кругу).
        public static string FindWorkingStrategy(string currentFile, Func<bool> cancel)
        {
            var files = GetStrategyFiles();
            if (files.Count == 0) return null;
            int start = files.IndexOf(currentFile);
            if (start < 0) start = 0;
            var probes = QuickProbes();
            for (int i = 1; i <= files.Count; i++)
            {
                if (cancel != null && cancel()) return null;
                string f = files[(start + i) % files.Count];
                if (f == currentFile) continue;
                var sc = TestStrategy(f, probes, cancel);
                if (sc.Ok > 0) return f;
            }
            return null;
        }
    }
}
