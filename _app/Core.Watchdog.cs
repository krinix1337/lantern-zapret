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
            // Проверка не выполнена: операция winws занята другим действием.
            public bool Busy;
        }

        // Быстрые пробы для мониторинга (меньше чем StratProbes — только ключевые).
        // public: проверяется в юнит-тестах SelfTest.
        public static List<Target> QuickProbes()
        {
            var l = new List<Target>();
            AddQ(l, "Discord", "https://discord.com");
            AddQ(l, "YouTube", "https://www.youtube.com");
            AddQ(l, "YT CDN", "https://i.ytimg.com");
            return l;
        }
        public static void AddQ(List<Target> l, string name, string url)
        {
            var t = new Target { Key = name, Name = name, Group = "quick", Kind = "HTTP", Url = url };
            try { t.Host = new Uri(url).Host; } catch { t.Host = url; }
            l.Add(t);
        }

        // Проверить одну стратегию: запустить winws, прогнать пробы через curl (3 протокола), вернуть счёт.
        public static StratScore TestStrategy(string batFile, List<Target> probes, Func<bool> cancel)
        {
            var sc = new StratScore { File = batFile, Total = probes.Count };
            if (!TryBeginWinwsOperation()) { sc.Busy = true; return sc; }
            try { RunStrategyProbe(batFile, probes, cancel, sc); }
            finally { EndWinwsOperation(); }
            return sc;
        }

        // Вариант для вызывающего, который уже удерживает TryBeginWinwsOperation
        // (массовые прогоны из UI — чтобы между стратегиями никто не вклинился).
        public static void RunStrategyProbe(string batFile, List<Target> probes, Func<bool> cancel, StratScore sc)
        {
            try
            {
                KillWinws();
                StartWinws(batFile);
                Thread.Sleep(600); // 600ms
                long msSum = 0; int msCount = 0;
                int ok = 0;
                var barrier = new WorkBarrier(probes.Count);
                foreach (var t in probes)
                {
                    var tt = t;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            if (cancel == null || !cancel())
                            {
                                if (tt.Kind == "PING")
                                {
                                    var pr = TestPing(tt.Host, 3000);
                                    if (pr.State == "reachable")
                                    {
                                        Interlocked.Increment(ref ok);
                                        if (pr.Ms >= 0) { Interlocked.Add(ref msSum, pr.Ms); Interlocked.Increment(ref msCount); }
                                    }
                                }
                                else
                                {
                                    var cr = CurlCheck(tt.Url, 3);
                                    if (cr.Verdict == "ok")
                                    {
                                        Interlocked.Increment(ref ok);
                                        if (cr.Ms >= 0) { Interlocked.Add(ref msSum, cr.Ms); Interlocked.Increment(ref msCount); }
                                    }
                                }
                            }
                        }
                        catch { }
                        finally { barrier.Signal(); }
                    });
                }
                // CurlCheck сам ждёт до 4,5 с, поэтому окно ожидания с запасом:
                // прежние 4000 мс истекали почти всегда, и результаты читались,
                // пока пробы ещё шли.
                barrier.Wait(9000);
                sc.Ok = ok;
                if (msCount > 0) sc.AvgMs = (double)msSum / msCount;
            }
            catch { }
            finally { try { KillWinws(); } catch { } }
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
