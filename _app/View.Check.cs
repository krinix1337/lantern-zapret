using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZapretStudio
{
    class CheckPage : Page
    {
        public override string Title { get { return Loc.T("check.title"); } }
        public override string Subtitle { get { return Loc.T("check.sub"); } }

        class Endpoint
        {
            public Target T;
            public Border Card;
            public Border StatePill;
            public TextBlock Latency, Detail, When;
            public CheckBox Sel;
            public Canvas Spark;
        }

        // История задержек за сессию (мс) по ключу цели — для спарклайна.
        static readonly Dictionary<string, List<long>> _latencyHistory = new Dictionary<string, List<long>>();
        const int SparkPoints = 24;

        static void PushLatency(Target t, long ms)
        {
            if (ms < 0) return;
            lock (_latencyHistory)
            {
                List<long> h;
                if (!_latencyHistory.TryGetValue(t.Key, out h)) { h = new List<long>(); _latencyHistory[t.Key] = h; }
                h.Add(ms);
                while (h.Count > SparkPoints) h.RemoveAt(0);
            }
        }

        readonly MainWindow _win;

        // Вкладки
        string _tab = "targets";
        WrapPanel _tabBar;
        Border _tabsHost;       // контейнер для содержимого активной вкладки
        readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();

        // Общий список конечных точек (для вкладок targets/popular/games)
        List<Endpoint> _rows = new List<Endpoint>();
        StackPanel _groups;
        Button _btnAll, _btnSel, _btnStop, _btnExport;
        volatile bool _stop;
        int _running;

        public CheckPage(MainWindow win)
        {
            _win = win;
            BuildTabBar();
            _tabsHost = new Border();
            Body.Children.Add(_tabsHost);
        }

        public override void OnShow() { ShowTab(_tab); }

        void BuildTabBar()
        {
            var barContainer = new Border
            {
                Background = Theme.BrSurfaceAlt,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10,
                Padding = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 16)
            };

            _tabBar = new WrapPanel();
            var tabs = new Grid();
            for (int i = 0; i < 5; i++)
                tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var t0 = TabButton("targets", Icons.List, Loc.T("check.tab.targets")); Grid.SetColumn(t0, 0); tabs.Children.Add(t0);
            var t1 = TabButton("popular", Icons.Globe, Loc.T("check.tab.popular")); Grid.SetColumn(t1, 1); tabs.Children.Add(t1);
            var t2 = TabButton("games", Icons.Game, Loc.T("check.tab.games"));      Grid.SetColumn(t2, 2); tabs.Children.Add(t2);
            var t3 = TabButton("strats", Icons.Bolt, Loc.T("check.tab.strats"));    Grid.SetColumn(t3, 3); tabs.Children.Add(t3);
            var t4 = TabButton("diag", Icons.Tool, Loc.T("check.tab.diag"));        Grid.SetColumn(t4, 4); tabs.Children.Add(t4);

            barContainer.Child = tabs;
            Body.Children.Add(barContainer);
        }

        Button TabButton(string key, string icon, string label)
        {
            var b = new Button { Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 4, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R8, Padding = new Thickness(12, 7, 12, 7),
                Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(0) };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var ic = UI.Icon(icon, 15, Theme.BrMuted, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(ic);
            var tb = new TextBlock { Text = label, Foreground = Theme.BrMuted, FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 0, 0) };
            sp.Children.Add(tb);
            bd.Child = sp;
            b.Content = bd;
            b.Tag = new object[] { bd, ic, tb };
            b.Click += (s, e) => ShowTab(key);
            b.MouseEnter += (s, e) =>
            {
                if (key != _tab) bd.Background = Theme.Alpha(Theme.Text, 14);
            };
            b.MouseLeave += (s, e) =>
            {
                if (key != _tab) bd.Background = Brushes.Transparent;
            };
            Ctl.AutomationSetName(b, label);
            _tabButtons[key] = b;
            return b;
        }

        void PaintTabs()
        {
            foreach (var kv in _tabButtons)
            {
                var arr = (object[])kv.Value.Tag;
                var bd = (Border)arr[0]; var ic = (System.Windows.Shapes.Path)arr[1]; var tb = (TextBlock)arr[2];
                bool on = kv.Key == _tab;
                bd.Background = on ? Theme.BrAccent : Brushes.Transparent;
                ic.Stroke = on ? Theme.BrOnAccent : Theme.BrMuted;
                tb.Foreground = on ? Theme.BrOnAccent : Theme.BrMuted;
            }
        }

        void ShowTab(string key)
        {
            _tab = key;
            PaintTabs();
            UIElement content;
            if (key == "strats") content = BuildStratTab();
            else if (key == "diag") content = BuildDiagTab();
            else content = BuildListTab(key);

            if (Theme.AnimationsEnabled)
            {
                var tt = new TranslateTransform(0, 8);
                content.RenderTransform = tt;
                content.Opacity = 0;

                var daFade = new System.Windows.Media.Animation.DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                var daSlide = new System.Windows.Media.Animation.DoubleAnimation(8.0, 0.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };

                content.BeginAnimation(UIElement.OpacityProperty, daFade);
                tt.BeginAnimation(TranslateTransform.YProperty, daSlide);
            }
            _tabsHost.Child = content;
        }

        UIElement BuildListTab(string which)
        {
            var panel = new StackPanel();

            // Панель инструментов — единая строка
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
            _btnAll = Ctl.Button(Loc.T("check.checkAll"), Icons.Pulse, 0);
            _btnAll.Margin = new Thickness(0, 0, 8, 0);
            _btnAll.Click += (s, e) => Start(false);
            _btnSel = Ctl.Button(Loc.T("check.checkSel"), Icons.Check, 1);
            _btnSel.Margin = new Thickness(0, 0, 8, 0);
            _btnSel.Click += (s, e) => Start(true);
            _btnStop = Ctl.Button(Loc.T("common.stop"), Icons.Stop, 2);
            _btnStop.Margin = new Thickness(0, 0, 8, 0);
            _btnStop.IsEnabled = false;
            _btnStop.Click += (s, e) => { _stop = true; };
            _btnExport = Ctl.Button(Loc.T("check.export"), Icons.Save, 1);
            _btnExport.Margin = new Thickness(0);
            _btnExport.Click += (s, e) => Export();
            toolbar.Children.Add(_btnAll);
            toolbar.Children.Add(_btnSel);
            toolbar.Children.Add(_btnStop);
            toolbar.Children.Add(_btnExport);
            panel.Children.Add(toolbar);

            _groups = new StackPanel();
            panel.Children.Add(_groups);

            _rows = new List<Endpoint>();
            List<Target> targets;
            if (which == "popular") targets = Core.PopularTargets();
            else if (which == "games") targets = Core.GameTargets();
            else targets = Core.LoadTargets();

            if (targets.Count == 0)
            {
                _groups.Children.Add(new TextBlock { Text = Loc.T("net.listEmpty"),
                    Foreground = Theme.BrMuted, FontSize = Theme.FsBody, FontFamily = Theme.UiFont });
                return panel;
            }
            string curGroup = null;
            StackPanel gp = null;
            foreach (var t in targets)
            {
                if (t.Group != curGroup)
                {
                    curGroup = t.Group;
                    _groups.Children.Add(SectionLabel(curGroup));
                    gp = new StackPanel();
                    _groups.Children.Add(gp);
                }
                var row = MakeRow(t);
                _rows.Add(row);
                gp.Children.Add(row.Card);
            }
            return panel;
        }

        Endpoint MakeRow(Target t)
        {
            var r = new Endpoint { T = t };
            var g = new Grid();
            // Колонки: выбор | название и адрес | спарклайн | задержка | состояние.
            // У колонки задержки раньше стояла жёсткая ширина 150 px, а строка
            // «ping 116 мс · http 253 мс» шире: текст выравнивался по правому краю
            // и уезжал на 36 px влево — под спарклайн и за скруглённую рамку
            // карточки, которая его обрезала (первая цифра пропадала). Теперь
            // ширина 118 px, а текст в ней переносится на две строки. Auto здесь
            // не годится: если раскладку успели померить на почти нулевой ширине
            // (переключение вкладки во время изменения размера окна), Grid
            // сжимает Auto-колонку до нескольких пикселей и уже не расширяет её
            // при следующем Arrange — задержка снова вылезала из колонки.
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(MetricW) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            r.Sel = Ctl.Check(string.Format(Loc.T("net.select"), t.Name));
            r.Sel.VerticalAlignment = VerticalAlignment.Center;
            r.Sel.Margin = new Thickness(0, 0, 12, 0);
            Grid.SetColumn(r.Sel, 0);
            g.Children.Add(r.Sel);

            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            // Название и тип — сетка, а не горизонтальный StackPanel: тот меряется
            // без ограничения ширины, и «Discord Gateway HTTP/TLS» вылезал из
            // колонки под спарклайн. Тип держим по правому краю названия.
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var nameTb = UI.T(t.Name, Theme.FsBody, Theme.BrText, FontWeights.SemiBold);
            nameTb.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(nameTb, 0);
            top.Children.Add(nameTb);
            var kind = new TextBlock { Text = t.Kind == "PING" ? "Ping" : "HTTP/TLS", Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.MonoFont, Margin = new Thickness(10, 2, 0, 0),
                VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(kind, 1);
            top.Children.Add(kind);
            mid.Children.Add(top);
            mid.Children.Add(new TextBlock { Text = t.Host, Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.MonoFont, Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap });
            // Подробности ответа — отдельной строкой с переносом. В одной строке
            // с адресом (горизонтальный StackPanel меряется без ограничения по
            // ширине) длинный текст вроде «HTTP 200 · TLS 1.2 · 1420 Б» вылезал
            // из колонки под спарклайн. Пока строки нет — места не занимает.
            r.Detail = new TextBlock { Text = "", Foreground = Theme.BrFaint, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            mid.Children.Add(r.Detail);
            Grid.SetColumn(mid, 1);
            g.Children.Add(mid);

            var metric = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(10, 0, 0, 0) };
            // Задержку переносим по словам в пределах колонки: строка
            // «ping 116 мс · http 253 мс» занимала 187 px и отбирала ширину у
            // названия и адреса. Выравнивание — Stretch с TextAlignment.Right:
            // при выравнивании самого блока по правому краю сжатая колонка
            // отводила ему отрицательный X, текст уезжал под спарклайн и
            // обрезался рамкой карточки.
            r.Latency = new TextBlock { Text = "-", Foreground = Theme.BrText, FontSize = Theme.FsBody,
                FontFamily = Theme.MonoFont, HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap };
            r.When = new TextBlock { Text = "", Foreground = Theme.BrFaint, FontSize = Theme.FsTiny,
                FontFamily = Theme.UiFont, HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = TextAlignment.Right, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0) };
            metric.Children.Add(r.Latency);
            metric.Children.Add(r.When);
            Grid.SetColumn(metric, 3);
            g.Children.Add(metric);

            // Спарклайн последних замеров задержки
            r.Spark = MakeSparkCanvas();
            Grid.SetColumn(r.Spark, 2);
            g.Children.Add(r.Spark);

            r.StatePill = Pill.Make(Sev.Neutral, Loc.T("common.waiting"));
            r.StatePill.VerticalAlignment = VerticalAlignment.Center;
            r.StatePill.Margin = new Thickness(14, 0, 0, 0);
            Grid.SetColumn(r.StatePill, 4);
            g.Children.Add(r.StatePill);

            r.Card = UI.Card(g, new Thickness(14, 12, 14, 12));
            r.Card.Margin = new Thickness(0, 0, 0, 8);
            return r;
        }

        const double SparkW = 90, SparkH = 30;
        // Ширина колонки задержки: «ping 1502 мс» в моношрифте занимает 108 px.
        const double MetricW = 118;

        // Пустой график не показываем: пунктирная базовая линия без данных
        // читалась как случайная строчка точек посреди карточки, да и 136 px
        // ширины до первой проверки лучше отдать названию и адресу.
        static Canvas MakeSparkCanvas()
        {
            var c = new Canvas { Width = SparkW, Height = SparkH, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 6, 0), Opacity = 0.85, Visibility = Visibility.Collapsed };
            var baseLine = new System.Windows.Shapes.Line
            {
                X1 = 0, X2 = SparkW, Y1 = SparkH - 1, Y2 = SparkH - 1,
                Stroke = Theme.Alpha(Theme.TextMuted, 60), StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }
            };
            c.Children.Add(baseLine);
            return c;
        }

        // Перерисовать спарклайн строки по накопленной истории (мс).
        void DrawSpark(Endpoint r)
        {
            List<long> h;
            lock (_latencyHistory)
            {
                List<long> src;
                if (!_latencyHistory.TryGetValue(r.T.Key, out src) || src.Count < 2) return;
                h = new List<long>(src);
            }
            var c = r.Spark;
            c.Visibility = Visibility.Visible;
            // точки — отдельные дети после базовой линии
            for (int i = c.Children.Count - 1; i >= 1; i--) c.Children.RemoveAt(i);

            long min = long.MaxValue, max = long.MinValue;
            foreach (var v in h) { if (v < min) min = v; if (v > max) max = v; }
            if (max == min) max = min + 1;

            var pts = new PointCollection();
            double stepX = SparkW / (SparkPoints - 1);
            double offset = SparkPoints - h.Count;
            for (int i = 0; i < h.Count; i++)
            {
                double x = (offset + i) * stepX;
                double y = (SparkH - 3) - ((h[i] - min) / (double)(max - min)) * (SparkH - 6);
                pts.Add(new Point(x, y));
            }

            var line = new System.Windows.Shapes.Polyline
            {
                Points = pts,
                Stroke = Theme.Frozen(Theme.AccentHi),
                StrokeThickness = 1.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            c.Children.Add(line);

            var last = pts[pts.Count - 1];
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 5, Height = 5, Fill = Theme.Frozen(Theme.AccentMain)
            };
            Canvas.SetLeft(dot, last.X - 2.5);
            Canvas.SetTop(dot, last.Y - 2.5);
            c.Children.Add(dot);
        }

        static Sev SevOf(string state)
        {
            switch (state)
            {
                case "reachable": return Sev.Ok;
                case "partial": return Sev.Warn;
                case "checking": return Sev.Progress;
                case "waiting": return Sev.Neutral;
                default: return Sev.Err;
            }
        }

        static string StateLabel(string state)
        {
            switch (state)
            {
                case "reachable":   return Loc.T("net.reachable");
                case "partial":     return Loc.T("net.partial");
                case "unreachable": return Loc.T("net.unreachable");
                case "timeout":     return Loc.T("net.timeout");
                case "errDns":      return Loc.T("net.errDns");
                case "errTls":      return Loc.T("net.errTls");
                case "checking":    return Loc.T("net.checking");
                case "waiting":     return Loc.T("common.waiting");
                default:            return Loc.T("net.err");
            }
        }

