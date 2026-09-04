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
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_selftest.log");

            // В SELFTEST-режиме глобальный обработчик из App.Main не подключается
            // (Main выходит раньше) — любое исключение в колбэке диспатчера убивает
            // процесс без лога. Ловим сами.
            app.DispatcherUnhandledException += delegate(object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                Line("DISPATCHER", e.Exception.ToString());
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { File.WriteAllText(logPath + ".crash", e.ExceptionObject.ToString()); } catch { }
            };

            app.Startup += delegate
            {
                try { Execute(); }
                catch (Exception ex) { Line("FATAL", ex.ToString()); }
                finally
                {
                    File.WriteAllText(logPath, "PASS=" + _pass + " FAIL=" + _fail + "\n" + Log.ToString());
                    app.Shutdown();
                }
            };
            app.Run();
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

            // 0) Чистая логика без UI — версии, парсеры, кодировки, буферы.
            PureTests();

            var win = new MainWindow();
            win.Width = 1200; win.Height = 800;
            win.WindowStartupLocation = WindowStartupLocation.Manual;
            win.Left = -4000; win.Top = -4000;      // за пределами экрана
            win.ShowInTaskbar = false;
            win.Show();
            Pump();

            string[] pages = { "overview", "strategies", "check", "service", "filters", "settings", "log", "about" };

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
            
            // 6 тем оформления
            NavCheck(win, "overview");
            Theme.Apply(ThemeMode.Dark); Pump(); ForceLayout(win); Shot(win, "overview-dark");
            Theme.Apply(ThemeMode.Amoled); Pump(); ForceLayout(win); Shot(win, "overview-amoled");
            Theme.Apply(ThemeMode.Light); Pump(); ForceLayout(win); Shot(win, "overview-light");
            Theme.Apply(ThemeMode.Aurora); Pump(); ForceLayout(win); Shot(win, "overview-aurora");
            Theme.Apply(ThemeMode.Sunset); Pump(); ForceLayout(win); Shot(win, "overview-sunset");
            Core.SetBool("peter_backdrop", true);
            Theme.Apply(ThemeMode.Peter); Pump(); ForceLayout(win); Shot(win, "overview-peter");
            
            // Все разделы приложения в единой фирменной тёмной теме (Dark)
            Theme.Apply(ThemeMode.Dark); Pump(); ForceLayout(win);
            NavCheck(win, "overview"); ForceLayout(win); Shot(win, "section-overview");
            NavCheck(win, "strategies"); ForceLayout(win); Shot(win, "section-strategies");
            NavCheck(win, "check"); ForceLayout(win); Shot(win, "section-check");
            NavCheck(win, "service"); ForceLayout(win); Shot(win, "section-service");
            NavCheck(win, "filters"); ForceLayout(win); Shot(win, "section-filters");
            NavCheck(win, "settings"); ForceLayout(win); Shot(win, "section-settings");
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
            foreach (var w in new double[] { 1120, 1000, 1300, 1600 })            {
                Try("resize " + w, delegate { win.Width = w; Pump(); NavCheck(win, "strategies"); ForceLayout(win); MeasureGrid(win, w); });
            }

            // 6b) Геометрический аудит каждой страницы на граничных ширинах и в
            //     двух языках: в английском подписи длиннее, и если что-то
            //     налезает друг на друга — вылезет именно там.
            int layoutIssues = 0;
            foreach (var lang in new string[] { "ru", "en" })
            {
                Loc.SetLang(lang); Pump();
                foreach (var w in new double[] { 1000, 1120, 1600 })
                {
                    win.Width = w; Pump(); ForceLayout(win);
                    foreach (var key in pages)
                    {
                        NavCheck(win, key);
                        layoutIssues += LayoutAudit(win, lang + " " + w.ToString("0") + " " + key);
                        if (key == "check") layoutIssues += AuditCheckTabs(win, lang + " " + w.ToString("0"));
                        if (key == "settings")
                        {
                            var sp = FillSettingsProgress(win);
                            if (sp != null)
                            {
                                layoutIssues += LayoutAudit(win, lang + " " + w.ToString("0") + " settings progress");
                                sp.HideDemoProgress();
                                Pump(); ForceLayout(win);
                            }
                        }
                    }
                }
            }
            Loc.SetLang("ru"); Pump();
            if (layoutIssues == 0) _pass++; else _fail++;
            Line(layoutIssues == 0 ? "INFO" : "FAIL", "layout audit total issues=" + layoutIssues);

            // 6c) Снимки на минимальной ширине окна: самые плотные страницы.
            win.Width = 1000; win.Height = 680; Pump(); ForceLayout(win);
            NavCheck(win, "overview"); Shot(win, "min-overview");
            ScrollEnd(win); Shot(win, "min-overview-bottom"); ScrollHome(win);
            NavCheck(win, "settings"); Shot(win, "min-settings");
            // Полоса загрузки обновления: дорожка во всю ширину карточки, на 0 %
            // остаётся видимый кусочек, при неизвестном размере — вся дорожка.
            Try("update progress bar", delegate
            {
                var sp = FillSettingsProgress(win);
                Assert(sp != null);
                sp.CheckDemoProgress();
            });
            {
                var sp = FindPage(win) as SettingsPage;
                if (sp != null) { sp.ScrollProgressIntoView(); Pump(); ForceLayout(win); }
            }
            Shot(win, "min-settings-progress");
            {
                var sp = FindPage(win) as SettingsPage;
                if (sp != null) { sp.HideDemoProgress(); Pump(); ForceLayout(win); }
            }            NavCheck(win, "check"); Shot(win, "min-check");
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
            // Клик (выбор) по первым карточкам-стратегиям СНАЧАЛА (фильтр = «все»).
            int clicked = 0;
            foreach (var bd in Descendants<Border>(page))
            {
                if (bd.Cursor == System.Windows.Input.Cursors.Hand && clicked < 3)
                {
                    Try("card select", delegate {
                        bd.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                            System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                        { RoutedEvent = UIElement.MouseLeftButtonUpEvent, Source = bd });
                        Pump();
                    });
                    clicked++;
                }
            }
            Line("INFO", "card clicks attempted=" + clicked);
            // Затем клики по всем чипам категорий.
            foreach (var btn in Descendants<Button>(page))
            {
                if (btn.Tag is Action)
                    Try("chip click", delegate { btn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Pump(); });
            }
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

        // Диагностика: цепочка предков элемента с текстом и их ширины.
        static void DumpChain(MainWindow win, string needle)
        {
            var page = FindPage(win);
            if (page == null) return;
            foreach (var tb in Descendants<TextBlock>(page))
            {
                if (string.IsNullOrEmpty(tb.Text) || tb.Text.IndexOf(needle, StringComparison.Ordinal) < 0) continue;
                var sb = new StringBuilder("chain '" + tb.Text + "' trim=" + tb.TextTrimming + " wrap=" + tb.TextWrapping);
                DependencyObject d = tb;
                while (d != null)
                {
                    var fe = d as FrameworkElement;
                    if (fe != null)
                        sb.Append(" <- " + fe.GetType().Name + "(w=" + fe.ActualWidth.ToString("0") +
                            ",des=" + fe.DesiredSize.Width.ToString("0") + (fe.ClipToBounds ? ",clip" : "") + ")");
                    if (fe is Page) break;
                    d = VisualTreeHelper.GetParent(d);
                }
                Line("MEAS", sb.ToString());
            }
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

        // Показать полосы прогресса обновления на открытых настройках: в покое
        // они скрыты, и аудит их геометрию не проверял.
        static SettingsPage FillSettingsProgress(MainWindow win)
        {
            var sp = FindPage(win) as SettingsPage;
            if (sp == null) return null;
            sp.ShowDemoProgress();
            Pump(); ForceLayout(win);
            return sp;
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
            Try("nav " + key, delegate { win.Navigate(key); Pump(); ForceLayout(win); });
        }

        static void ThemeSwitch(string tag, ThemeMode m)
        {
            Try("theme " + tag, delegate { Theme.Apply(m); Pump(); });
        }

        static void LangSwitch(string tag, string lang)
        {
            Try("lang " + tag, delegate { Loc.SetLang(lang); Pump(); });
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

        static void ScrollToBottom(DependencyObject root)
        {
            var sv = root as ScrollViewer;
            if (sv != null) sv.ScrollToEnd();
            int n = 0;
            try { n = VisualTreeHelper.GetChildrenCount(root); } catch { return; }
            for (int i = 0; i < n; i++) ScrollToBottom(VisualTreeHelper.GetChild(root, i));
        }

        static void Try(string what, Action act)
        {
            try { act(); _pass++; }
            catch (Exception ex) { _fail++; Line("FAIL", what + " -> " + ex.GetType().Name + ": " + ex.Message); }
        }

        static void Line(string lvl, string msg) { Log.AppendLine("[" + lvl + "] " + msg); }

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

        static void Shot(MainWindow win, string name)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string docsDir = Path.GetFullPath(Path.Combine(baseDir, "..", "docs"));

            SavePng(win, Path.Combine(baseDir, "_shot_" + name + ".png"));

            if (name.StartsWith("overview-"))
            {
                SavePng(win, Path.Combine(docsDir, "themes", name + ".png"));
                if (name == "overview-dark")
                {
                    SavePng(win, Path.Combine(docsDir, "screenshot.png"));
                    SavePng(win, Path.Combine(docsDir, "screens", "overview.png"));
                }
            }
            else if (name.StartsWith("section-"))
            {
                string sName = name.Substring("section-".Length);
                SavePng(win, Path.Combine(docsDir, "screens", sName + ".png"));
            }
            else if (name == "collapsed")
            {
                SavePng(win, Path.Combine(docsDir, "screens", "collapsed.png"));
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
                Core.WinwsLogClear();
                for (int i = 0; i < 450; i++) Core.WinwsLogAppend("line" + i);
                string tail = Core.WinwsLogTail(400);
                Assert(tail.StartsWith("...") && tail.Contains("line449") && !tail.Contains("line49\n"));
                Core.WinwsLogClear();
                Assert(Core.WinwsLogAll().Length == 0);
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

            // --- ID3-кодировки ---
            Try("id3 utf8", delegate
            {
                var data = new List<byte> { 3 };
                data.AddRange(Encoding.UTF8.GetBytes("Тест"));
                Assert(AudioTagReader.DecodeId3Text(data.ToArray()) == "Тест");
            });
            Try("id3 cp1251", delegate
            {
                var data = new List<byte> { 0 };
                data.AddRange(Encoding.GetEncoding(1251).GetBytes("Тест"));
                Assert(AudioTagReader.DecodeId3Text(data.ToArray()) == "Тест");
            });
            Try("id3 double-encoded", delegate
            {
                // Реальный случай: файл помечен enc=0 (ANSI), но байты — сырой UTF-8.
                var data = new List<byte> { 0 };
                data.AddRange(Encoding.UTF8.GetBytes("Привет"));
                Assert(AudioTagReader.DecodeId3Text(data.ToArray()) == "Привет");
            });

            // --- Режимы ipset на изолированном корне (каталог exe selftest) ---
            IpsetModeTests();

            // --- Пробы watchdog: ровно ключевые эндпоинты ---
            Try("quick probes count", delegate { Assert(Core.QuickProbes().Count == 3); });

            // --- Человекочитаемые размеры ---
            Try("humansize smoke", delegate
            {
                Assert(Core.HumanSize(0).Length > 0 && Core.HumanSize(2048).Length > 0);
                Assert(Core.HumanSpeed(1024).Length > 0);
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
    }
}
#endif
