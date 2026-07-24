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

            // 5c) Скриншоты для визуальной проверки (главное меню, светлая тема, службы).
            Core.SetBool("reduce_motion", true); // без анимаций — контент виден сразу
            win.Width = 1200; Pump(); ForceLayout(win);
            NavCheck(win, "overview");
            Theme.Apply(ThemeMode.Dark); Pump(); ForceLayout(win); Shot(win, "overview-dark");
            Theme.Apply(ThemeMode.Light); Pump(); ForceLayout(win); Shot(win, "overview-light");
            NavCheck(win, "strategies"); ForceLayout(win); Shot(win, "strategies-light");
            NavCheck(win, "service"); ForceLayout(win); Shot(win, "service-light");
            NavCheck(win, "settings"); ForceLayout(win); Shot(win, "settings-light");
            NavCheck(win, "about"); ForceLayout(win); Shot(win, "about-light");
            Theme.Apply(ThemeMode.Dark); Pump(); ForceLayout(win);
            NavCheck(win, "service"); ForceLayout(win); Shot(win, "service-dark");

            // 6) Разные ширины окна (проверка сетки/раскладки)
            foreach (var w in new double[] { 1120, 1000, 1300, 1600 })            {
                Try("resize " + w, delegate { win.Width = w; Pump(); NavCheck(win, "strategies"); ForceLayout(win); MeasureGrid(win, w); });
            }

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

        static void Try(string what, Action act)
        {
            try { act(); _pass++; }
            catch (Exception ex) { _fail++; Line("FAIL", what + " -> " + ex.GetType().Name + ": " + ex.Message); }
        }

        static void Line(string lvl, string msg) { Log.AppendLine("[" + lvl + "] " + msg); }

        // Рендер окна в PNG для визуальной проверки.
        static void Shot(MainWindow win, string name)
        {
            try
            {
                int w = (int)win.ActualWidth, h = (int)win.ActualHeight;
                if (w < 10 || h < 10) { Line("WARN", "shot " + name + " skipped (size)"); return; }
                var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                win.Measure(new Size(w, h));
                win.Arrange(new Rect(0, 0, w, h));
                win.UpdateLayout();
                rtb.Render(win);
                var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
                enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_shot_" + name + ".png");
                using (var fs = new FileStream(path, FileMode.Create)) enc.Save(fs);
                Line("SHOT", name + " -> " + path);
            }
            catch (Exception ex) { Line("WARN", "shot " + name + ": " + ex.Message); }
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
    }
}
#endif
