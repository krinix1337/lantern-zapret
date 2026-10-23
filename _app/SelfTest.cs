#if SELFTEST
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZapretStudio
{
    // Автотест интерфейса: реально прогоняет WPF-пайплайн (шаблоны, layout, смену темы/языка)
    // и ловит исключения. Запускается только в отладочной сборке (-define:SELFTEST).
    static class SelfTest
    {
        static readonly StringBuilder Log = new StringBuilder();
        static int _pass, _fail;

        public static void Run()
        {
            var app = new App();
            Theme.InstallScrollBarStyle();

            // В SELFTEST-режиме глобальный обработчик из App.Main не подключается
            // (Main выходит раньше) — любое исключение в колбэке диспатчера убивает
            // процесс без лога. Ловим сами и обязательно считаем падением: иначе
            // Click/Loaded, упавший внутри диспатчера, оставлял прогон «зелёным».
            app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                _fail++;
                Line("DISPATCHER", e.Exception.ToString());
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { WriteLog("PASS=" + _pass + " FAIL=" + _fail + "\n" + Log.ToString() + "\nCRASH:\n" + e.ExceptionObject); } catch { }
            };

            app.Startup += delegate
            {
                try { Execute(); }
                catch (Exception ex) { _fail++; Line("FATAL", ex.ToString()); }
                finally
                {
                    // Код возврата обязателен: build-selftest.cmd собирает -target:exe,
                    // и без этого шаг CI оставался зелёным при любом числе FAIL.
                    try { WriteLog("PASS=" + _pass + " FAIL=" + _fail + "\n" + Log.ToString()); }
                    catch (Exception ex) { Console.WriteLine("[WARN] log write: " + ex.Message); }
                    Environment.ExitCode = _fail > 0 ? 1 : 0;
                    app.Shutdown();
                }
            };
            app.Run();
            // Окно теста закрывается во время Shutdown и снова сохраняет геометрию
            // в gui-config.ini уже после первого восстановления снимка. Второй
            // проход возвращает настройки пользователя (в т.ч. размер окна).
            try { RestoreConfig(); } catch { }
        }

        // Лог пишется в %LOCALAPPDATA%\Lantern, а не рядом с exe: папка установки
        // (C:\Program Files\...) может быть недоступна для записи, и исключение из
        // finally не дало бы выполниться app.Shutdown() — процесс висел бы до
        // тайм-аута CI. Фолбэк — каталог exe, чтобы путь из CI-артефактов работал.
        static void WriteLog(string text)
        {
            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Core.AppName),
                AppDomain.CurrentDomain.BaseDirectory
            };
            Exception last = null;
            foreach (var root in roots)
            {
                try
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    if (!Directory.Exists(root)) Directory.CreateDirectory(root);
                    File.WriteAllText(Path.Combine(root, "_selftest.log"), text, Encoding.UTF8);
                    Console.WriteLine("[INFO] log written to " + Path.Combine(root, "_selftest.log"));
                    return;
                }
                catch (Exception ex) { last = ex; }
            }
            if (last != null) throw last;
        }

        static void Execute()
        {
            Loc.Load();
            Theme.Apply(ThemeMode.Dark);
            if (!Core.LocateRoot()) { Line("WARN", "root not found; using best-effort"); }
            try { Core.LoadConfig(); } catch (Exception ex) { Line("WARN", "LoadConfig: " + ex.Message); }
            // Шрифты из кэша обязательно: App.Main их подключает, и без этого шага
            // раскладка мерялась бы системным шрифтом с другими метриками — то есть
            // не тем, что видит пользователь.
            Line("INFO", "ui fonts from cache: " + Core.ConfigureUiFontsFromCache());
            // Какой корень использован — без этого «strat files=0» невозможно
            // истолковать: то ли верстка пустая, то ли компонентов нет.
            Line("INFO", "root=" + (Core.Root ?? "<null>") + " base=" + AppDomain.CurrentDomain.BaseDirectory);

            // 6f: словарь конфига в памяти один на процесс, а прогон переключает язык,
            // тему, reduce_motion и режимы ipset. Любой SaveConfig() после этого
            // (Loc.SetLang, Theme.ApplySystem24Flavor, сеттер IpsetEnabled) записал бы
            // тестовые значения в настоящий utils\gui-config.ini.
            SnapshotConfig();
            try { RunChecks(); }
            finally { RestoreConfig(); }
        }

        // Ключи, которые прогон трогает, и снимок самого файла: восстановление
        // побайтово, чтобы gui-config.ini пользователя остался нетронутым.
        static readonly string[] GuardedKeys = { "ipset_enabled", "lang", "theme", "reduce_motion", "sidebar_collapsed", "last_strategy" };
        static Dictionary<string, string> _cfgWas;
        static string _cfgFileWas;
        static byte[] _cfgBytesWas;

        static void SnapshotConfig()
        {
            _cfgWas = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var k in GuardedKeys) _cfgWas[k] = Core.Get(k, null);
            try
            {
                _cfgFileWas = Core.ConfigFile;
                _cfgBytesWas = File.Exists(_cfgFileWas) ? File.ReadAllBytes(_cfgFileWas) : null;
            }
            catch (Exception ex) { _cfgFileWas = null; _cfgBytesWas = null; Line("WARN", "config snapshot: " + ex.Message); }
        }

        static void RestoreConfig()
        {
            if (_cfgWas != null)
                foreach (var kv in _cfgWas)
                {
                    if (kv.Value == null) Core.Unset(kv.Key);
                    else Core.Set(kv.Key, kv.Value);
                }
            if (_cfgFileWas == null) return;
            try
            {
                byte[] now = File.Exists(_cfgFileWas) ? File.ReadAllBytes(_cfgFileWas) : null;
                if (SameBytes(now, _cfgBytesWas)) return;
                if (_cfgBytesWas == null) File.Delete(_cfgFileWas);
                else File.WriteAllBytes(_cfgFileWas, _cfgBytesWas);
                Line("WARN", "gui-config.ini changed during the run; restored from snapshot");
            }
            catch (Exception ex) { _fail++; Line("FAIL", "config restore: " + ex.Message); }
        }

        static bool SameBytes(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void RunChecks()
        {
            // 0) Чистая логика без UI — версии, парсеры, кодировки, буферы.
            PureTests();

            // Flowseal не публикует SHA256SUMS.txt: его отсутствие не повреждение.
            Try("integrity optional manifest", delegate
            {
                string dir = Path.Combine(Path.GetTempPath(), "lantern-integrity-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                try
                {
                    var issues = Core.EngineIntegrityIssuesAt(dir);
                    foreach (string issue in issues)
                        Assert(issue.IndexOf("SHA256SUMS", StringComparison.OrdinalIgnoreCase) < 0,
                            "отсутствие SHA256SUMS.txt записано как повреждение: " + issue);
                    Assert(issues.Count >= 1, "пустой каталог движка не дал ни одной проблемы");
                }
                finally { try { Directory.Delete(dir, true); } catch { } }
            });

            // Сохранённый размер окна должен восстанавливаться при старте.
            Core.SetInt("window_width", 800);
            Core.SetInt("window_height", 500);
            Core.SetBool("window_maximized", false);
            var win = new MainWindow();
            Try("window size restore", delegate
            {
                Assert(Math.Abs(win.Width - 800) <= 1.0 && Math.Abs(win.Height - 500) <= 1.0,
                    "размер окна не восстановлен: " + win.Width.ToString("0") + "x" + win.Height.ToString("0"));
            });
            win.Width = 1200; win.Height = 800;
            win.WindowStartupLocation = WindowStartupLocation.Manual;
            win.Left = -4000; win.Top = -4000;      // за пределами экрана
            win.ShowInTaskbar = false;
            win.Show();
            Pump();

            string[] pages = { "overview", "strategies", "check", "service", "filters", "settings", "updates", "log", "about" };

            // 1) Навигация по всем страницам в тёмной теме
            foreach (var key in pages) NavCheck(win, key);

            // 2) Смена темы туда-обратно на КАЖДОЙ странице (ловит read-only brush)
            foreach (var key in pages)
            {
                NavCheck(win, key);
                ThemeSwitch("light @" + key, ThemeMode.Light);
                ThemeSwitch("dark @" + key, ThemeMode.Dark);
            }

            // 3) Быстрые многократные переключения темы на Settings (там ComboBox-шаблоны)
            NavCheck(win, "settings");
            for (int i = 0; i < 4; i++)
            {
                ThemeSwitch("rapid light " + i, ThemeMode.Light);
                ThemeSwitch("rapid dark " + i, ThemeMode.Dark);
            }

            // 3b) Подсветка карточки под курсором должна оставаться КРУГОЙ.
            // Радиусы задаются в долях bounding box, и без поправки на отношение
            // сторон пятно на широкой карточке растягивалось по всей рамке —
            // «свечение прямоугольное».
            Try("spotlight круглое", delegate
            {
                bool motionWas = Theme.AnimationsEnabled;
                var modeWas = Theme.Mode;
                Core.SetBool("reduce_motion", false);
                Theme.Apply(ThemeMode.Dark);
                try
                {
                    var bd = new Border { Width = 400, Height = 100, Background = Theme.BrSurface };
                    UI.AttachSpotlight(bd);
                    bd.Measure(new Size(400, 100));
                    bd.Arrange(new Rect(0, 0, 400, 100));
                    Pump();
                    var args = new System.Windows.Input.MouseEventArgs(
                        System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount)
                    {
                        RoutedEvent = UIElement.MouseEnterEvent
                    };
                    bd.RaiseEvent(args);
                    var rg = bd.BorderBrush as RadialGradientBrush;
                    Assert(rg != null, "наведение не поставило радиальную подсветку рамки");
                    // Круг в абсолютных пикселях: RadiusX * W == RadiusY * H.
                    double dx = rg.RadiusX * 400, dy = rg.RadiusY * 100;
                    Assert(Math.Abs(dx - dy) < 1.5,
                        "пятно эллиптическое: по ширине " + dx + "px, по высоте " + dy + "px");
                }
                finally
                {
                    Core.SetBool("reduce_motion", motionWas ? false : true);
                    Theme.Apply(modeWas);
                }
            });

            // 3c) Сетка карточек стратегий обязана добирать ширину до края блока.
            // На старом WrapPanel + ручном Relayout ширина карточек пересчитывалась
            // не всегда, и при расширении окна справа оставалась пустая полоса.
            Try("strategies grid fills width", delegate
            {
                NavCheck(win, "strategies");
                foreach (var w in new double[] { 860, 1000, 1180, 1400 })
                {
                    win.Width = w; Pump(); ForceLayout(win); Pump();
                    var page = FindPage(win) as StrategiesPage;
                    Assert(page != null, "страница стратегий не найдена");
                    var list = page.DemoStratList;
                    Assert(list != null && list.Children.Count > 0, "сетка стратегий пуста");
                    var first = (FrameworkElement)list.Children[0];
                    double y0 = first.TranslatePoint(new Point(0, 0), list).Y;
                    double right = 0;
                    for (int i = 0; i < list.Children.Count; i++)
                    {
                        var fe = list.Children[i] as FrameworkElement;
                        if (fe == null) continue;
                        var p = fe.TranslatePoint(new Point(0, 0), list);
                        if (Math.Abs(p.Y - y0) > 1) break;      // сравниваем только первую строку
                        right = Math.Max(right, p.X + fe.ActualWidth);
                    }
                    double gapRight = list.ActualWidth - right;
                    Assert(gapRight <= 1.5, "при ширине окна " + w + " справа от карточек пусто " + gapRight + "px");
                    var banner = page.DemoBanner;
                    if (banner != null)
                    {
                        double bl = banner.TranslatePoint(new Point(0, 0), list).X;
                        double br = bl + banner.ActualWidth;
                        Assert(Math.Abs(bl) < 1.5 && Math.Abs(br - right) < 1.5,
                            "баннер и сетка не выровнены: баннер " + bl + ".." + br + ", карточки 0.." + right);
                    }
                }
                win.Width = 1200; Pump(); ForceLayout(win);
            });

            // 3b) Каждая тема из реестра по кругу. Раньше тест гонял только Light↔Dark,
            // и палитры AMOLED/Закат/System24 не проверялись на read-only кисти вообще.
            NavCheck(win, "overview");
            foreach (var mode in Theme.Modes) ThemeSwitch("all " + Theme.Name(mode), mode);
            ThemeSwitch("back to dark", ThemeMode.Dark);

            // 4) Смена языка туда-обратно
            LangSwitch("en", "en");
            LangSwitch("ru", "ru");

            // 5) Страница «Стратегии»: клики по категориям + выбор карточки
            NavCheck(win, "strategies");
            StrategiesInteractions(win);

            // 5b) Свёртывание боковой панели: иконки не должны выходить за пределы 64px.
            CollapseCheck(win);
            // Визуальная проверка свёрнутой панели.
            Core.SetBool("reduce_motion", true);
            var cb = FindByName<Button>(win, Loc.T("mw.collapse"));
            if (cb != null) { cb.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Pump(); ForceLayout(win); Shot(win, "collapsed"); cb.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Pump(); ForceLayout(win); }

            // 5c) Скриншоты для визуальной проверки (темы оформления и все разделы в единой тёмной теме).
            Core.SetBool("reduce_motion", true); // без анимаций — контент виден сразу
            win.Width = 1200; win.Height = 800; Pump(); ForceLayout(win);
            
            // Темы оформления: скриншоты берутся из реестра Theme.Modes, поэтому
            // добавление или удаление темы не требует правки этого места.
            NavCheck(win, "overview");
            foreach (var m in Theme.Modes)
            {
                Theme.Apply(m); Pump(); ForceLayout(win);
                Shot(win, "overview-" + Theme.Name(m));
            }
            foreach (var flv in Theme.System24Flavors)
            {
                Theme.ApplySystem24Flavor(flv); Pump(); ForceLayout(win);
                Shot(win, "overview-system24-" + flv);
            }
            Theme.ApplySystem24Flavor(Theme.FlavorDefault);
            NavCheck(win, "settings"); win.Height = 1000; ForceLayout(win); Shot(win, "section-settings-system24"); win.Height = 800; ForceLayout(win);
            NavCheck(win, "overview");
            
            // Все разделы приложения в единой фирменной тёмной теме (Dark)
            Theme.Apply(ThemeMode.Dark); Pump(); ForceLayout(win);
            NavCheck(win, "overview"); ForceLayout(win); Shot(win, "section-overview");
            NavCheck(win, "strategies"); ForceLayout(win); Shot(win, "section-strategies");
            NavCheck(win, "check"); ForceLayout(win); Shot(win, "section-check");
            var cpDiag = FindPage(win) as CheckPage;
            if (cpDiag != null) { cpDiag.ShowTab("diag"); Pump(); ForceLayout(win); Shot(win, "section-check-diag"); }
            Theme.Apply(ThemeMode.System24); Pump(); ForceLayout(win); Shot(win, "section-check-diag-system24");
            Theme.Apply(ThemeMode.Dark);
            if (cpDiag != null) { cpDiag.ShowTab("targets"); Pump(); ForceLayout(win); }
            NavCheck(win, "service"); ForceLayout(win); Shot(win, "section-service");
            NavCheck(win, "filters"); ForceLayout(win); Shot(win, "section-filters");
            NavCheck(win, "settings"); ForceLayout(win); Shot(win, "section-settings");
            NavCheck(win, "updates"); ForceLayout(win); Shot(win, "section-updates");
            NavCheck(win, "log"); ForceLayout(win); Shot(win, "section-log");
            NavCheck(win, "about"); ForceLayout(win); Shot(win, "section-about");

            // 7) Журнал: строки реально отрисовываются (биндинги VM на свойствах,
            //    а не полях — иначе строки схлопываются в нулевую высоту).
            NavCheck(win, "log");
            Try("log rows render", delegate
            {
                Core.Info("selftest-log-row-check");
                Core.Good("selftest-log-row-ok");
                Pump(); ForceLayout(win);
                ListBox lb = null;
                foreach (var l in Descendants<ListBox>(win)) { lb = l; break; }
                Assert(lb != null && lb.Items.Count > 0);
                lb.ScrollIntoView(lb.Items[lb.Items.Count - 1]);
                Pump(); ForceLayout(win);
                double maxH = 0;
                foreach (var it in Descendants<System.Windows.Controls.ListBoxItem>(lb))
                    maxH = Math.Max(maxH, it.ActualHeight);
                Assert(maxH > 8);
            });

            // 6) Разные ширины окна (проверка сетки/раскладки)
            foreach (var w in new double[] { 1120, 1000, 1300, 1600 })
            {
                // На CI экран бывает 1024 px: Windows не даёт окну стать шире
                // рабочей области, и проверка «ширина применилась» падала не
                // из-за вёрстки. Проверяем фактически достижимую ширину.
                double want = Math.Max(win.MinWidth, Math.Min(w, SystemParameters.WorkArea.Width));
                Try("resize " + w, delegate
                {
                    win.Width = want;
                    Pump();
                    NavCheck(win, "strategies");
                    ForceLayout(win);
                    MeasureGrid(win, want);
                    // Без проверки факта ширина могла не применяться (MinWidth,
                    // Zoom, отказ макета), и «проверка сетки» шла на одной ширине.
                    Assert(Math.Abs(win.ActualWidth - want) <= 1.0,
                        "ширина не применилась: ActualWidth=" + win.ActualWidth.ToString("0") + ", ожидалось " + want.ToString("0"));
                    Assert(win.ActualWidth >= win.MinWidth - 1.0,
                        "ActualWidth ниже MinWidth: " + win.ActualWidth + " < " + win.MinWidth);
                });
            }

            // 6b) Геометрический аудит каждой страницы на граничных ширинах и в
            //     двух языках: в английском подписи длиннее, и если что-то
            //     налезает друг на друга — вылезет именно там.
            int layoutIssues = 0, layoutAudits = 0;
            foreach (var lang in new string[] { "ru", "en" })
            {
                Loc.SetLang(lang); Pump();
                // 820 — почти нижняя граница окна (MinWidth 760 плюс рамка): с тех
                // пор как минимум перестал быть «половиной экрана», узкое окно стало
                // реальным состоянием, и проверять надо именно его.
                foreach (var w in new double[] { 820, 1000, 1120, 1600 })
                {
                    win.Width = w; Pump(); ForceLayout(win);
                    foreach (var key in pages)
                    {
                        NavCheck(win, key);
                        layoutAudits++; layoutIssues += LayoutAudit(win, lang + " " + w.ToString("0") + " " + key);
                        if (key == "check") { layoutAudits++; layoutIssues += AuditCheckTabs(win, lang + " " + w.ToString("0")); }
                        if (key == "updates")
                        {
                            var up = FillUpdatesProgress(win);
                            if (up != null)
                            {
                                layoutAudits++; layoutIssues += LayoutAudit(win, lang + " " + w.ToString("0") + " updates progress");
                                up.HideDemoProgress();
                                Pump(); ForceLayout(win);
                                // История релизов на той же ширине: панель самая
                                // плотная на странице, и резалась именно она.
                                if (up.ShowDemoHistory())
                                {
                                    Pump(); ForceLayout(win);
                                    layoutAudits++;
                                    layoutIssues += LayoutAudit(win, lang + " " + w.ToString("0") + " updates history");
                                    up.HideDemoHistory();
                                    Pump(); ForceLayout(win);
                                }
                            }
                        }
                    }
                }
            }
            Loc.SetLang("ru"); Pump();
            // Раньше весь аудит (2 языка x 3 ширины x 9 страниц) сводился к
            // одному _pass/_fail: «3 обрезанных подписи» выглядели так же, как «300».
            // Теперь зачёт пропорционален: каждый просмотренный экран в _pass, каждая
            // найденная проблема отдельным _fail.
            _pass += layoutAudits;
            _fail += layoutIssues;
            Line(layoutIssues == 0 ? "INFO" : "FAIL", "layout audit screens=" + layoutAudits + " total issues=" + layoutIssues);

            // 6c) Снимки на минимальной ширине окна: самые плотные страницы.
            win.Width = 1000; win.Height = 680; Pump(); ForceLayout(win);
            NavCheck(win, "overview"); Shot(win, "min-overview");
            ScrollEnd(win); Shot(win, "min-overview-bottom"); ScrollHome(win);
            // Переход с главной в настройки: секция TG-прокси должна быть
            // ниже первого экрана, к ней надо доскроллить, а не просто открыть
            // настройки сверху.
            Try("settings tg section", delegate
            {
                foreach (double tgWidth in new double[] { 820, 1000, 1120 })
                {
                    win.Width = tgWidth; Pump(); ForceLayout(win);
                    win.OpenTgProxySettings();
                    for (int i = 0; i < 50; i++) { System.Threading.Thread.Sleep(20); Pump(); }
                    ForceLayout(win);
                    var page = FindPage(win) as SettingsPage;
                    Assert(page != null, "страница настроек не найдена");
                    Assert(string.Equals(win.CurrentPageKey, "settings", StringComparison.Ordinal),
                        "переход не в настройки: " + win.CurrentPageKey);
                    Assert(page.ScrollOffset > 0, "к секции TG не прокрутило на " + tgWidth.ToString("0") + ": offset=" + page.ScrollOffset);
                    int issues = AuditScope(page, win, Bounds(page, win), null, "settings tg section " + tgWidth.ToString("0"));
                    Assert(issues == 0, "секция TG на " + tgWidth.ToString("0") + ": проблем разметки " + issues);
                    if (Math.Abs(tgWidth - 1000) < 0.1) Shot(win, "settings-tg");
                    page.ScrollOffset = 0;
                    Pump(); ForceLayout(win);
                }
                win.Width = 1000; Pump(); ForceLayout(win);
            });
            // Три компактных чипа в одну линию: иконка+подпись не должны
            // обрезаться и не должны растягиваться на всю карточку.
            Try("settings tg chips", delegate
            {
                win.Width = 1000; Pump(); ForceLayout(win);
                win.OpenTgProxySettings();
                for (int i = 0; i < 50; i++) { System.Threading.Thread.Sleep(20); Pump(); }
                ForceLayout(win);
                var page = FindPage(win) as SettingsPage;
                Assert(page != null, "страница настроек не найдена");
                page.ShowDemoTgChips();
                Pump(); ForceLayout(win);
                Shot(win, "settings-tg-chips");

                foreach (double tgWidth in new double[] { 820, 1000, 1120 })
                {
                    win.Width = tgWidth; Pump(); ForceLayout(win);
                    page.ShowDemoTgChips();
                    Pump(); ForceLayout(win);
                    int issues = AuditScope(page, win, Bounds(page, win), null, "settings tg chips " + tgWidth.ToString("0"));
                    Assert(issues == 0, "чипы TG на " + tgWidth.ToString("0") + ": проблем разметки " + issues);
                }
                page.HideDemoTgChips();
                win.Width = 1000; Pump(); ForceLayout(win);
            });
            // Самая длинная комбинированная плашка статуса должна влезать
            // в шапку на минимальной ширине, в том числе в акцентном цвете.
            Try("top status long", delegate
            {
                win.Width = 820; Pump(); ForceLayout(win);
                NavCheck(win, "overview");
                win.ShowDemoTopStatus("state.bypassTg", true);
                Pump(); ForceLayout(win);
                int issues = LayoutAudit(win, "top status bypass+tg");
                Assert(issues == 0, "длинная плашка статуса: проблем разметки " + issues);
                win.ShowDemoTopStatus("state.tg", true);
                Pump(); ForceLayout(win);
                issues = LayoutAudit(win, "top status tg accent");
                Assert(issues == 0, "плашка TG в акценте: проблем разметки " + issues);
                win.RefreshTop();
                win.Width = 1000; Pump(); ForceLayout(win);
            });
            NavCheck(win, "settings"); Shot(win, "min-settings");
            NavCheck(win, "updates"); Shot(win, "min-updates");
            // Полоса загрузки обновления: дорожка во всю ширину карточки, на 0 %
            // остаётся видимый кусочек, при неизвестном размере — вся дорожка.
            Try("update progress bar", delegate
            {
                var up = FillUpdatesProgress(win);
                Assert(up != null);
                up.CheckDemoProgress();
            });
            {
                var up = FindPage(win) as UpdatesPage;
                if (up != null) { up.ScrollProgressIntoView(); Pump(); ForceLayout(win); }
            }
            Shot(win, "min-updates-progress");
            {
                var up = FindPage(win) as UpdatesPage;
                if (up != null) { up.HideDemoProgress(); Pump(); ForceLayout(win); }
            }
            // Раскрытая панель истории релизов: снимок и аудит должны видеть её
            // настоящей, а не свёрнутой — именно там текст резался и оставался
            // сырой markdown.
            Try("updates history panel", delegate
            {
                var up = FindPage(win) as UpdatesPage;
                Assert(up != null, "страница обновлений не найдена");
                Assert(up.ShowDemoHistory(), "панель истории не раскрылась");
                Pump(); ForceLayout(win);
                int issues = LayoutAudit(win, "updates history");
                Assert(issues == 0, "панель истории релизов: проблем разметки " + issues);
                Shot(win, "min-updates-history");
                up.HideDemoHistory(); Pump(); ForceLayout(win);
            });
            // Карточки версий: строка и кнопка «Обновить» появляются только когда
            // обновление действительно есть, иначе карточка молчит.
            Try("update notices only when needed", delegate
            {
                var up = FindPage(win) as UpdatesPage;
                Assert(up != null);
                up.CheckUpdateNotices();
                Pump(); ForceLayout(win);
            });
            Shot(win, "min-updates-uptodate");
            NavCheck(win, "check"); Shot(win, "min-check");
            ClickTab(win, Loc.T("check.tab.popular"));
            FillCheck(win); Shot(win, "min-check-filled");
            NavCheck(win, "service"); Shot(win, "min-service");
            NavCheck(win, "strategies"); Shot(win, "min-strategies");
            NavCheck(win, "filters"); Shot(win, "min-filters");
            Loc.SetLang("en"); Pump(); win.Width = 1000; win.Height = 680; ForceLayout(win);
            NavCheck(win, "settings"); Shot(win, "min-settings-en");
            NavCheck(win, "overview"); Shot(win, "min-overview-en");
            Loc.SetLang("ru"); Pump();

            Line("DONE", "all checks executed");
        }

        // Проверяет, что в свёрнутой панели иконки не выходят за пределы 64px.
        static void CollapseCheck(MainWindow win)
        {
            string collapseName = Loc.T("mw.collapse");
            Button toggle = null;
            foreach (var b in Descendants<Button>(win))
                if (System.Windows.Automation.AutomationProperties.GetName(b) == collapseName) { toggle = b; break; }
            if (toggle == null) { Line("WARN", "collapse button not found"); return; }

            Try("collapse toggle", delegate {
                Core.SetBool("reduce_motion", true); // без анимации ширина применяется сразу
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump(); ForceLayout(win);
            });

            // Находим боковую панель: Border с правой границей 1px (BorderThickness 0,0,1,0).
            Border sidebar = null;
            foreach (var bd in Descendants<Border>(win))
                if (bd.BorderThickness.Right == 1 && bd.BorderThickness.Left == 0 &&
                    bd.BorderThickness.Top == 0 && bd.ActualHeight > 300) { sidebar = bd; break; }
            double sw = sidebar != null ? sidebar.ActualWidth : 64;
            Line("MEAS", "collapsed sidebar width=" + sw.ToString("0"));

            int overflow = 0, icons = 0;
            // Меряем позиции всех иконок навигации относительно левого края окна.
            foreach (var p in Descendants<System.Windows.Shapes.Path>(win))
            {
                var gt = p.TransformToAncestor(win);
                var r = gt.TransformBounds(new Rect(0, 0, p.ActualWidth, p.ActualHeight));
                if (r.Left > sw + 4) continue; // не в области панели
                icons++;
                if (r.Right > sw + 0.5) { overflow++; Line("MEAS", "  icon clipped: left=" + r.Left.ToString("0.0") + " right=" + r.Right.ToString("0.0")); }
            }
            Line(overflow == 0 ? "INFO" : "FAIL", "collapsed icons=" + icons + " clipped=" + overflow);
            if (overflow > 0) _fail++; else _pass++;

            // Развернуть обратно.
            Try("expand toggle", delegate {
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump(); ForceLayout(win);
                Core.SetBool("reduce_motion", false);
            });
        }

        // Считает, сколько карточек помещается в один ряд (по Y-координате), и логирует.
        static void MeasureGrid(MainWindow win, double winW)
        {
            var page = FindPage(win);
            if (page == null) return;
            // Сбросить фильтр на «все» (первый чип), иначе список может быть пуст.
            foreach (var btn in Descendants<Button>(page))
            {
                if (btn.Tag is Action)
                {
                    btn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Pump(); ForceLayout(win);
                    break;
                }
            }
            foreach (var wp in Descendants<WrapPanel>(page))
            {
                double firstTop = double.NaN; int inFirstRow = 0; double cardW = 0; int total = 0;
                foreach (var ch in wp.Children)
                {
                    var b = ch as Border;
                    if (b == null || b.ActualWidth < 40) continue;
                    total++;
                    GeneralTransform gt = b.TransformToAncestor(wp);
                    Point pos = gt.Transform(new Point(0, 0));
                    if (double.IsNaN(firstTop)) firstTop = pos.Y;
                    if (Math.Abs(pos.Y - firstTop) < 2) { inFirstRow++; cardW = b.ActualWidth; }
                }
                Line("MEAS", "win=" + winW.ToString("0") + " wrap=" + wp.ActualWidth.ToString("0") +
                    " cols=" + inFirstRow + "/" + total + " cardW=" + cardW.ToString("0"));
                break;
            }
        }

        // ============ Геометрический аудит раскладки ============
        // Механическая проверка «ничего не наезжает друг на друга»:
        //  1) соседи в StackPanel/WrapPanel и элементы несовпадающих ячеек Grid
        //     не должны пересекаться по пикселям;
        //  2) однострочный текст не должен обрезаться (нужная ширина больше
        //     отведённой) — либо перенос, либо многоточие, либо влезает;
        //  3) ничто не должно выходить за левый/правый край страницы
        //     (вертикальный выход — это прокрутка, она законна).
        // Возвращает число найденных проблем.
        static int LayoutAudit(MainWindow win, string tag)
        {
            var page = FindPage(win);
            if (page == null) { Line("WARN", "audit " + tag + ": page not found"); return 0; }
            ForceLayout(win);
            Rect pr;
            try { pr = Bounds(page, win); }
            catch { return 0; }
            int issues = AuditScope(page, win, pr, null, tag);
            // Рама окна (заголовок и боковое меню) — отдельный проход; элементы
            // страницы пропускаем, чтобы не считать одни и те же проблемы дважды.
            issues += AuditScope(win, win, new Rect(0, 0, win.ActualWidth, win.ActualHeight), page, tag + " chrome");
            return issues;
        }

        static int AuditScope(FrameworkElement scope, Visual root, Rect refRect, Visual skip, string tag)
        {
            int overlaps = 0, clips = 0, trims = 0, outside = 0, spills = 0;
            foreach (var panel in Descendants<Panel>(scope))
            {
                if (skip != null && panel.IsDescendantOf(skip)) continue;
                bool grid = panel is Grid;
                if (!grid && !(panel is StackPanel) && !(panel is WrapPanel)) continue;
                var kids = new List<FrameworkElement>();
                foreach (UIElement u in panel.Children)
                {
                    var fe = u as FrameworkElement;
                    if (fe == null || fe.Visibility != Visibility.Visible) continue;
                    if (fe.ActualWidth < 1 || fe.ActualHeight < 1) continue;
                    kids.Add(fe);
                }
                // Содержимое шире контейнера: по горизонтали это всегда обрезка
                // (по вертикали — законная прокрутка, поэтому её не смотрим).
                if (panel.ActualWidth >= 1)
                    foreach (var kid in kids)
                    {
                        Rect k;
                        try { k = Bounds(kid, panel); }
                        catch { continue; }
                        if (k.Left >= -0.5 && k.Right <= panel.ActualWidth + 0.5) continue;
                        spills++;
                        if (spills <= 4)
                            Line("MEAS", "  spill in " + panel.GetType().Name + " w=" +
                                panel.ActualWidth.ToString("0") + ": " + Desc(kid) +
                                " x=" + k.Left.ToString("0") + ".." + k.Right.ToString("0"));
                    }
                for (int i = 0; i < kids.Count; i++)
                    for (int j = i + 1; j < kids.Count; j++)
                    {
                        // Наложение внутри одной ячейки Grid — приём вёрстки
                        // (фон под содержимым, значок поверх поля), не дефект.
                        if (grid && CellsIntersect(kids[i], kids[j])) continue;
                        Rect a, b;
                        try { a = Bounds(kids[i], panel); b = Bounds(kids[j], panel); }
                        catch { continue; }
                        a.Inflate(-0.5, -0.5);   // допуск на округление пикселей
                        if (a.Width <= 0 || a.Height <= 0) continue;
                        if (!a.IntersectsWith(b)) continue;
                        overlaps++;
                        if (overlaps <= 4)
                            Line("MEAS", "  overlap in " + panel.GetType().Name + ": " +
                                Desc(kids[i]) + " x " + Desc(kids[j]));
                    }
            }
            AuditText(scope, skip, ref clips, ref trims);
            AuditBounds(scope, root, refRect, skip, ref outside);
            AuditBorders(scope, skip, ref spills);

            string lvl = (overlaps + clips + outside + spills) == 0 ? "INFO" : "FAIL";
            Line(lvl, "audit " + tag + ": overlaps=" + overlaps + " clipped=" + clips +
                " trimmed=" + trims + " outside=" + outside + " spill=" + spills);
            return overlaps + clips + outside + spills;
        }

        static void StrategiesInteractions(MainWindow win)
        {
            var page = FindPage(win);
            if (page == null) { Line("FAIL", "strategies page not found"); _fail++; return; }
            Line("INFO", "strat files=" + Core.GetStrategyFiles().Count);
            int borders = 0, hands = 0;
            foreach (var bd in Descendants<Border>(page)) { borders++; if (bd.Cursor == System.Windows.Input.Cursors.Hand) hands++; }
            Line("INFO", "borders=" + borders + " handBorders=" + hands);
            // Замер ширин: WrapPanel и первые карточки (проверка сетки).
            foreach (var wp in Descendants<WrapPanel>(page))
            {
                Line("MEAS", "wrap.ActualWidth=" + wp.ActualWidth.ToString("0"));
                int shown = 0;
                foreach (var ch in wp.Children)
                {
                    var b = ch as Border;
                    if (b != null && shown < 4) { Line("MEAS", "  card.Width=" + b.Width.ToString("0") + " actual=" + b.ActualWidth.ToString("0")); shown++; }
                }
                break;
            }
            // Клики (выбор) по первым карточкам-стратегиям СНАЧАЛА (фильтр = «все»).
            // Раньше Try засчитывал PASS просто за «не бросило»: карточка могла
            // перестать быть кликабельной, и тест бы это не поймал.
            int clicked = 0, selected = 0;
            foreach (var bd in Descendants<Border>(page))
            {
                if (bd.Cursor == System.Windows.Input.Cursors.Hand && clicked < 3)
                {
                    var card = bd;
                    Try("card select", delegate
                    {
                        card.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                            System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                        { RoutedEvent = UIElement.MouseLeftButtonUpEvent, Source = card });
                        Pump();
                        Assert(!string.IsNullOrEmpty(win.CurrentStrategyFile()), "после клика по карточке стратегия не выбрана");
                        selected++;
                    });
                    clicked++;
                }
            }
            // В checkout релизного workflow нет general*.bat: движок и стратегии
            // скачиваются приложением при первом запуске. Проверять кликабельность
            // карточек там не на чем — это не дефект интерфейса.
            if (clicked == 0)
                Line("SKIP", "cards are clickable: в этом checkout нет general*.bat");
            else
                Try("cards are clickable", delegate
                {
                    Assert(selected == clicked, "из " + clicked + " кликов по карточкам выбор подтверждён только для " + selected);
                });
            Line("INFO", "card clicks attempted=" + clicked);
            // Петля по «чипам категорий» (Button с Tag is Action) удалена: таких
            // кнопок на странице стратегий нет — Ctl.Button кладёт в Tag массив
            // {border, brush, kind}, а фильтры категорий в UI не реализованы, так
            // что цикл никогда не выполнялся ни одного теста.
        }

        // Содержимое шире внутренней области Border (рамка + Padding). Border с
        // закруглением обрезает своё содержимое, поэтому именно так пропадает
        // хвост подписи на сжатой кнопке — снаружи это выглядит как «обрезано».
        static void AuditBorders(FrameworkElement scope, Visual skip, ref int spills)
        {
            foreach (var bd in Descendants<Border>(scope))
            {
                if (skip != null && bd.IsDescendantOf(skip)) continue;
                if (bd.Visibility != Visibility.Visible || bd.ActualWidth < 1) continue;
                var child = bd.Child as FrameworkElement;
                if (child == null || child.Visibility != Visibility.Visible) continue;
                if (child.ActualWidth < 1) continue;
                double right = bd.ActualWidth - bd.Padding.Right - bd.BorderThickness.Right;
                double left = bd.Padding.Left + bd.BorderThickness.Left;
                if (right - left < 1) continue;
                Rect k;
                try { k = Bounds(child, bd); }
                catch { continue; }
                if (k.Right <= right + 2.0 && k.Left >= left - 2.0) continue;   // < 2px — шум округления
                spills++;
                if (spills <= 8)
                    Line("MEAS", "  cut in Border w=" + bd.ActualWidth.ToString("0") + ": " + Desc(child) +
                        " x=" + k.Left.ToString("0") + ".." + k.Right.ToString("0") +
                        " box=" + left.ToString("0") + ".." + right.ToString("0") + " up=" + Up(bd, 4));
            }
        }

        // Короткий путь вверх по дереву — чтобы найти место в вёрстке по журналу.
        static string Up(FrameworkElement fe, int levels)
        {
            var sb = new StringBuilder();
            DependencyObject d = VisualTreeHelper.GetParent(fe);
            for (int i = 0; i < levels && d != null; i++)
            {
                var p = d as FrameworkElement;
                if (p != null)
                {
                    if (sb.Length > 0) sb.Append('/');
                    sb.Append(p.GetType().Name);
                    sb.Append('(').Append(p.ActualWidth.ToString("0")).Append(')');
                    string nm = System.Windows.Automation.AutomationProperties.GetName(p);
                    if (!string.IsNullOrEmpty(nm)) { sb.Append('\'').Append(nm).Append('\''); break; }
                }
                d = VisualTreeHelper.GetParent(d);
            }
            return sb.ToString();
        }

        // Прокрутка текущей страницы в конец/начало — чтобы снимок захватил
        // и нижние блоки (компоненты, быстрые действия).
        static void ScrollEnd(MainWindow win)
        {
            var page = FindPage(win);
            if (page == null) return;
            foreach (var sv in Descendants<ScrollViewer>(page)) { sv.ScrollToEnd(); break; }
            Pump(); ForceLayout(win);
        }

        static void ScrollHome(MainWindow win)
        {
            var page = FindPage(win);
            if (page == null) return;
            foreach (var sv in Descendants<ScrollViewer>(page)) { sv.ScrollToHome(); break; }
            Pump(); ForceLayout(win);
        }

        // Вкладки «Проверки соединения» строят разное содержимое — проверяем каждую.
        // Переключить вкладку страницы проверки по подписи кнопки.
        static void ClickTab(MainWindow win, string label)
        {
            var b = FindByName<Button>(win, label);
            if (b == null) { Line("WARN", "tab not found: " + label); return; }
            b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            Pump(); ForceLayout(win);
        }

        // Заполнить открытую страницу проверки правдоподобными результатами.
        static void FillCheck(MainWindow win)
        {
            var cp = FindPage(win) as CheckPage;
            if (cp == null) return;
            cp.FillDemoResults();
            Pump(); ForceLayout(win);
        }

        // Показать полосы прогресса обновления на открытой странице обновлений: в покое
        // они скрыты, и аудит их геометрию не проверял.
        static UpdatesPage FillUpdatesProgress(MainWindow win)
        {
            var up = FindPage(win) as UpdatesPage;
            if (up == null) return null;
            up.ShowDemoProgress();
            Pump(); ForceLayout(win);
            return up;
        }

        static int AuditCheckTabs(MainWindow win, string tag)
        {
            int issues = 0;
            string[] labels = { Loc.T("check.tab.popular"), Loc.T("check.tab.games"),
                                Loc.T("check.tab.strats"), Loc.T("check.tab.targets") };
            foreach (var lbl in labels)
            {
                var b = FindByName<Button>(win, lbl);
                if (b == null) { Line("WARN", "tab not found: " + lbl); continue; }
                b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Pump(); ForceLayout(win);
                issues += LayoutAudit(win, tag + " check/" + lbl);
                // И то же самое с заполненными строками: задержка, подробности и
                // плашка состояния появляются только после проверки, а обрезается
                // вёрстка именно на них.
                var cp = FindPage(win) as CheckPage;
                if (cp != null)
                {
                    cp.FillDemoResults();
                    Pump(); ForceLayout(win);
                    issues += LayoutAudit(win, tag + " check/" + lbl + " filled");
                }
            }
            return issues;
        }

        // Однострочный текст: сравниваем нужную ширину строки с фактически
        // отведённой. Перенос (Wrap) не проверяем — там обрезки не бывает.
        static void AuditText(FrameworkElement scope, Visual skip, ref int clips, ref int trims)
        {
            foreach (var tb in Descendants<TextBlock>(scope))
            {
                if (skip != null && tb.IsDescendantOf(skip)) continue;
                if (tb.Visibility != Visibility.Visible || tb.ActualWidth < 1) continue;
                if (tb.TextWrapping != TextWrapping.NoWrap) continue;
                string s = tb.Text;
                if (string.IsNullOrEmpty(s)) continue;
                double need;
                try
                {
                    // Меряем тем же движком, что и WPF: отдельный TextBlock с теми же
                    // шрифтовыми свойствами и режимом сглаживания, без ограничения ширины.
                    var probe = new TextBlock();
                    probe.Text = s;
                    probe.FontFamily = tb.FontFamily; probe.FontSize = tb.FontSize;
                    probe.FontStyle = tb.FontStyle; probe.FontWeight = tb.FontWeight;
                    probe.FontStretch = tb.FontStretch; probe.TextWrapping = TextWrapping.NoWrap;
                    TextOptions.SetTextFormattingMode(probe, TextOptions.GetTextFormattingMode(tb));
                    probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    need = probe.DesiredSize.Width;
                }
                catch { continue; }
                double have = tb.ActualWidth - tb.Padding.Left - tb.Padding.Right;
                if (need <= have + 2.0) continue;
                if (tb.TextTrimming != TextTrimming.None)
                {
                    trims++;
                    if (trims <= 4) Line("MEAS", "  trimmed: " + Desc(tb));
                    continue;
                }
                clips++;
                if (clips <= 4)
                    Line("MEAS", "  clipped text: " + Desc(tb) +
                        " need=" + need.ToString("0") + " have=" + have.ToString("0"));
            }
        }

        // Выход за левый/правый край страницы. Проверяем только листья дерева:
        // у вложенных элементов нарушение иначе считалось бы многократно.
        static void AuditBounds(FrameworkElement scope, Visual root, Rect refRect, Visual skip, ref int outside)
        {
            foreach (var fe in Descendants<FrameworkElement>(scope))
            {
                if (skip != null && fe.IsDescendantOf(skip)) continue;
                if (fe.Visibility != Visibility.Visible) continue;
                if (fe.ActualWidth < 1 || fe.ActualHeight < 1) continue;
                if (VisualTreeHelper.GetChildrenCount(fe) > 0) continue;
                Rect r;
                try { r = Bounds(fe, root); }
                catch { continue; }
                if (r.Right <= refRect.Right + 0.5 && r.Left >= refRect.Left - 0.5) continue;
                outside++;
                if (outside <= 4)
                    Line("MEAS", "  outside: " + Desc(fe) + " x=" + r.Left.ToString("0") +
                        ".." + r.Right.ToString("0") + " area=" + refRect.Left.ToString("0") +
                        ".." + refRect.Right.ToString("0"));
            }
        }

        static Rect Bounds(FrameworkElement fe, Visual ancestor)
        {
            return fe.TransformToAncestor(ancestor)
                     .TransformBounds(new Rect(0, 0, fe.ActualWidth, fe.ActualHeight));
        }

        // Пересекаются ли ячейки Grid, занимаемые двумя элементами.
        static bool CellsIntersect(FrameworkElement a, FrameworkElement b)
        {
            int ar = Grid.GetRow(a), ac = Grid.GetColumn(a);
            int br = Grid.GetRow(b), bc = Grid.GetColumn(b);
            int ars = Math.Max(1, Grid.GetRowSpan(a)), acs = Math.Max(1, Grid.GetColumnSpan(a));
            int brs = Math.Max(1, Grid.GetRowSpan(b)), bcs = Math.Max(1, Grid.GetColumnSpan(b));
            bool rows = ar < br + brs && br < ar + ars;
            bool cols = ac < bc + bcs && bc < ac + acs;
            return rows && cols;
        }

        // Короткое имя элемента для журнала: текст, имя для читалки или тип.
        static string Desc(FrameworkElement fe)
        {
            string s = null;
            var tb = fe as TextBlock;
            if (tb != null) s = tb.Text;
            if (string.IsNullOrEmpty(s)) s = System.Windows.Automation.AutomationProperties.GetName(fe);
            // У контейнеров своего текста нет — берём подпись изнутри, иначе в
            // журнале останется бесполезное «StackPanel».
            if (string.IsNullOrEmpty(s))
                foreach (var inner in Descendants<TextBlock>(fe))
                    if (!string.IsNullOrEmpty(inner.Text)) { s = inner.Text; break; }
            if (string.IsNullOrEmpty(s))
            {
                // Совсем без подписи (иконки, поля ввода) — перечисляем содержимое,
                // иначе по журналу не найти место в вёрстке.
                var sb = new StringBuilder(fe.GetType().Name + "[");
                int n = 0;
                try { n = VisualTreeHelper.GetChildrenCount(fe); }
                catch { }
                for (int i = 0; i < n && i < 4; i++)
                {
                    if (i > 0) sb.Append(',');
                    var c = VisualTreeHelper.GetChild(fe, i) as FrameworkElement;
                    sb.Append(c == null ? "?" : c.GetType().Name + "(" + c.ActualWidth.ToString("0") + ")");
                }
                return sb.Append(']').ToString();
            }
            s = s.Replace("\r", " ").Replace("\n", " ");
            if (s.Length > 40) s = s.Substring(0, 40) + "…";
            return fe.GetType().Name + "'" + s + "'";
        }

        static T FindByName<T>(DependencyObject root, string name) where T : DependencyObject
        {
            foreach (var el in Descendants<T>(root))
                if (System.Windows.Automation.AutomationProperties.GetName(el) == name) return el;
            return null;
        }
        static Page FindPage(MainWindow win)
        {
            foreach (var cc in Descendants<ContentControl>(win))
                if (cc.Content is Page) return (Page)cc.Content;
            return null;
        }

        static void NavCheck(MainWindow win, string key)
        {
            Try("nav " + key, delegate
            {
                win.Navigate(key);
                Pump();
                ForceLayout(win);
                Assert(win.CurrentPageKey == key, "активен раздел '" + win.CurrentPageKey + "', ожидался '" + key + "'");
                Assert(win.ActualHeight > 0, "окно нулевой высоты после Navigate('" + key + "')");
            });
        }

        static void ThemeSwitch(string tag, ThemeMode m)
        {
            Try("theme " + tag, delegate
            {
                Theme.Apply(m);
                Pump();
                Assert(Theme.Mode == m, "Theme.Mode не переключился на " + m + ", стоит " + Theme.Mode);
                // Кисти объявлены как readonly new SolidColorBrush() и потому ненулевые
                // всегда; проверять надо, что Apply() выставил цвет (по умолчанию он
                // полностью прозрачный).
                Assert(Theme.BrText.Color.A != 0, "тема " + m + " не задала цвет текста");
                Assert(Theme.BrBgDeep.Color.A != 0, "тема " + m + " не задала цвет фона");
            });
        }

        static void LangSwitch(string tag, string lang)
        {
            Try("lang " + tag, delegate
            {
                Loc.SetLang(lang);
                Pump();
                Assert(string.Equals(Loc.Lang, lang, StringComparison.OrdinalIgnoreCase),
                    "Loc.Lang = '" + Loc.Lang + "', ожидался '" + lang + "'");
                // Пустая строка локализации — самый частый тихий регресс: UI остаётся
                // целым, но подписи исчезают.
                Assert(!string.IsNullOrEmpty(Loc.T("common.close")), "Loc.T(common.close) пуст при " + lang);
                Assert(!Loc.T("common.close").StartsWith("common."), "ключ не переведён при " + lang + ": " + Loc.T("common.close"));
            });
        }

        static void ForceLayout(DependencyObject root)
        {
            var win = Window.GetWindow(root as Visual);
            if (win != null)
            {
                win.Measure(new Size(win.Width, win.Height));
                win.Arrange(new Rect(0, 0, win.Width, win.Height));
                win.UpdateLayout();
            }
        }

        static void Try(string what, Action act)
        {
            try { act(); _pass++; }
            catch (Exception ex) { _fail++; Line("FAIL", what + " -> " + ex.GetType().Name + ": " + ex.Message); }
        }

        static void Line(string lvl, string msg) { Log.AppendLine("[" + lvl + "] " + msg); Console.WriteLine("[" + lvl + "] " + msg); }

        // Рендер окна в PNG для визуальной проверки и автогенерации документации.
        static void SavePng(MainWindow win, string fullPath)
        {
            try
            {
                int w = (int)win.ActualWidth, h = (int)win.ActualHeight;
                if (w < 10 || h < 10) return;
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                win.Measure(new Size(w, h));
                win.Arrange(new Rect(0, 0, w, h));
                win.UpdateLayout();
                rtb.Render(win);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                string dir = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (var fs = new FileStream(fullPath, FileMode.Create)) enc.Save(fs);
                Line("SHOT", fullPath);
            }
            catch (Exception ex) { Line("WARN", "shot " + fullPath + ": " + ex.Message); }
        }

        // Все снимки теста — в musor/photo (вне git): рабочие _shot_*.png и
        // кадры документации с той же структурой, что в репозитории (docs/…).
        // Раньше кадры сыпались в корень и _app, а docs правились прямо во
        // время прогона; теперь репо после теста чист. Обновление
        // документации: скопировать musor/photo/docs/. в docs/.
        static string GetPhotoDir()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string root = Core.Root;
            if (string.IsNullOrEmpty(root))
                root = Directory.Exists(Path.Combine(baseDir, "docs"))
                    ? baseDir
                    : Path.GetFullPath(Path.Combine(baseDir, ".."));
            return Path.Combine(root, "musor", "photo");
        }

        static void Shot(MainWindow win, string name)
        {
            string photoDir = GetPhotoDir();

            SavePng(win, Path.Combine(photoDir, "_shot_" + name + ".png"));

            if (name.StartsWith("overview-"))
            {
                SavePng(win, Path.Combine(photoDir, "docs", "themes", name + ".png"));
                if (name == "overview-dark")
                {
                    SavePng(win, Path.Combine(photoDir, "docs", "screenshot.png"));
                    SavePng(win, Path.Combine(photoDir, "docs", "screens", "overview.png"));
                }
            }
            else if (name.StartsWith("section-"))
            {
                string sName = name.Substring("section-".Length);
                SavePng(win, Path.Combine(photoDir, "docs", "screens", sName + ".png"));
            }
            else if (name == "collapsed")
            {
                SavePng(win, Path.Combine(photoDir, "docs", "screens", "collapsed.png"));
            }
        }



        static void Pump()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new Action(delegate { frame.Continue = false; }));
            Dispatcher.PushFrame(frame);
        }

        static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null) yield break;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var c = VisualTreeHelper.GetChild(root, i);
                if (c is T) yield return (T)c;
                foreach (var d in Descendants<T>(c)) yield return d;
            }
        }

        // ================= Юнит-тесты чистой логики =================
        // Никакого UI и сети: версии, парсеры, кодировки, кольцевой буфер.

        // Реестр тем, парсер theme = и миграция конфигов старой сборки.
        static void ThemeTests()
        {
            Try("theme реестр без дублей", delegate
            {
                Assert(Theme.Modes.Length >= 3, "в реестре подозрительно мало тем: " + Theme.Modes.Length);
                for (int i = 0; i < Theme.Modes.Length; i++)
                    for (int j = i + 1; j < Theme.Modes.Length; j++)
                        Assert(Theme.Modes[i] != Theme.Modes[j], "дубль темы в реестре: " + Theme.Name(Theme.Modes[i]));
            });

            Try("theme имя и подпись", delegate
            {
                ThemeMode back;
                foreach (var m in Theme.Modes)
                {
                    string key = "settings.theme." + Theme.Name(m);
                    Assert(Theme.TryParse(Theme.Name(m), out back) && back == m,
                        "имя темы не парсится обратно: " + Theme.Name(m));
                    Assert(string.Equals(Theme.LocKey(m), key, StringComparison.Ordinal),
                        "ключ подписи темы собрался неверно: " + Theme.Name(m));
                    string label = Loc.T(key);
                    Assert(!string.IsNullOrEmpty(label) && !label.StartsWith("settings."),
                        "нет перевода темы " + Theme.Name(m));
                }
            });

            // Удалённые темы не должны «протекать» обратно: ни парсером, ни кнопкой
            // переключения. Иначе пользователь получает мёртвую тему из цикла тем.
            Try("theme удалённые не живые", delegate
            {
                ThemeMode m;
                string[] gone = { "aurora", "peter", "petergriffin", "midnight" };
                foreach (var g in gone)
                {
                    Assert(!Theme.TryParse(g, out m), "удалённая тема всё ещё парсится: " + g);
                    Assert(Theme.IsRemovedTheme(g), "удалённая тема не помечена как мигрирующая: " + g);
                    Assert(!string.Equals(Theme.Name(Theme.RemovedThemeFallback(g)), g, StringComparison.OrdinalIgnoreCase),
                        "фолбэк совпадает с самим удалённым именем: " + g);
                }
                Assert(!Theme.IsRemovedTheme("dark"), "живая тема помечена удалённой");
                Assert(!Theme.IsRemovedTheme("garbage"), "мусор помечен удалённой темой — должен идти по ветке «неизвестное значение»");
            });

            Try("theme цикл = реестр", delegate
            {
                ThemeMode was = Theme.Mode;
                try
                {
                    for (int i = 0; i < Theme.Modes.Length; i++)
                    {
                        Theme.Mode = Theme.Modes[i];
                        ThemeMode next = Theme.NextMode();
                        Assert(next == Theme.Modes[(i + 1) % Theme.Modes.Length],
                            "цикл тем сломан после " + Theme.Name(Theme.Modes[i]) + ": дальше " + Theme.Name(next));
                        Assert(Theme.IndexOf(next) >= 0, "цикл выдал тему вне реестра: " + Theme.Name(next));
                    }
                }
                finally { Theme.Mode = was; }
            });

            Try("strat favorites без дублей", delegate
            {
                string was = Core.Get("favorites", null);
                try
                {
                    Core.Set("favorites", "a.bat|a.bat|b.bat");
                    var list = Core.GetFavoriteStrategies();
                    Assert(list.Count == 2, "дубли избранного не схлопнуты: " + list.Count);
                    int added = Core.AddFavoriteStrategies(new string[] { "b.bat", "c.bat" });
                    Assert(added == 1, "добавлено неверное число избранных: " + added);
                    var after = Core.GetFavoriteStrategies();
                    Assert(after.Count == 3 && after.Contains("c.bat"), "новое избранное не сохранилось");
                    Core.SetFavoriteStrategies(new string[] { "x.bat", "x.bat", " " });
                    var clean = Core.GetFavoriteStrategies();
                    Assert(clean.Count == 1 && clean[0] == "x.bat", "очистка избранного собрала мусор");
                }
                finally
                {
                    if (was == null) Core.Unset("favorites"); else Core.Set("favorites", was);
                    try { Core.SaveConfig(); } catch { }
                }
            });

            Try("fonts: кэш помечает применённый шрифт", delegate
            {
                if (!Core.ConfigureUiFontsFromCache()) return;
                var fam = Core.ResolveUiFontFamily();
                Assert(Core.IsUiFontApplied(fam.Title) || Core.IsUiFontApplied(Core.DefaultFontFamily.Title),
                    "шрифт из кэша подключён, но не помечен применённым");
            });

            // Сквозная миграция: реальный стартовый путь (App.ApplySavedTheme) должен
            // не только включить правильную тему, но и записать её в gui-config.ini.
            // Без записи тема «не сохраняется»: следующее сохранение конфига вернёт
            // мёртвое имя, и пользователь будет терять выбор при каждом обновлении.
            ThemeMigration("aurora", ThemeMode.Dark);
            ThemeMigration("peter", ThemeMode.Light);
            ThemeMigration("midnight", ThemeMode.Dark);
        }

        static void ThemeMigration(string saved, ThemeMode expect)
        {
            Try("theme миграция " + saved, delegate
            {
                ThemeMode was = Theme.Mode;
                string wasSaved = Core.Get("theme", null);
                byte[] fileWas = null;
                try { string path = Core.ConfigFile; if (File.Exists(path)) fileWas = File.ReadAllBytes(path); }
                catch { }
                try
                {
                    Core.Set("theme", saved);
                    App.ApplySavedTheme();
                    Assert(Theme.Mode == expect, "theme = " + saved + " дал «" + Theme.Name(Theme.Mode)
                        + "», ожидался «" + Theme.Name(expect) + "»");
                    string inMem = Core.Get("theme", null);
                    Assert(string.Equals(inMem, Theme.Name(expect), StringComparison.Ordinal),
                        "миграция не обновила тему в памяти: «" + inMem + "»");
                    if (!string.IsNullOrEmpty(Core.Root))
                    {
                        string onDisk = ConfigValueOnDisk("theme");
                        Assert(string.Equals(onDisk, Theme.Name(expect), StringComparison.Ordinal),
                            "в gui-config.ini осталось «theme = " + onDisk + "», ожидалось «" + Theme.Name(expect) + "»");
                    }
                }
                finally
                {
                    if (wasSaved == null) Core.Unset("theme"); else Core.Set("theme", wasSaved);
                    // Файл возвращаем сразу, а не в конце прогона: запуск, убитый
                    // посреди тестов, оставлял в gui-config.ini тестовое значение,
                    // и пользователь терял свою тему.
                    try { if (fileWas != null) File.WriteAllBytes(Core.ConfigFile, fileWas); }
                    catch { }
                    Theme.Apply(was);
                }
            });
        }

        static string ConfigValueOnDisk(string key)
        {
            try
            {
                string path = Core.ConfigFile;
                if (!File.Exists(path)) return null;
                foreach (var l in File.ReadAllLines(path))
                {
                    string s = l.Trim();
                    int eq = s.IndexOf('=');
                    if (eq <= 0) continue;
                    if (string.Equals(s.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        return s.Substring(eq + 1).Trim();
                }
            }
            catch { }
            return null;
        }

        static void PureTests()
        {
            // --- Версии ---
            Try("normver v5.2", delegate { Assert(SettingsPage.NormVer("v5.2") == "5.2"); });
            Try("normver 4comp", delegate { Assert(SettingsPage.NormVer("1.10.1.0") == "1.10.1"); });
            Try("cmp 5.10>5.9", delegate { Assert(SettingsPage.CompareVersions("5.10", "5.9") > 0); });
            Try("cmp eq short", delegate { Assert(SettingsPage.CompareVersions("5.2", "5.2.0") == 0); });
            Try("cmp eq exact", delegate { Assert(SettingsPage.CompareVersions("1.10.1", "1.10.1") == 0); });
            Try("cmp 10>9", delegate { Assert(SettingsPage.CompareVersions("10.0", "9.9") > 0); });
            Try("cmp empty<", delegate { Assert(SettingsPage.CompareVersions("", "1.0") < 0); });

            // --- Естественная сортировка стратегий ---
            Try("natkey pad", delegate { Assert(Core.NaturalKey("a2bat") == "a00000002bat"); });

            // --- Темы: реестр, парсер theme = и миграция удалённых имён ---
            ThemeTests();

            // --- История релизов: разбор массива ответов GitHub ---
            Try("releases parser", delegate
            {
                string relJson = "[{\"tag_name\":\"v1.2.3\",\"published_at\":\"2026-01-02T03:04:05Z\",\"body\":\"line1\\n- item\"," +
                              "\"assets\":[{\"name\":\"asset.zip\",\"uploader\":{\"login\":\"bot\"}}],\"author\":{\"login\":\"Flowseal\"}}," +
                              "{\"tag_name\":\"v1.2.2\",\"body\":\"second\",\"assets\":[]}]";
                var rel = Endpoints.Releases(relJson);
                Assert(rel.Count == 2, "релизов разобрано: " + rel.Count);
                Assert(rel[0].Tag == "v1.2.3", "tag_name верхнего уровня не прочитан: " + rel[0].Tag);
                Assert(rel[0].Notes != null && rel[0].Notes.Contains("line1"), "body не прочитан: " + rel[0].Notes);
                Assert(rel[1].Tag == "v1.2.2", "второй релиз потерян: " + rel[1].Tag);
                Assert(!string.IsNullOrEmpty(rel[0].Date), "published_at не прочитан");
                Assert(Endpoints.ReleaseDate(rel[0].Date).Length == 10,
                    "дата не в формате dd.MM.yyyy: " + Endpoints.ReleaseDate(rel[0].Date));
            });
            Try("releases parser чужой формат", delegate
            {
                Assert(Endpoints.Releases("{\"tag_name\":\"v1\"}").Count == 0, "объект вместо массива распарсился");
                Assert(Endpoints.Releases(null).Count == 0, "null уронил парсер");
                Assert(Endpoints.Releases("[{\"name\":\"x\"}]").Count == 0, "релиз без tag_name попал в список");
            });

            // --- TG-прокси: разбор config.json и сборка tg:// ссылки ---
            Try("tg config parse", delegate
            {
                string cfgJson = "{\"port\": 1443, \"host\": \"127.0.0.1\", \"secret\": \"0123456789ABCDEF0123456789abcdef\"}";
                TgProxySettings s;
                string err;
                Assert(Core.ParseTgProxySettings(cfgJson, out s, out err), "config не разобран: " + err);
                Assert(s.Port == 1443 && s.Host == "127.0.0.1", "host/port прочитаны неверно");
                Assert(s.Secret == "0123456789abcdef0123456789abcdef", "secret не приведён к нижнему регистру");
                Assert(s.Link == "tg://proxy?server=127.0.0.1&port=1443&secret=dd0123456789abcdef0123456789abcdef",
                    "ссылка собрана неверно: " + s.Link);
            });
            Try("tg link 0.0.0.0", delegate
            {
                var s = new TgProxySettings { Host = "0.0.0.0", Port = 1443, Secret = new string('a', 32) };
                Assert(s.LinkHost == "127.0.0.1", "0.0.0.0 не заменён на loopback: " + s.LinkHost);
                Assert(s.Endpoint == "127.0.0.1:1443", "endpoint неверный: " + s.Endpoint);
                Assert(s.Link.StartsWith("tg://proxy?server=127.0.0.1&port=1443&secret=dd", StringComparison.Ordinal),
                    "ссылка для 0.0.0.0 неверна: " + s.Link);
            });
            Try("tg config rejects bad", delegate
            {
                TgProxySettings s;
                string err;
                Assert(!Core.ParseTgProxySettings("{\"port\":0,\"secret\":\"" + new string('a', 32) + "\"}", out s, out err),
                    "порт 0 принят");
                Assert(!Core.ParseTgProxySettings("{\"port\":1443,\"secret\":\"xyz\"}", out s, out err),
                    "короткий secret принят");
                Assert(!Core.ParseTgProxySettings(null, out s, out err), "null принят");
            });

            // --- Ряды кнопок: структура набора и поведение при нехватке места ---
            Try("button rows group", delegate
            {
                var group = Ctl.ButtonRows(
                    new FrameworkElement[] { new Button(), new Button(), new Button(), new Button() },
                    new FrameworkElement[] { new Button(), new Button(), new Button() });
                var host = group as StackPanel;
                Assert(host != null && host.Children.Count == 2, "ButtonRows не вернул два ряда");
                var r1 = host.Children[0] as CellRow;
                var r2 = host.Children[1] as CellRow;
                Assert(r1 != null && r2 != null, "ряды собраны не CellRow");
                Assert(r1.ForceCols == 4 && r2.ForceCols == 3,
                    "ряд кнопок не растянут на блок равными колонками: " + r1.ForceCols + " и " + r2.ForceCols);
                // UniformCols: ряд из трёх кнопок обязан иметь ту же ширину ячейки,
                // что и ряд из четырёх над ним, иначе кнопки «разными по размеру».
                Assert(r1.UniformCols == 4 && r2.UniformCols == 4,
                    "UniformCols не выровнен по длинному ряду: " + r1.UniformCols + " и " + r2.UniformCols);
                var avail = new Size(812, double.PositiveInfinity);
                r1.Measure(avail); r2.Measure(avail);
                r1.Arrange(new Rect(0, 0, avail.Width, 200));
                r2.Arrange(new Rect(0, 0, avail.Width, 200));
                double w1 = ((FrameworkElement)r1.Children[0]).ActualWidth;
                Assert(w1 > 100, "кнопка не получила ширину: " + w1);
                for (int i = 0; i < r2.Children.Count; i++)
                    Assert(Math.Abs(((FrameworkElement)r2.Children[i]).ActualWidth - w1) < 0.6,
                        "ряд из трёх кнопок шире ряда из четырёх: " + ((FrameworkElement)r2.Children[i]).ActualWidth + " против " + w1);
            });

            // --- Полоса табов: пять равных ячеек закрывают ширину целиком ---
            Try("cell row tiles full width", delegate
            {
                var row = new CellRow { Gap = 6, EvenRows = false, MinCell = 0, ForceCols = 5 };
                for (int i = 0; i < 5; i++) row.Children.Add(new Button());
                double[] widths = { 600, 812, 1100 };
                foreach (double w in widths)
                {
                    row.Measure(new Size(w, double.PositiveInfinity));
                    row.Arrange(new Rect(0, 0, w, 40));
                    double cw = ((FrameworkElement)row.Children[0]).ActualWidth;
                    for (int i = 1; i < 5; i++)
                        Assert(Math.Abs(((FrameworkElement)row.Children[i]).ActualWidth - cw) < 0.6,
                            "ячейки табов разные по ширине при " + w);
                    double right = cw * 5 + 6 * 4;
                    Assert(Math.Abs(right - w) < 1.5,
                        "ряд не доходит до правого края: " + right + " при " + w);
                }
            });

            // --- Justify: компактные элементы распределены по всей ширине ---
            Try("cell row justify fills width", delegate
            {
                string[] texts = { "A", "BB", "CCC", "DDDD", "EEEEEE" };
                var row = new CellRow { Gap = 6, EvenRows = false, MinCell = 0, ForceCols = 5, Justify = true };
                for (int i = 0; i < texts.Length; i++)
                    row.Children.Add(new TextBlock { Text = texts[i], FontSize = 16 });
                double w = 600;
                row.Measure(new Size(w, double.PositiveInfinity));
                row.Arrange(new Rect(0, 0, w, 30));

                double sum = 0;
                var xs = new double[texts.Length];
                var ws = new double[texts.Length];
                for (int i = 0; i < texts.Length; i++)
                {
                    var ch = (FrameworkElement)row.Children[i];
                    xs[i] = ch.TranslatePoint(new Point(0, 0), row).X;
                    ws[i] = ch.ActualWidth;
                    sum += ws[i];
                    Assert(ws[i] < 120, "компактный элемент растянут на колонку: " + ws[i]);
                }
                Assert(Math.Abs(xs[0]) < 0.6, "первый элемент не прижат к левому краю: " + xs[0]);
                Assert(Math.Abs(xs[texts.Length - 1] + ws[texts.Length - 1] - w) < 0.6,
                    "ряд не доходит до правого края: " + (xs[texts.Length - 1] + ws[texts.Length - 1]) + " при " + w);
                double gap = (w - sum) / (texts.Length - 1);
                Assert(gap >= 6 - 0.01, "зазор меньше минимального: " + gap);
                for (int i = 1; i < texts.Length; i++)
                    Assert(Math.Abs((xs[i] - xs[i - 1] - ws[i - 1]) - gap) < 0.6,
                        "зазоры между элементами равными не вышли: " + (xs[i] - xs[i - 1] - ws[i - 1]) + " против " + gap);
            });

            // --- Фокус-кольцо не должно двигать содержимое ---
            Try("focus ring keeps layout", delegate
            {
                var bd = new Border { BorderThickness = new Thickness(1) };
                Ctl.SetFocusRing(bd, true);
                Assert(bd.BorderThickness == new Thickness(1),
                    "фокус меняет толщину рамки (" + bd.BorderThickness + ") — содержимое смещается на пиксель");
                Assert(bd.Effect != null, "фокус не показан кольцом");
                Ctl.SetFocusRing(bd, false);
                Assert(bd.Effect == null, "кольцо фокуса не снято");
            });

            // --- Закрытый ComboBox не должен съедать колесо ---
            Try("closed combo does not eat wheel", delegate
            {
                var cb = new System.Windows.Controls.ComboBox();
                Assert(!SmoothScrollViewer.IsInsideDropdown(cb, cb),
                    "закрытый ComboBox перехватывает колесо — страница залипает при прокрутке");
            });

            // --- Разделение заливки не должно сжимать её в полоску ---
            Try("split surface fills hit area", delegate
            {
                var s = new Border
                {
                    Background = Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock { Text = "github.com" }
                };
                var root = Ctl.SplitSurface(s);
                Assert(s.HorizontalAlignment == HorizontalAlignment.Stretch,
                    "заливка не растянулась — от пилюли останется тонкая полоска");
                Assert(root.Background != null,
                    "у кнопки нет hit-test фона — клики по тексту не дойдут");
                Assert(root.HorizontalAlignment == HorizontalAlignment.Left,
                    "кнопка потеряла исходное выравнивание");
                Assert(root.Children.Count == 2, "заливка и содержимое не разделены");
            });

            // --- Неон кнопки не должен ложиться на содержимое с подписью ---
            Try("button glow stays off content", delegate
            {
                var btn = Ctl.Button("Тест", null, 1);
                var surface = Ctl.Surface(btn);
                var host = Ctl.ContentHost(btn);
                Assert(surface != null, "у кнопки нет заливки");
                Assert(host != null && host.Child != null, "контент не отделён от заливки");
                Assert(surface.Child == null, "подпись осталась внутри заливки — эффект размоет текст");
                Ctl.SetGlow(surface, true, false);
                Assert(surface.Effect != null, "подсветка не включилась");
                Ctl.SetGlow(surface, false, false);
                Assert(surface.Effect == null, "подсветка не снялась");
            });

            Try("button rows span block", delegate
            {
                var a = Ctl.Button("Пауза", Icons.Pause, 1);
                var b2 = Ctl.Button("Копировать диагностику", Icons.Shield, 1);
                var row = Ctl.ButtonRow(a, b2);
                row.Measure(new Size(900, double.PositiveInfinity));
                row.Arrange(new Rect(0, 0, 900, 40));
                // Ряд = ширина блока: равные колонки, край в край, одна линия.
                Assert(Math.Abs(a.ActualWidth - b2.ActualWidth) < 0.6,
                    "кнопки в ряду не равны: " + a.ActualWidth + " / " + b2.ActualHeight);
                Assert(Math.Abs((a.ActualWidth + b2.ActualWidth + row.Gap) - 900) < 1.5,
                    "ряд не растянут на весь блок: " + (a.ActualWidth + b2.ActualWidth + row.Gap));
                Assert(Math.Abs(a.ActualHeight - b2.ActualHeight) < 0.6,
                    "высота кнопок в одном ряду разная: " + a.ActualHeight + " / " + b2.ActualHeight);
                double ax = a.TranslatePoint(new Point(0, 0), row).X;
                double bx = b2.TranslatePoint(new Point(0, 0), row).X;
                Assert(Math.Abs(ax) < 0.6, "первая кнопка не у левого края ряда: " + ax);
                Assert(Math.Abs((bx + b2.ActualWidth) - 900) < 1.5,
                    "правый край ряда не совпадает с краем блока: " + (bx + b2.ActualWidth));
            });

            Try("release notes markdown", delegate
            {
                string md = "## What's new\n\n- **Fixed** `crash` on [Discord](https://discord.com)\n" +
                            "> note\n![badge](https://img/shield.png)\n**bold** and *italic* and ~~strike~~\n";
                string outp = Endpoints.FormatReleaseNotes(md);
                Assert(!outp.Contains("**"), "жирный не вычищен: " + outp);
                Assert(!outp.Contains("`"), "inline-код не вычищен: " + outp);
                Assert(!outp.Contains("]("), "ссылка не вычищена: " + outp);
                Assert(!outp.Contains("shield.png"), "картинка-бейдж не удалена: " + outp);
                Assert(!outp.Contains("##"), "заголовок не вычищен: " + outp);
                Assert(!outp.Contains("~~"), "зачёркивание не вычищено: " + outp);
                Assert(outp.Contains("• Fixed crash on Discord"), "строка списка собралась неверно: " + outp);
                Assert(outp.Contains("note"), "цитата потерялась: " + outp);
                Assert(outp.Contains("bold and italic and strike"), "инлайн-маркеры съели текст: " + outp);
            });
            Try("release notes пустые строки", delegate
            {
                string spaced = Endpoints.FormatReleaseNotes("a\n\n\n\nb");
                Assert(spaced.Split('\n').Length == 3, "пустые строки не схлопнуты в одну: [" + spaced + "]");
            });

            // --- Сборка аргументов winws из .bat ---
            BuildArgsTests();

            // --- JSON / release notes ---
            string json = "{\"tag_name\":\"v1.2.3\",\"body\":\"line1\\n- item\"}";
            Try("jsonfield tag", delegate { Assert(Endpoints.JsonField(json, "tag_name") == "v1.2.3"); });
            Try("jsonfield body nl", delegate { Assert(Endpoints.JsonField(json, "body").Contains("\n- item")); });
            Try("release notes bullet", delegate
            {
                string notes = Endpoints.ReleaseNotes(json);
                Assert(notes != null && notes.Contains("• item"));
            });

            // --- Починка двойного кодирования UTF-8 → cp1252 ---
            Try("mojibake repair", delegate
            {
                string orig = "Привет Ёё";
                string moji = Encoding.GetEncoding(1252).GetString(Encoding.UTF8.GetBytes(orig));
                Assert(Endpoints.RepairUtf8Mojibake(moji) == orig);
            });
            Try("mojibake passthrough", delegate
            {
                string plain = "Café normal text";
                Assert(Endpoints.RepairUtf8Mojibake(plain) == plain);
            });

            // --- Маскирование диагностики ---
            Try("mask username/ip", delegate
            {
                string m = Core.Mask("path " + Environment.UserName + " ip 192.168.1.55 pub 8.8.8.8");
                Assert(m.Contains("USER") && !m.Contains(Environment.UserName));
                Assert(m.Contains("x.x.x.x") && m.Contains("8.8.8.8"));
            });

            // --- Кольцевой буфер вывода winws ---
            Try("winws ring buffer", delegate
            {
                // 6e: буфер общий и живой — из него читают «Вывод winws» и
                // Core.Diagnostics(). Снимаем состояние и возвращаем после теста.
                string was = Core.WinwsLogAll();
                try
                {
                    Core.WinwsLogClear();
                    for (int i = 0; i < 450; i++) Core.WinwsLogAppend("line" + i);
                    string tail = Core.WinwsLogTail(400);
                    Assert(tail.StartsWith("..."));
                    Assert(HasLine(tail, "line449"));
                    // Прежняя проверка !tail.Contains("line49\n") была тавтологией,
                    // WinwsLogTail склеивает строки через AppendLine (\r\n), подстрока не
                    // появлялась ни при каком состоянии буфера. Плюс "line49": префикс
                    // "line490", так что сравнивать можно только целыми строками.
                    Assert(!HasLine(tail, "line49"));
                    Assert(HasLine(tail, "line50"));
                    Core.WinwsLogClear();
                    Assert(Core.WinwsLogAll().Length == 0);
                }
                finally
                {
                    Core.WinwsLogClear();
                    string[] lines = LogLines(was);
                    // Первая строка "..." — маркер усечения от WinwsLogTail, а не вывод winws.
                    int from = (lines.Length > 0 && lines[0] == "...") ? 1 : 0;
                    for (int i = from; i < lines.Length; i++) Core.WinwsLogAppend(lines[i]);
                }
            });

            // --- Разбор .sha256 файла установщика ---
            Try("parse sha256 file", delegate
            {
                const string hex = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
                Assert(Core.ParseSha256File(hex + "  Lantern-Setup.exe\r\n") == hex);
                Assert(Core.ParseSha256File("no hash here") == null);
            });

            // --- Ассеты релиза: имя релиза и вложенный uploader не должны
            // подменять имя файла (из-за этого самообновление скачивало архив
            // и всегда падало на проверке хеша). ---
            Try("release assets pick", delegate
            {
                const string rel = "{\"tag_name\":\"7.1\",\"name\":\"Lantern v7.1\",\"assets\":[" +
                    "{\"name\":\"Lantern-Setup.exe\",\"uploader\":{\"login\":\"krinix1337\",\"name\":\"nope\"}," +
                    "\"browser_download_url\":\"https://x/Lantern-Setup.exe\"}," +
                    "{\"name\":\"Lantern-Setup.exe.sha256\",\"uploader\":{\"login\":\"krinix1337\"}," +
                    "\"browser_download_url\":\"https://x/Lantern-Setup.exe.sha256\"}]}";
                var list = Endpoints.ReleaseAssets(rel);
                Assert(list.Count == 2);
                Assert(list[0].Key == "Lantern-Setup.exe");
                Assert(Endpoints.ReleaseAssetUrl(rel, "Lantern-Setup.exe") == "https://x/Lantern-Setup.exe");
                Assert(Endpoints.ReleaseAssetUrl(rel, "Lantern-Setup.exe.sha256") == "https://x/Lantern-Setup.exe.sha256");
                Assert(Endpoints.ReleaseAssetUrl(rel, "Absent.zip") == null);
            });
            Try("release assets empty", delegate
            {
                Assert(Endpoints.ReleaseAssets("{\"tag_name\":\"1.0\"}").Count == 0);
                Assert(Endpoints.ReleaseAssetUrl(null, "x") == null);
            });

            // --- Экранирование аргументов для sc create: путь установки по
            // умолчанию содержит пробел, без кавычек служба ставилась битой. ---
            Try("arg quoting", delegate
            {
                Assert(Core.Arg("simple") == "\"simple\"");
                Assert(Core.Arg("C:\\Program Files\\Lantern\\zapret") == "\"C:\\Program Files\\Lantern\\zapret\"");
                Assert(Core.Arg("\"C:\\a b\\winws.exe\" --wf-tcp=80") ==
                    "\"\\\"C:\\a b\\winws.exe\\\" --wf-tcp=80\"");
                Assert(Core.Arg("ends\\") == "\"ends\\\\\"");
                Assert(Core.Arg(null) == "\"\"");
            });


            // --- Режимы ipset на изолированном корне (каталог exe selftest) ---
            IpsetModeTests();

            // --- Пробы watchdog: ровно ключевые эндпоинты ---
            Try("quick probes count", delegate { Assert(Core.QuickProbes().Count == 3); });

            // --- Человекочитаемые размеры ---
            // Прежняя проверка (Length > 0) проходила даже на заглушке,
            // возвращающей любую непустую строку.
            Try("humansize units", delegate
            {
                // Разделитель дробной части зависит от локали — сравниваем с тем же форматом.
                Assert(Core.HumanSize(-1) == "?");
                Assert(NumOf(Core.HumanSize(0)) == "0");
                Assert(NumOf(Core.HumanSize(1023)) == "1023");
                Assert(NumOf(Core.HumanSize(2048)) == (2.0).ToString("0.0"));
                Assert(NumOf(Core.HumanSize(5L * 1024 * 1024 * 1024)) == (5.0).ToString("0.0"));
                // Единицы обязаны расти, иначе получилось бы «1024 КБ» вместо «1,0 МБ».
                Assert(UnitOf(Core.HumanSize(0)) != UnitOf(Core.HumanSize(1024)));
                Assert(UnitOf(Core.HumanSize(1024)) != UnitOf(Core.HumanSize(1024 * 1024)));
                Assert(UnitOf(Core.HumanSize(1024 * 1024)) != UnitOf(Core.HumanSize(1024L * 1024 * 1024)));
                // Скорость — тот же размер плюс суффикс «/с».
                Assert(Core.HumanSpeed(2048).StartsWith(Core.HumanSize(2048), StringComparison.Ordinal));
                Assert(Core.HumanSpeed(2048).EndsWith(Loc.T("unit.perSec"), StringComparison.Ordinal));
            });
        }

        // Удаление временной папки с повторами: сразу после теста файл иногда
        // ещё держит сам код (запись конфига/списков), однократный Delete
        // молча падал и в %TEMP% оставались папки lantern_ut_*.
        static void Nuke(string dir)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (!Directory.Exists(dir)) return;
                    foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                    Directory.Delete(dir, true);
                    return;
                }
                catch { System.Threading.Thread.Sleep(120); }
            }
        }

        static void BuildArgsTests()
        {
            string root = Path.Combine(Path.GetTempPath(), "lantern_ut_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            // Исходный корень обязательно вернуть: без этого весь дальнейший прогон
            // (и все скриншоты) шёл с Root, указывающим на удалённую temp-папку —
            // интерфейс выглядел как «компоненты не установлены».
            string rootWas = Core.Root;
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "bin"));
                string bat =
                    "@echo off\r\n" +
                    "start \"z\" /min \"%BIN%winws.exe\" --wf-tcp=443,%GameFilterTCP% ^\r\n" +
                    "--filter-udp=443 --hostlist=\"%LISTS%list.txt\" --dpi-desync-fake-tls=^! --dpi-desync=fake --new ^\r\n" +
                    "--filter-tcp=%GameFilterUDP% --ipset=\"%LISTS%ipset-all.txt\" --dpi-desync=multisplit\r\n";
                File.WriteAllText(Path.Combine(root, "t.bat"), bat);

                Core.SetRoot(root);
                bool ipsetWas = Core.IpsetEnabled;

                Core.SetBool("ipset_enabled", true); // без записи конфига на диск — только словарь
                string args = Core.BuildArgs("t.bat");
                Try("buildargs no caret", delegate { Assert(args.IndexOf('^') < 0); });
                // Путь к winws в аргументы не попадает (префикс строки до winws.exe
                // отбрасывается), но плейсхолдеры %LISTS% обязаны подставиться.
                Try("buildargs lists subst", delegate { Assert(args.Contains(Path.Combine(root, "lists")) && !args.Contains("%LISTS%") && !args.Contains("%BIN%")); });
                Try("buildargs gamefilter off", delegate { Assert(args.Contains("--wf-tcp=443,12")); });
                Try("buildargs unescape bang", delegate { Assert(args.Contains("fake-tls=!")); });
                Try("buildargs keeps ipset group", delegate { Assert(args.Contains("--ipset=")); });

                Core.SetBool("ipset_enabled", false);
                string argsNoIpset = Core.BuildArgs("t.bat");
                Try("buildargs drops ipset groups", delegate
                {
                    Assert(!argsNoIpset.Contains("--ipset=") && argsNoIpset.Contains("--dpi-desync=fake"));
                });
                Core.SetBool("ipset_enabled", ipsetWas);

                // Тестирование многострочного game_filter.enabled
                Directory.CreateDirectory(Path.Combine(root, "utils"));
                string flagFile = Path.Combine(root, "utils", "game_filter.enabled");

                // 1) mode=off с кастомными портами не должен возвращать udp
                File.WriteAllText(flagFile, "mode=off\r\ntcp=1024-1934,1936-65535\r\nudp=1024-50000\r\n");
                Try("gamefilter mode off with custom ports", delegate
                {
                    Assert(Core.GameMode == "off");
                    Assert(Core.GameFilterTcpPorts == "1024-1934,1936-65535");
                    Assert(Core.GameFilterUdpPorts == "1024-50000");
                    string a = Core.BuildArgs("t.bat");
                    Assert(a.Contains("--wf-tcp=443,12"));
                    Assert(a.Contains("--filter-tcp=12"));
                });

                // 2) Изменение портов при mode=off не должно включать фильтр
                Core.GameFilterTcpPorts = "1024-1934,1936-60000";
                Try("gamefilter preserve mode off on port edit", delegate
                {
                    Assert(Core.GameMode == "off");
                    Assert(Core.GameFilterTcpPorts == "1024-1934,1936-60000");
                });

                // 3) mode=all подставляет кастомные порты в параметры запуска
                Core.GameMode = "all";
                Try("gamefilter mode all custom ports", delegate
                {
                    Assert(Core.GameMode == "all");
                    string a = Core.BuildArgs("t.bat");
                    Assert(a.Contains("--wf-tcp=443,1024-1934,1936-60000"));
                    Assert(a.Contains("--filter-tcp=1024-50000"));
                });

                // 4) Сброс в исходное состояние
                Core.GameMode = "off";
                Core.GameFilterTcpPorts = "1024-65535";
                Core.GameFilterUdpPorts = "1024-65535";

                // 5) голый %GameFilter% (без суффикса TCP/UDP). В эталонных
                // .bat он стоит в режиме all и обязан раскрываться в TCP-порты:
                // прежний код подставлял туда udp, то есть фильтр заводился не на то.
                string batG = Path.Combine(root, "t2.bat");
                File.WriteAllText(batG,
                    "@echo off\r\n" +
                    "start \"z\" /min \"%BIN%winws.exe\" --wf-tcp=%GameFilter% --wf-udp=%GameFilter%\r\n");
                string tcpPorts = "1111-2222", udpPorts = "3333-4444";
                Core.GameFilterTcpPorts = tcpPorts;
                Core.GameFilterUdpPorts = udpPorts;
                // В off/tcp-режимах %GameFilter% обязан раскрываться в TCP-значения
                // GameValues(): off → служебные «12», all/tcp → кастомные TCP-порты.
                foreach (var kv in new string[][] {
                    new string[] { "off", "12" },
                    new string[] { "all", tcpPorts },
                    new string[] { "tcp", tcpPorts } })
                {
                    string mode = kv[0], expect = kv[1];
                    Core.GameMode = mode;
                    string a = Core.BuildArgs("t2.bat");
                    Try("gamefilter bare subst mode=" + mode, delegate
                    {
                        Assert(!a.Contains("%GameFilter"), "остался неразвёрнутый %GameFilter%: " + a);
                        Assert(a.Contains("--wf-tcp=" + expect) && a.Contains("--wf-udp=" + expect),
                            "в режиме " + mode + " %GameFilter% обязан раскрываться в '" + expect + "': " + a);
                    });
                }
                Core.GameMode = "udp";
                Try("gamefilter bare subst mode=udp", delegate
                {
                    string a = Core.BuildArgs("t2.bat");
                    Assert(a.Contains("--wf-tcp=" + udpPorts), "в режиме udp %GameFilter% обязан раскрываться в UDP-порты: " + a);
                });

                // 6) плейсхолдер сразу перед кавычкой. Core.Bin/Core.Lists
                // заканчиваются на бэкслеш, и наивная подстановка даёт lists\", где
                // \" съедает кавычку при разборе argv. Форма обязана стоять внутри
                // самой команды winws: парсер берёт строки, начиная с той, где
                // встречается winws.exe (Core.Strategies.cs:315).
                string batQ = Path.Combine(root, "t3.bat");
                File.WriteAllText(batQ,
                    "@echo off\r\n" +
                    "start \"z\" /min \"%BIN%winws.exe\" --wf-tcp=443 ^\r\n" +
                    "--hostlist=\"%LISTS%\"aux.txt\r\n");
                string aq = Core.BuildArgs("t3.bat");
                Try("buildargs quoted BIN/LISTS subst", delegate
                {
                    Assert(!aq.Contains("%BIN%") && !aq.Contains("%LISTS%"), "плейсхолдеры не развернулись: " + aq);
                    Assert(aq.IndexOf("\\\"", StringComparison.Ordinal) < 0,
                        "путь закончился на бэкслеш перед кавычкой (\\\" съедает кавычку): " + aq);
                });

                Core.GameMode = "off";
                Core.GameFilterTcpPorts = "1024-65535";
                Core.GameFilterUdpPorts = "1024-65535";
            }
            finally
            {
                Core.SetRoot(rootWas);
                Nuke(root);
            }
        }

        static void IpsetModeTests()
        {
            // Строго во временном корне: тест перезаписывает lists\ipset-all.txt,
            // и на реальной установке это уничтожило бы список пользователя.
            string root = Path.Combine(Path.GetTempPath(), "lantern_ut_" + Guid.NewGuid().ToString("N"));
            string rootWas = Core.Root;
            Core.SetRoot(root);
            try
            {
                IpsetModeChecks();
            }
            finally
            {
                Core.SetRoot(rootWas);
                Nuke(root);
            }
        }

        static void IpsetModeChecks()
        {
            string listsDir = Core.Lists;
            Directory.CreateDirectory(listsDir);
            string file = Path.Combine(listsDir, "ipset-all.txt");
            File.WriteAllText(file, "203.0.113.7/32\r\n198.51.100.9/32\r\n");

            Try("ipset loaded detect", delegate { Assert(Core.IpsetStatus() == "loaded"); });

            Core.SetIpsetMode("none");
            Try("ipset none sentinel", delegate
            {
                Assert(Core.IpsetStatus() == "none" && File.Exists(file + ".backup"));
            });

            Core.SetIpsetMode("loaded");
            Try("ipset restore from backup", delegate
            {
                string content = File.ReadAllText(file);
                Assert(Core.IpsetStatus() == "loaded" && content.Contains("198.51.100.9/32") && !File.Exists(file + ".backup"));
            });

            Core.SetIpsetMode("any");
            Try("ipset any empty", delegate { Assert(Core.IpsetStatus() == "any"); });
            Core.SetIpsetMode("loaded"); // вернём загруженный список как исходное состояние
        }

        static void Assert(bool cond)
        {
            if (!cond) throw new Exception("assertion failed");
        }

        // Тот же_assert_ с расшифровкой: без текста провала Differ-тесты вроде
        // подстановки аргументов winws приходится дебагать вслепую.
        static void Assert(bool cond, string message)
        {
            if (!cond) throw new Exception("assertion failed: " + message);
        }

        // Строки текста, разделённого AppendLine (\r\n).
        static string[] LogLines(string text)
        {
            if (text == null) return new string[0];
            return text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Сравнение целой строкой: подстрока "line49" есть и в "line490".
        static bool HasLine(string text, string line)
        {
            foreach (var s in LogLines(text)) if (s == line) return true;
            return false;
        }

        // HumanSize возвращает "<число> <единица>"; единицы локализованы, поэтому
        // проверяем только числовую часть и то, что единицы различаются.
        static string NumOf(string s) { int i = s.IndexOf(' '); return i < 0 ? s : s.Substring(0, i); }
        static string UnitOf(string s) { int i = s.IndexOf(' '); return i < 0 ? "" : s.Substring(i + 1); }
    }
}
#endif
