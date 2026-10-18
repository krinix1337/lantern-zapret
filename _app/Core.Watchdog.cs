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
            public bool Error;
            public string ErrorMessage;
        }

        // Сколько проб держать одновременно. utils/test zapret.ps1 (его запускает
        // service.bat → «Проверить стратегии») держит ровно 8 ранспейсов, а каждый
        // CurlCheck внутри плодит ещё по 3 процесса curl.exe. Раньше пробы
        // стартовали все разом: на полном списке целей из utils/targets.txt это
        // десятки curl.exe, они толкались за сеть и процессор, часть умирала по
        // своему же таймауту — и стратегия, которая в service.bat числилась
        // рабочей, в приложении получала «не работает».
        public const int MaxParallelProbes = 8;

        // Прогрев WinDivert перед пробами. В test zapret.ps1 — Start-Sleep 5 с;
        // прежние 2 с не хватало, чтобы драйвер и все группы правил (--new)
        // успели перехватить первые пакеты.
        public const int ProbeWarmupMs = 5000;

        // Окно ожидания на весь набор проб: CurlCheck ждёт до timeoutSec*1000+1500
        // (для 5 с — 6500 мс), пробы идут пачками по MaxParallelProbes.
        public static int ProbeBudgetMs(int probeCount)
        {
            int batches = (probeCount + MaxParallelProbes - 1) / MaxParallelProbes;
            if (batches < 1) batches = 1;
            return batches * 7000 + 2000;
        }

        // Прогнать пробы не более чем в MaxParallelProbes потоков.
        // Возвращает true, если все цели отработали в срок.
        public static bool RunProbes(List<Target> probes, Action<Target> one, Func<bool> cancel, out int missed)
        {
            int idx = -1;
            int done = 0;
            var barrier = new WorkBarrier(probes.Count);
            int workers = Math.Min(MaxParallelProbes, probes.Count);
            for (int w = 0; w < workers; w++)
                ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        while (true)
                        {
                            int i = Interlocked.Increment(ref idx);
                            if (i >= probes.Count) break;
                            try { if (cancel == null || !cancel()) one(probes[i]); }
                            catch { }
                            // Signal обязан быть при любом исходе, иначе барьер
                            // не соберётся и весь прогон упрётся в таймаут.
                            finally { Interlocked.Increment(ref done); barrier.Signal(); }
                        }
                    }
                    catch { }
                });
            bool allInTime = barrier.Wait(ProbeBudgetMs(probes.Count));
            missed = probes.Count - done;
            return allInTime && missed <= 0;
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
                if (!StartWinws(batFile))
                {
                    sc.Error = true;
                    sc.ErrorMessage = Loc.T("check.strat.notStarted");
                    return;
                }
                for (int i = 0; i < ProbeWarmupMs / 100 && (cancel == null || !cancel()); i++) Thread.Sleep(100);
                long msSum = 0; int msCount = 0;
                int ok = 0;
                int missed;
                bool complete = RunProbes(probes, delegate(Target tt)
                {
                    if (tt.Kind == "PING")
                    {
                        var pr = TestPing(tt.Host, 4000);
                        if (pr.State == "reachable")
                        {
                            Interlocked.Increment(ref ok);
                            if (pr.Ms >= 0) { Interlocked.Add(ref msSum, pr.Ms); Interlocked.Increment(ref msCount); }
                        }
                    }
                    else
                    {
                        var cr = CurlCheck(tt.Url, 5);
                        if (cr.Verdict == "ok")
                        {
                            Interlocked.Increment(ref ok);
                            if (cr.Ms >= 0) { Interlocked.Add(ref msSum, cr.Ms); Interlocked.Increment(ref msCount); }
                        }
                    }
                }, cancel, out missed);
                // №15: результат барьера не выбрасывается — недобранное число проб
                // иначе молча уходило в заниженный счётчик «работает».
                if (!complete)
                    Warn(string.Format(Loc.T("net.probesTimeout"), missed, probes.Count));
                sc.Ok = ok;
                if (msCount > 0) sc.AvgMs = (double)msSum / msCount;
            }
            catch { }
            finally { try { KillWinws(); } catch { } }
        }

        // Автопереключение (watchdog) вырезано как мёртвый код: ни WatchdogEnabled,
        // ни WatchdogIntervalMin, ни QuickCheck, ни FindWorkingStrategy не вызывались.
        // Ключ "watchdog_enabled" при этом живёт и читается напрямую в
        // MainWindow.cs (_bypassMonitor: упал winws — перезапустить в том же режиме),
        // поэтому удалять его из конфига нельзя. Вычеркнутый исходник сохранён в
        // musor/disabled/Core.Watchdog.autoswitch.cs.
    }
}
