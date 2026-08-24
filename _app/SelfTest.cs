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

        static void BuildArgsTests()
        {
            string root = Path.Combine(Path.GetTempPath(), "lantern_ut_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
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
                try { Directory.Delete(root, true); } catch { }
            }
        }

        static void IpsetModeTests()
        {
            // Работаем в корне selftest-процесса: списки реального zapret не трогаем.
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