#if SELFTEST
        // Заполнить строки правдоподобными результатами. Аудит вёрстки иначе
        // видит только исходное «-» и не замечает, что заполненная строка
        // (задержка + подробности + плашка) не влезает по ширине.
        internal void FillDemoResults()
        {
            string[] states = { "reachable", "partial", "timeout", "errTls", "unreachable" };
            long[] samples = { 98, 97, 116, 101, 12, 340, 1502 };
            int i = 0;
            foreach (var r in _rows)
            {
                long ms = samples[i % samples.Length];
                for (int k = 0; k < SparkPoints; k++) PushLatency(r.T, ms + (k * 7) % 23);
                DrawSpark(r);
                string lat = Loc.T("net.ping") + " " + ms + Loc.T("net.ms") + "\n" +
                    Loc.T("net.http") + " " + (ms + 137) + Loc.T("net.ms");
                SetRow(r, states[i % states.Length], lat, "HTTP 200 · TLS 1.2 · 1420 " + Loc.T("unit.b"), "12:34:56");
                i++;
            }
        }
#endif

        void SetRow(Endpoint r, string state, string latency, string detail, string when)
        {
            var np = Pill.Make(SevOf(state), StateLabel(state));
            np.VerticalAlignment = VerticalAlignment.Center;
            np.Margin = new Thickness(14, 0, 0, 0);
            var g = (Grid)r.Card.Child;
            int idx = g.Children.IndexOf(r.StatePill);
            Grid.SetColumn(np, 4);
            if (idx >= 0) { g.Children.RemoveAt(idx); g.Children.Insert(idx, np); }
            r.StatePill = np;
            if (latency != null) r.Latency.Text = latency;
            if (detail != null)
            {
                r.Detail.Text = detail;
                r.Detail.Visibility = detail.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            if (when != null) r.When.Text = when;
        }

        void Start(bool selectedOnly)
        {
            if (_running > 0) return;
            _stop = false;
            var todo = new List<Endpoint>();
            foreach (var r in _rows)
                if (!selectedOnly || r.Sel.IsChecked == true) todo.Add(r);
            if (todo.Count == 0) return;

            _btnStop.IsEnabled = true; _btnAll.IsEnabled = false; _btnSel.IsEnabled = false;
            Core.Info(string.Format(Loc.T("check.run.started"), todo.Count));
            foreach (var r in todo) SetRow(r, "waiting", "-", "", "");

            int[] timeouts = { 3000, 5000, 8000, 10000 };
            int tIdx = Core.GetInt("check_timeout_idx", 1);
            int timeout = (tIdx >= 0 && tIdx < timeouts.Length) ? timeouts[tIdx] : 5000;
            _running = todo.Count;
            foreach (var r in todo)
            {
                var row = r;
            ThreadPool.QueueUserWorkItem(delegate
            {
                CheckResult res = null;
                try
                {
                    if (_stop) return;
                    try { Dispatcher.Invoke((Action)delegate { SetRow(row, "checking", "...", "", ""); }); } catch { }
                    res = Core.TestTarget(row.T, timeout);
                    long pingMs = Core.PingHost(row.T.Host, 3000);
                    // История для спарклайна: HTTP-задержка, иначе пинг
                    long sample = res.Ms >= 0 ? res.Ms : pingMs;
                    if (sample >= 0) PushLatency(row.T, sample);
                    Dispatcher.Invoke((Action)delegate
                    {
                        DrawSpark(row);
                        string lat = "";
                        if (pingMs >= 0) lat += Loc.T("net.ping") + " " + pingMs + Loc.T("net.ms");
                        // Пинг и http — отдельными строками: в одну строку они
                        // занимали 187 px и отбирали ширину у названия и адреса.
                        if (res.Ms >= 0) lat += (lat.Length > 0 ? "\n" : "") + Loc.T("net.http") + " " + res.Ms + Loc.T("net.ms");
                        if (lat.Length == 0) lat = "-";
                        SetRow(row, res.State, lat, res.Detail, res.When.HasValue ? res.When.Value.ToString("HH:mm:ss") : "");
                    });
                }
                catch { }
                finally
                {
                    // Счётчик должен уменьшаться при любом исходе, иначе кнопки
                    // проверки останутся заблокированными до пересборки вкладки.
                    try { Done(row, res); } catch { }
                }
            });
            }
        }

        void Done(Endpoint row, CheckResult res)
        {
            int left = Interlocked.Decrement(ref _running);
            if (left <= 0)
                try
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        _btnStop.IsEnabled = false; _btnAll.IsEnabled = true; _btnSel.IsEnabled = true;
                        Core.Info(_stop ? Loc.T("check.run.stopped") : Loc.T("check.run.done"));
                    });
                }
                catch { }
        }

        void Export()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format(Loc.T("check.export.head"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.AppendLine();
            foreach (var r in _rows)
                sb.AppendLine(string.Format("{0,-24} {1,-10} {2,-24} {3}",
                    r.T.Name, Pill.GetText(r.StatePill),
                    // В интерфейсе задержка занимает две строки — в отчёте
                    // возвращаем её в одну, иначе таблица разъезжается.
                    r.Latency.Text.Replace("\r", "").Replace("\n", " · "), r.Detail.Text));
            try
            {
                string path = System.IO.Path.Combine(Core.Root, "connection-check.txt");
                System.IO.File.WriteAllText(path, sb.ToString());
                Core.Good(string.Format(Loc.T("check.export.ok"), path));
                Core.OpenFile(path);
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("check.export.err"), ex.Message)); }
        }

        // ---------- Вкладка: проверка отдельных стратегий ----------
        StackPanel _stratList;
        Button _stratStopBtn, _stratRunSel, _stratRunAll, _stratSelAll, _stratClear;
        volatile bool _stratBusy;
        volatile bool _stratCancel;
        readonly List<StratRowRef> _stratRows = new List<StratRowRef>();

        UIElement BuildStratTab()
        {
            var panel = new StackPanel();
            _stratRows.Clear();

            // Пояснение + предупреждение о правах
            panel.Children.Add(NoteCard(Icons.Info, Theme.BrAccent, Loc.T("check.strat.hint"), Sev.Info));
            panel.Children.Add(new Border { Height = 12 });

            // Панель массовой проверки: выбрать несколько или все стратегии.
            var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            _stratRunSel = Ctl.Button(Loc.T("check.strat.runSel"), Icons.Check, 0);
            _stratRunSel.Margin = new Thickness(0, 0, 10, 10);
            _stratRunSel.Click += (s, e) => RunBatch(true);
            _stratRunAll = Ctl.Button(Loc.T("check.strat.runAll"), Icons.Bolt, 1);
            _stratRunAll.Margin = new Thickness(0, 0, 10, 10);
            _stratRunAll.Click += (s, e) => RunBatch(false);
            _stratSelAll = Ctl.Button(Loc.T("check.strat.selectAll"), Icons.Grid, 3);
            _stratSelAll.Margin = new Thickness(0, 0, 10, 10);
            _stratSelAll.Click += (s, e) => SetAllSel(true);
            _stratClear = Ctl.Button(Loc.T("check.strat.clearSel"), Icons.Cross, 3);
            _stratClear.Margin = new Thickness(0, 0, 10, 10);
            _stratClear.Click += (s, e) => SetAllSel(false);
            _stratStopBtn = Ctl.Button(Loc.T("common.stop"), Icons.Stop, 2);
            _stratStopBtn.Margin = new Thickness(0, 0, 10, 10);
            _stratStopBtn.IsEnabled = false;
            _stratStopBtn.Click += (s, e) => { _stratCancel = true; };
            var autoPick = Ctl.Button(Loc.T("check.strat.autoPick"), Icons.Star, 0);
            autoPick.Margin = new Thickness(0, 0, 10, 10);
            autoPick.Click += (s, e) => AutoPick();
            bar.Children.Add(_stratRunSel);
            bar.Children.Add(_stratRunAll);
            bar.Children.Add(_stratSelAll);
            bar.Children.Add(_stratClear);
            bar.Children.Add(_stratStopBtn);
            bar.Children.Add(autoPick);
            panel.Children.Add(bar);

            _stratList = new StackPanel();
            panel.Children.Add(_stratList);

            var files = Core.GetStrategyFiles();
            if (files.Count == 0)
            {
                _stratList.Children.Add(new TextBlock { Text = Loc.T("net.notFound"),
                    Foreground = Theme.BrMuted, FontSize = Theme.FsBody, FontFamily = Theme.UiFont });
                return panel;
            }
            foreach (var f in files)
                _stratList.Children.Add(StratRow(f));
            return panel;
        }

        void SetAllSel(bool on)
        {
            foreach (var rr in _stratRows)
                if (rr.Sel != null) rr.Sel.IsChecked = on;
        }

        class ProbeReport
        {
            public string Name;
            public string Url;
            public bool Ok;
            public string Detail;
            public long Ms;
        }

        class StratRowRef
        {
            public string File;
            public Border Card;
            public Border Pill;
            public TextBlock Result;
            public Button Run;
            public CheckBox Sel;
            public List<ProbeReport> LastReports;
        }

        Border StratRow(string file)
        {
            var rr = new StratRowRef { File = file };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            rr.Sel = Ctl.Check(string.Format(Loc.T("net.select"), Core.PrettyName(file)));
            rr.Sel.VerticalAlignment = VerticalAlignment.Center;
            rr.Sel.Margin = new Thickness(0, 0, 12, 0);
            Grid.SetColumn(rr.Sel, 0);
            g.Children.Add(rr.Sel);

            var mid = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            mid.Children.Add(UI.T(Core.PrettyName(file), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            rr.Result = new TextBlock { Text = Core.DescriptionOf(file), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap };
            mid.Children.Add(rr.Result);
            Grid.SetColumn(mid, 1);
            g.Children.Add(mid);

            rr.Pill = Pill.Make(Sev.Neutral, Loc.T("common.waiting"));
            rr.Pill.VerticalAlignment = VerticalAlignment.Center;
            rr.Pill.Margin = new Thickness(14, 0, 12, 0);
            Grid.SetColumn(rr.Pill, 2);
            g.Children.Add(rr.Pill);

            rr.Run = Ctl.Button(Loc.T("check.strat.run"), Icons.Play, 1);
            rr.Run.VerticalAlignment = VerticalAlignment.Center;
            rr.Run.Click += (s, e) => RunSingle(rr);
            Grid.SetColumn(rr.Run, 3);
            g.Children.Add(rr.Run);

            rr.Card = UI.Card(g, new Thickness(14, 12, 14, 12));
            rr.Card.Margin = new Thickness(0, 0, 0, 8);
            rr.Card.Tag = rr;
            _stratRows.Add(rr);
            return rr.Card;
        }

        void SetStratPill(StratRowRef rr, Sev sev, string text)
        {
            var np = Pill.Make(sev, text);
            np.VerticalAlignment = VerticalAlignment.Center;
            np.Margin = new Thickness(14, 0, 12, 0);
            var g = (Grid)rr.Card.Child;
            int idx = g.Children.IndexOf(rr.Pill);
            Grid.SetColumn(np, 2);
            if (idx >= 0) { g.Children.RemoveAt(idx); g.Children.Insert(idx, np); }
            rr.Pill = np;
        }

        // Тестовые адреса для проверки стратегии — охват как в service.bat
        // (Discord: сайт/CDN/медиа/gateway; YouTube: сайт/мобильный/картинки/музыка/видео-CDN).
        // Реальные проверки, без подмены результатов.
        static List<Target> StratProbes()
        {
            var l = new List<Target>();
            AddProbe(l, "Discord Web",    "Discord", "https://discord.com");
            AddProbe(l, "Discord CDN",    "Discord", "https://cdn.discordapp.com");
            AddProbe(l, "YouTube Web",    "YouTube", "https://www.youtube.com");
            AddProbe(l, "YouTube Video",  "YouTube", "https://i.ytimg.com");
            AddProbe(l, "Google",         "Google",  "https://www.google.com");
            return l;
        }

        static void AddProbe(List<Target> l, string name, string group, string url)
        {
            var t = new Target { Key = name, Name = name, Group = group, Kind = "HTTP", Url = url };
            try { t.Host = new Uri(url).Host; } catch { t.Host = url; }
            l.Add(t);
        }

        void BeginBusy()
        {
            _stratBusy = true; _stratCancel = false;
            _stratStopBtn.IsEnabled = true;
            _stratRunSel.IsEnabled = false; _stratRunAll.IsEnabled = false;
            _stratSelAll.IsEnabled = false; _stratClear.IsEnabled = false;
            foreach (var rr in _stratRows) if (rr.Run != null) rr.Run.IsEnabled = false;
        }

        void EndBusy()
        {
            _stratBusy = false; _stratStopBtn.IsEnabled = false;
            _stratRunSel.IsEnabled = true; _stratRunAll.IsEnabled = true;
            _stratSelAll.IsEnabled = true; _stratClear.IsEnabled = true;
            foreach (var rr in _stratRows) if (rr.Run != null) rr.Run.IsEnabled = true;
        }

        bool GuardAdmin()
        {
            if (Core.IsAdmin()) return true;
            MessageBox.Show(Loc.T("check.strat.noAdmin"),
                Loc.T("service.noAdmin.title"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        void RunSingle(StratRowRef rr)
        {
            if (_stratBusy) return;
            if (!GuardAdmin()) return;
            var confirm = MessageBox.Show(
                string.Format(Loc.T("check.strat.confirmOne"), Core.PrettyName(rr.File)),
                Loc.T("check.strat.confirmOne.title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            var one = new List<StratRowRef>(); one.Add(rr);
            RunQueue(one);
        }

        void RunBatch(bool selectedOnly)
        {
            if (_stratBusy) return;
            if (!GuardAdmin()) return;
            var queue = new List<StratRowRef>();
            foreach (var rr in _stratRows)
                if (!selectedOnly || rr.Sel.IsChecked == true) queue.Add(rr);
            if (queue.Count == 0) { Core.Warn(Loc.T("check.strat.noneSel")); return; }

            var confirm = MessageBox.Show(
                string.Format(Loc.T("check.strat.confirmAll"), queue.Count),
                Loc.T("check.strat.confirmAll.title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            RunQueue(queue);
        }

        // Последовательная проверка очереди стратегий в одном фоновом потоке.
        // Стратегии нельзя проверять параллельно — winws общий, они конфликтуют.
        void RunQueue(List<StratRowRef> queue)
        {
            BeginBusy();
            bool wasRunning = _win.IsActive2();
            string prevStrat = _win.CurrentStrategyFile();
            foreach (var rr in queue) SetStratPill(rr, Sev.Neutral, Loc.T("check.strat.queued"));
            var probes = StratProbes();
            Core.Info(Loc.T("check.strat.busy") + " (" + queue.Count + ")");

            ThreadPool.QueueUserWorkItem(delegate
            {
              if (!Core.TryBeginWinwsOperation())
              {
                Dispatcher.Invoke((Action)delegate { EndBusy(); Core.Warn(Loc.T("mw.busy")); });
                return;
              }
              try {
                foreach (var rr in queue)
                {
                    if (_stratCancel) { Dispatcher.Invoke((Action)delegate { SetStratPill(rr, Sev.Neutral, Loc.T("check.strat.stopped")); }); continue; }
                    var row = rr;
                    Dispatcher.Invoke((Action)delegate { SetStratPill(row, Sev.Progress, Loc.T("net.checking")); row.Result.Text = Loc.T("check.strat.launching"); });

                    int ok = 0, total = probes.Count;
                    string err = null;
                    bool isAlreadyRunning = wasRunning && string.Equals(prevStrat, row.File, StringComparison.OrdinalIgnoreCase) && Core.IsWinwsRunning();
                    var reports = new List<ProbeReport>();
                    object reportsLock = new object();
                    try
                    {
                        if (!isAlreadyRunning)
                        {
                            Core.KillWinws();
                            if (!Core.StartWinws(row.File)) throw new Exception("winws.exe failed to start");
                            // Полноценный прогрев WinDivert: 2.0 секунды (как в service.bat)
                            for (int i = 0; i < 20 && !_stratCancel; i++) Thread.Sleep(100);
                        }
                        else
                        {
                            Thread.Sleep(200);
                        }

                        // Параллельная проверка всех целей
                        var barrier = new Core.WorkBarrier(probes.Count);
                        foreach (var t in probes)
                        {
                            var tt = t;
                            ThreadPool.QueueUserWorkItem(delegate
                            {
                                bool isOk = false;
                                string detail = "";
                                long ms = -1;
                                try
                                {
                                    if (!_stratCancel)
                                    {
                                        if (tt.Kind == "PING")
                                        {
                                            var res = Core.TestPing(tt.Host, 4000);
                                            isOk = (res.State == "reachable");
                                            ms = res.Ms;
                                            detail = isOk ? (res.Ms + " ms") : (res.Detail ?? "Timeout");
                                        }
                                        else
                                        {
                                            var cr = Core.CurlCheck(tt.Url, 5); // 5 сек таймаут как в service.bat
                                            isOk = (cr.Verdict == "ok");
                                            ms = cr.Ms;
                                            detail = isOk ? (cr.BestCode != null ? "HTTP " + cr.BestCode : "OK") : (cr.Detail ?? "Error");
                                        }
                                        if (isOk) Interlocked.Increment(ref ok);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    detail = ex.Message;
                                }
                                finally
                                {
                                    lock (reportsLock)
                                    {
                                        reports.Add(new ProbeReport { Name = tt.Name, Url = tt.Url, Ok = isOk, Detail = detail, Ms = ms });
                                    }
                                    barrier.Signal();
                                }
                            });
                        }
                        barrier.Wait(9000);
                    }
                    catch (Exception ex) { err = ex.Message; }
                    finally
                    {
                        if (!isAlreadyRunning)
                        {
                            try { Core.KillWinws(); } catch { }
                        }
                    }

                    int okCount = ok; string error = err;
                    var repSnapshot = reports;
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (error != null)
                        {
                            SetStratPill(row, Sev.Err, Loc.T("check.strat.err"));
                            row.Result.Text = string.Format(Loc.T("check.strat.errDetail"), error);
                            Core.Fail(string.Format(Loc.T("check.strat.errLog"), Core.PrettyName(row.File), error));
                            return;
                        }
                        if (_stratCancel)
                        {
                            SetStratPill(row, Sev.Neutral, Loc.T("check.strat.stopped"));
                            row.Result.Text = Loc.T("check.strat.cancelled");
                            return;
                        }

                        // Упорядочиваем по исходному порядку зондов
                        var ordered = new List<ProbeReport>();
                        foreach (var p in probes)
                        {
                            ProbeReport found = null;
                            foreach (var r in repSnapshot) if (r.Name == p.Name) { found = r; break; }
                            if (found != null) ordered.Add(found);
                        }

                        var okParts = new List<string>();
                        var failParts = new List<string>();
                        var ttSb = new System.Text.StringBuilder();
                        ttSb.AppendLine(Core.PrettyName(row.File) + ":");

                        foreach (var rpt in ordered)
                        {
                            string mark = rpt.Ok ? "✓" : "✗";
                            string timeInfo = rpt.Ms >= 0 ? " [" + rpt.Ms + " ms]" : "";
                            ttSb.AppendLine(string.Format("  {0} {1}: {2}{3}", mark, rpt.Name, rpt.Detail, timeInfo));

                            string shortItem = rpt.Name + (rpt.Detail != null ? " (" + rpt.Detail + ")" : "");
                            if (rpt.Ok) okParts.Add(shortItem);
                            else failParts.Add(shortItem);
                        }

                        Sev sev = okCount == total ? Sev.Ok : (okCount > 0 ? Sev.Warn : Sev.Err);
                        string label = okCount == total ? Loc.T("check.strat.pass") : (okCount > 0 ? Loc.T("check.strat.partial") : Loc.T("check.strat.fail"));
                        SetStratPill(row, sev, label);

                        string summaryText;
                        if (okCount == total)
                        {
                            summaryText = string.Format("Доступно {0}/{1}: {2}", okCount, total, string.Join(", ", okParts.ToArray()));
                        }
                        else if (okCount > 0)
                        {
                            summaryText = string.Format("Доступно {0}/{1} · Сбой: {2}", okCount, total, string.Join(", ", failParts.ToArray()));
                        }
                        else
                        {
                            summaryText = string.Format("Не работает (0/{0}) · Все адреса недоступны", total);
                        }

                        row.Result.Text = summaryText;
                        string ttStr = ttSb.ToString().TrimEnd();
                        row.Card.ToolTip = ttStr;
                        row.Pill.ToolTip = ttStr;
                        row.LastReports = ordered;
                        Core.Info(string.Format("[{0}] {1}", Core.PrettyName(row.File), summaryText));
                    });
                }

                Dispatcher.Invoke((Action)delegate
                {
                    EndBusy();
                    Core.Info(_stratCancel ? Loc.T("check.strat.batchStopped") : Loc.T("check.strat.batchDone"));
                    if (!_stratCancel) ShowComparison();
                });
              } catch { }
              finally
              {
                  Core.EndWinwsOperation();
                  if (wasRunning && !string.IsNullOrEmpty(prevStrat))
                  {
                      Dispatcher.Invoke((Action)delegate { try { _win.RunStrategy(prevStrat); } catch { } });
                  }
              }
            });
        }

        // Таблица сравнения стратегий после массового прогона.
        Border _comparisonCard;

        void ShowComparison()
        {
            // Собираем результаты из строк.
            var results = new List<KeyValuePair<string, string>>();
            foreach (var rr in _stratRows)
            {
                if (rr.Pill == null) continue;
                var sp = rr.Pill.Child as StackPanel;
                if (sp == null || sp.Children.Count < 2) continue;
                var tb = sp.Children[1] as TextBlock;
                string status = tb != null ? tb.Text : "";
                results.Add(new KeyValuePair<string, string>(Core.PrettyName(rr.File), status + " — " + rr.Result.Text));
            }
            if (results.Count == 0) return;

            // Сортируем: прошедшие первыми.
            results.Sort(delegate (KeyValuePair<string, string> a, KeyValuePair<string, string> b)
            {
                bool aOk = a.Value.StartsWith(Loc.T("check.strat.pass"));
                bool bOk = b.Value.StartsWith(Loc.T("check.strat.pass"));
                if (aOk && !bOk) return -1;
                if (!aOk && bOk) return 1;
                return string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
            });

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(Loc.T("check.strat.comparison"));
            sb.AppendLine(new string('─', 40));
            foreach (var r in results)
                sb.AppendLine(r.Key + ": " + r.Value);

            if (_comparisonCard != null && _stratList.Children.Contains(_comparisonCard))
                _stratList.Children.Remove(_comparisonCard);

            var txt = new TextBlock { Text = sb.ToString(), Foreground = Theme.BrText,
                FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, TextWrapping = TextWrapping.Wrap };
            _comparisonCard = UI.Card(txt, new Thickness(16, 14, 16, 14), Theme.R10, Theme.Alpha(Theme.AccentMain, 10));
            _comparisonCard.BorderBrush = Theme.Alpha(Theme.AccentMain, 60);
            _comparisonCard.Margin = new Thickness(0, 0, 0, 12);
            _stratList.Children.Insert(0, _comparisonCard);
        }

        // Автоподбор: прогоняет все стратегии и выбирает лучшую.
        void AutoPick()
        {
            if (_stratBusy) return;
            if (!GuardAdmin()) return;
            var files = Core.GetStrategyFiles();
            if (files.Count == 0) { Core.Warn(Loc.T("net.notFound")); return; }

            var confirm = MessageBox.Show(
                string.Format(Loc.T("check.strat.autoPickConfirm"), files.Count),
                Loc.T("check.strat.autoPick"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            BeginBusy();
            var probes = StratProbes();
            Core.Info(string.Format(Loc.T("check.strat.autoPickStart"), files.Count));

            ThreadPool.QueueUserWorkItem(delegate
            {
              // Держим глобальную операцию на весь прогон, как RunQueue: иначе
              // между стратегиями пользователь мог бы запустить обход, а занятый
              // лок молча помечал бы стратегии как «не пройдено».
              if (!Core.TryBeginWinwsOperation())
              {
                Dispatcher.Invoke((Action)delegate { EndBusy(); Core.Warn(Loc.T("mw.busy")); });
                return;
              }
              try {
                var scores = new List<Core.StratScore>();
                foreach (var f in files)
                {
                    if (_stratCancel) break;
                    var file = f;
                    Dispatcher.Invoke((Action)delegate
                    {
                        foreach (var rr in _stratRows)
                            if (rr.File == file) { SetStratPill(rr, Sev.Progress, Loc.T("net.checking")); break; }
                    });
                    var sc = new Core.StratScore { File = file, Total = probes.Count };
                    Core.RunStrategyProbe(file, probes, () => _stratCancel, sc);
                    scores.Add(sc);
                    Dispatcher.Invoke((Action)delegate
                    {
                        foreach (var rr in _stratRows)
                        {
                            if (rr.File == file)
                            {
                                Sev sev = sc.Ok == sc.Total ? Sev.Ok : (sc.Ok > 0 ? Sev.Warn : Sev.Err);
                                string label = sc.Ok == sc.Total ? Loc.T("check.strat.pass") : (sc.Ok > 0 ? Loc.T("check.strat.partial") : Loc.T("check.strat.fail"));
                                SetStratPill(rr, sev, label);
                                rr.Result.Text = string.Format(Loc.T("check.strat.resultLine"), sc.Ok, sc.Total);
                                break;
                            }
                        }
                    });
                }

                Dispatcher.Invoke((Action)delegate
                {
                    EndBusy();
                    if (_stratCancel) { Core.Info(Loc.T("check.strat.batchStopped")); return; }
                    // Выбираем лучшую: макс Ok, затем мин AvgMs.
                    Core.StratScore best = null;
                    foreach (var sc in scores)
                    {
                        if (best == null) { best = sc; continue; }
                        if (sc.Ok > best.Ok || (sc.Ok == best.Ok && sc.AvgMs < best.AvgMs)) best = sc;
                    }
                    if (best != null && best.Ok > 0)
                    {
                        _win.SelectStrategy(best.File);
                        Core.Good(string.Format(Loc.T("check.strat.autoPickDone"), Core.PrettyName(best.File), best.Ok, best.Total));
                        MessageBox.Show(string.Format(Loc.T("check.strat.autoPickDone"), Core.PrettyName(best.File), best.Ok, best.Total),
                            Loc.T("check.strat.autoPick"), MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        Core.Warn(Loc.T("check.strat.autoPickNone"));
                        MessageBox.Show(Loc.T("check.strat.autoPickNone"),
                            Loc.T("check.strat.autoPick"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });
              } catch { }
              finally { Core.EndWinwsOperation(); }
            });
        }

        UIElement BuildDiagTab()
        {
            var panel = new StackPanel();

            // 1. DPI Freeze Test
            panel.Children.Add(SectionLabel(Loc.T("check.diag.dpiFreeze")));
            var dpiContent = new StackPanel();
            dpiContent.Children.Add(new TextBlock
            {
                Text = Loc.T("check.diag.dpiFreezeDesc"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var dpiRow = new Grid();
            dpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            dpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            dpiRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var dpiBtn = Ctl.Button(Loc.T("check.diag.dpiFreezeRun"), Icons.Bolt, 0);
            var dpiPillHost = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
            dpiPillHost.Content = Pill.Make(Sev.Neutral, Loc.T("common.waiting"));
            var dpiStatusText = new TextBlock
            {
                Text = "",
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(14, 0, 0, 0)
            };

            dpiBtn.Click += (s, e) =>
            {
                dpiBtn.IsEnabled = false;
                dpiPillHost.Content = Pill.Make(Sev.Progress, Loc.T("check.diag.dpiFreezeTesting"));
                dpiStatusText.Text = Loc.T("check.diag.dpiFreezeTesting");
                Core.RunDpiFreezeTest(delegate (bool ok, string detail, int read, long ms)
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        dpiBtn.IsEnabled = true;
                        dpiPillHost.Content = Pill.Make(ok ? Sev.Ok : Sev.Err, ok ? Loc.T("check.diag.dpiFreezeOk") : Loc.T("check.diag.dpiFreezeFail"));
                        dpiStatusText.Text = detail;
                        _win.ShowToast(ok ? Loc.T("check.diag.dpiFreezeOk") : Loc.T("check.diag.dpiFreezeFail"), ok ? Sev.Ok : Sev.Warn);
                    });
                });
            };

            Grid.SetColumn(dpiBtn, 0);
            Grid.SetColumn(dpiPillHost, 2);
            Grid.SetColumn(dpiStatusText, 3);
            dpiRow.Children.Add(dpiBtn);
            dpiRow.Children.Add(dpiPillHost);
            dpiRow.Children.Add(dpiStatusText);
            dpiContent.Children.Add(dpiRow);

            panel.Children.Add(UI.Card(dpiContent, new Thickness(16, 14, 16, 14)));

            panel.Children.Add(new Border { Height = 14 });

            // 2. Инструменты быстрого решения проблем
            panel.Children.Add(SectionLabel(Loc.T("check.diag.title")));
            var repairGrid = new Grid();
            repairGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            repairGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            repairGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Карточка 1: Очистить кэш Discord
            var dcPanel = new StackPanel();
            var dcTop = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            dcTop.Children.Add(UI.Icon(Icons.Discord, 16, Theme.BrAccent, 1.8));
            var dcTitle = UI.T(Loc.T("check.diag.discordCache"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold);
            dcTitle.Margin = new Thickness(8, 0, 0, 0);
            dcTop.Children.Add(dcTitle);
            dcPanel.Children.Add(dcTop);
            dcPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("check.diag.discordCacheDesc"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                Height = 48
            });
            var dcBtn = Ctl.Button(Loc.T("check.diag.discordCache"), Icons.Trash, 1);
            dcBtn.HorizontalAlignment = HorizontalAlignment.Left;
            dcBtn.Click += (s, e) =>
            {
                dcBtn.IsEnabled = false;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    int deleted = Core.ClearDiscordCache();
                    Dispatcher.Invoke((Action)delegate
                    {
                        dcBtn.IsEnabled = true;
                        string msg = string.Format(Loc.T("check.diag.discordCacheOk"), deleted);
                        _win.ShowToast(msg, Sev.Ok);
                        Core.Good(msg);
                    });
                });
            };
            dcPanel.Children.Add(dcBtn);
            var dcCard = UI.Card(dcPanel, new Thickness(16, 14, 16, 14));
            Grid.SetColumn(dcCard, 0);
            repairGrid.Children.Add(dcCard);

            // Карточка 2: Сброс зависшего WinDivert
            var wdPanel = new StackPanel();
            var wdTop = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            wdTop.Children.Add(UI.Icon(Icons.Refresh, 16, Theme.BrWarn, 1.8));
            var wdTitle = UI.T(Loc.T("check.diag.wdReset"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold);
            wdTitle.Margin = new Thickness(8, 0, 0, 0);
            wdTop.Children.Add(wdTitle);
            wdPanel.Children.Add(wdTop);
            wdPanel.Children.Add(new TextBlock
            {
                Text = Loc.T("check.diag.wdResetDesc"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                Height = 48
            });
            var wdBtn = Ctl.Button(Loc.T("check.diag.wdReset"), Icons.Refresh, 1);
            wdBtn.HorizontalAlignment = HorizontalAlignment.Left;
            wdBtn.Click += (s, e) =>
            {
                wdBtn.IsEnabled = false;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    bool ok = Core.ResetWinDivertDriver();
                    Dispatcher.Invoke((Action)delegate
                    {
                        wdBtn.IsEnabled = true;
                        if (ok)
                        {
                            _win.ShowToast(Loc.T("check.diag.wdResetOk"), Sev.Ok);
                            Core.Good(Loc.T("check.diag.wdResetOk"));
                        }
                    });
                });
            };
            wdPanel.Children.Add(wdBtn);
            var wdCard = UI.Card(wdPanel, new Thickness(16, 14, 16, 14));
            Grid.SetColumn(wdCard, 2);
            repairGrid.Children.Add(wdCard);

            panel.Children.Add(repairGrid);

            panel.Children.Add(new Border { Height = 14 });

            // 3. Параметры системы и окружения — загружаем АСИНХРОННО, чтобы вкладка открывалась мгновенно
            var envHeader = new Grid();
            envHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            envHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var envLabel = SectionLabel(Loc.T("check.diag.sub"));
            Grid.SetColumn(envLabel, 0);
            envHeader.Children.Add(envLabel);

            var diagRefreshBtn = Ctl.Button(Loc.T("check.diag.run"), Icons.Refresh, 1);
            diagRefreshBtn.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(diagRefreshBtn, 1);
            envHeader.Children.Add(diagRefreshBtn);
            panel.Children.Add(envHeader);

            var diagListPanel = new StackPanel();

            // Плейсхолдер загрузки
            var loadingText = new TextBlock
            {
                Text = Loc.T("check.diag.loading"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont,
                Margin = new Thickness(2, 4, 0, 12)
            };
            diagListPanel.Children.Add(loadingText);

            // Асинхронная загрузка диагностики
            Action refreshDiagList = null;
            refreshDiagList = delegate
            {
                diagListPanel.Children.Clear();
                var spinner = new TextBlock
                {
                    Text = Loc.T("check.diag.loading"),
                    Foreground = Theme.BrMuted,
                    FontSize = Theme.FsBody,
                    FontFamily = Theme.UiFont,
                    Margin = new Thickness(2, 4, 0, 12)
                };
                diagListPanel.Children.Add(spinner);
                diagRefreshBtn.IsEnabled = false;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    var items = Core.RunDiagnostics();
                    Dispatcher.Invoke((Action)delegate
                    {
                        diagListPanel.Children.Clear();
                        foreach (var it in items)
                        {
                            diagListPanel.Children.Add(DiagRow(it, delegate { if (refreshDiagList != null) refreshDiagList(); }));
                        }
                        diagRefreshBtn.IsEnabled = true;
                    });
                });
            };
            diagRefreshBtn.Click += (s, e) => refreshDiagList();

            // Первая загрузка — асинхронно
            ThreadPool.QueueUserWorkItem(delegate
            {
                var items = Core.RunDiagnostics();
                Dispatcher.Invoke((Action)delegate
                {
                    diagListPanel.Children.Clear();
                    foreach (var it in items)
                    {
                        diagListPanel.Children.Add(DiagRow(it, delegate { if (refreshDiagList != null) refreshDiagList(); }));
                    }
                });
            });

            panel.Children.Add(diagListPanel);

            return panel;
        }

        UIElement DiagRow(DiagItem it, Action reload)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(UI.T(it.Name, Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock
            {
                Text = it.Value,
                Foreground = it.Sev == Sev.Err ? Theme.BrErr : it.Sev == Sev.Warn ? Theme.BrWarn : Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(sp, 0);
            g.Children.Add(sp);

            // Кнопки быстрого исправления для специфических параметров
            if (it.Name == Loc.T("diag.n.tcpTs") && it.Sev == Sev.Warn)
            {
                var fixTsBtn = Ctl.Button(Loc.T("check.diag.fixTimestamps"), Icons.Bolt, 0);
                fixTsBtn.VerticalAlignment = VerticalAlignment.Center;
                fixTsBtn.Margin = new Thickness(8, 0, 0, 0);
                fixTsBtn.Click += (s, e) =>
                {
                    if (Core.EnableTcpTimestamps())
                    {
                        _win.ShowToast(Loc.T("check.diag.fixTimestampsOk"), Sev.Ok);
                        Core.Good(Loc.T("check.diag.fixTimestampsOk"));
                        if (reload != null) reload();
                    }
                };
                Grid.SetColumn(fixTsBtn, 1);
                g.Children.Add(fixTsBtn);
            }
            else if (it.Name == Loc.T("diag.n.proxy") && it.Sev == Sev.Warn)
            {
                var fixProxyBtn = Ctl.Button(Loc.T("check.diag.fixProxy"), Icons.Trash, 1);
                fixProxyBtn.VerticalAlignment = VerticalAlignment.Center;
                fixProxyBtn.Margin = new Thickness(8, 0, 0, 0);
                fixProxyBtn.Click += (s, e) =>
                {
                    if (Core.DisableSystemProxy())
                    {
                        _win.ShowToast(Loc.T("check.diag.fixProxyOk"), Sev.Ok);
                        Core.Good(Loc.T("check.diag.fixProxyOk"));
                        if (reload != null) reload();
                    }
                };
                Grid.SetColumn(fixProxyBtn, 1);
                g.Children.Add(fixProxyBtn);
            }

            var pill = Pill.Make(it.Sev, it.Sev == Sev.Ok ? Loc.T("common.ok") : it.Sev == Sev.Warn ? Loc.T("common.warning") : it.Sev == Sev.Err ? Loc.T("common.error") : Loc.T("common.info"));
            pill.VerticalAlignment = VerticalAlignment.Center;
            pill.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(pill, 2);
            g.Children.Add(pill);

            var card = UI.Card(g, new Thickness(14, 10, 14, 10));
            card.Margin = new Thickness(0, 0, 0, 6);
            return card;
        }
    }
}
