using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        // Подписи табов по порядку Children полоски — по ним CellRow меряет
        // естественную ширину таба (без переноса) для режима Justify.
        readonly System.Collections.Generic.List<TextBlock> _tabLabels = new System.Collections.Generic.List<TextBlock>();
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

        public override void OnShow()
        {
            if (_running > 0 || _stratBusy) { EnsureStopEnabled(); return; }
            ShowTab(_tab);
        }

        void EnsureStopEnabled()
        {
            if (_btnStop != null) _btnStop.IsEnabled = _running > 0;
            if (_stratStopBtn != null) _stratStopBtn.IsEnabled = _stratBusy;
        }

        public void CancelAll()
        {
            _stop = true;
            _stratCancel = true;
        }

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

            // Полоса табов: табы компактные (по ширине иконки с подписью), но
            // распределены по всей ширине бара с равными зазорами — Justify.
            // Равные колонки растягивали пилюлю и на широком окне иконка
            // разъезжалась с подписью, а сгруппированная полоса оставляла пустое
            // поле у краёв бара. Не хватает ширины — откат к равным колонкам.
            var tabs = new CellRow { Gap = 6, EvenRows = false, MinCell = 0, ForceCols = 5,
                Justify = true, Labels = _tabLabels };
            tabs.Children.Add(TabButton("targets", Icons.List, Loc.T("check.tab.targets")));
            tabs.Children.Add(TabButton("popular", Icons.Globe, Loc.T("check.tab.popular")));
            tabs.Children.Add(TabButton("games", Icons.Game, Loc.T("check.tab.games")));
            tabs.Children.Add(TabButton("strats", Icons.Bolt, Loc.T("check.tab.strats")));
            tabs.Children.Add(TabButton("diag", Icons.Tool, Loc.T("check.tab.diag")));

            barContainer.Child = tabs;
            Body.Children.Add(barContainer);
        }

        Button TabButton(string key, string icon, string label)
        {
            var b = new Button { Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R8, Padding = new Thickness(12, 7, 12, 7),
                Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1) };
            // Иконка Auto + подпись Auto, вся сетка по центру ячейки: Star-колонка
            // брала на себя всю ширину таба, и на широком окне подпись уезжала от
            // иконки в центр. Auto-колонка даёт ровно ширину текста.
            var sp = new Grid { HorizontalAlignment = HorizontalAlignment.Center };
            sp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ic = UI.Icon(icon, 15, Theme.BrMuted, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ic, 0);
            sp.Children.Add(ic);
            var tb = new TextBlock { Text = label, Foreground = Theme.BrMuted, FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(7, 0, 0, 0), TextWrapping = TextWrapping.Wrap };
            _tabLabels.Add(tb);
            // Без переноса узкая ячейка резала бы подпись: WPF ужимает Auto-колонки
            // пропорционально, и доставалось бы тоже иконке. MaxWidth считается от
            // ширины таба, которую задаёт ряд (внешняя величина), а не от ширины
            // сетки: сетка меряется по тексту, и на каждом проходе разметки текст
            // оказывался бы уже предыдущего — до одной буквы в строке.
            // 24 паддинг + 2 рамка + 15 иконка + 7 отступ подписи = 48, ещё 1 px
            // остаётся на округление метрик: в компактном режиме ячейка равна
            // естественной ширине текста, и запас не даёт подписи свернуться в
            // две строки ровно там, где она влезает в одну.
            b.SizeChanged += (s, e) => { tb.MaxWidth = Math.Max(20, b.ActualWidth - 48); };
            Grid.SetColumn(tb, 1);
            sp.Children.Add(tb);
            bd.Child = sp;
            b.Content = bd;
            b.Tag = new object[] { bd, ic, tb };
            Ctl.AutomationSetName(b, label);
            b.Click += (s, e) => ShowTab(key);
            b.MouseEnter += (s, e) =>
            {
                if (key != _tab)
                {
                    bd.Background = Theme.Alpha(Theme.Text, 14);
                    if (Theme.Mode == ThemeMode.System24)
                    {
                        if (Theme.AnimationsEnabled)
                        {
                            var bAnim = new ColorAnimation(Colors.Transparent, Theme.AccentMain, TimeSpan.FromMilliseconds(200))
                            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                            var brush = new SolidColorBrush(Colors.Transparent);
                            bd.BorderBrush = brush;
                            brush.BeginAnimation(SolidColorBrush.ColorProperty, bAnim);
                        }
                        else bd.BorderBrush = Theme.BrAccent;
                    }
                }
            };
            b.MouseLeave += (s, e) =>
            {
                if (key != _tab)
                {
                    bd.Background = Brushes.Transparent;
                    if (Theme.Mode == ThemeMode.System24)
                    {
                        if (Theme.AnimationsEnabled)
                        {
                            var bAnim = new ColorAnimation(Theme.AccentMain, Colors.Transparent, TimeSpan.FromMilliseconds(200))
                            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                            var brush = new SolidColorBrush(Theme.AccentMain);
                            bd.BorderBrush = brush;
                            brush.BeginAnimation(SolidColorBrush.ColorProperty, bAnim);
                        }
                        else bd.BorderBrush = Brushes.Transparent;
                    }
                    else bd.BorderBrush = Brushes.Transparent;
                }
            };
            b.GotKeyboardFocus += (s, e) =>
            {
                bd.BorderBrush = Theme.BrAccentHi;
                bd.BorderThickness = new Thickness(2);
                if (key != _tab) bd.Background = Theme.Alpha(Theme.Text, 14);
            };
            b.LostKeyboardFocus += (s, e) =>
            {
                bd.BorderThickness = new Thickness(1);
                if (key != _tab)
                {
                    bd.BorderBrush = Brushes.Transparent;
                    bd.Background = Brushes.Transparent;
                }
                else
                {
                    bd.BorderBrush = Theme.BrAccent;
                }
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
                bd.BorderThickness = new Thickness(1);
                bd.BorderBrush = (on && Theme.Mode == ThemeMode.System24) ? Theme.BrAccent : Brushes.Transparent;
                ic.Stroke = on ? Theme.BrOnAccent : Theme.BrMuted;
                tb.Foreground = on ? Theme.BrOnAccent : Theme.BrMuted;
            }
        }

        internal void ShowTab(string key)
        {
            _tab = key;
            PaintTabs();
            UIElement content;
            if (key == "strats") content = BuildStratTab();
            else if (key == "diag") content = BuildDiagTab();
            else content = BuildListTab(key);

            _tabsHost.Child = content;
            // В system24 переходы строже — только проявление, без сдвига.
            UiAnim.Appear(content, Theme.Mode == ThemeMode.System24 ? 0 : 8, 200, 240);
        }

        UIElement BuildListTab(string which)
        {
            var panel = new StackPanel();

            // Панель инструментов
            _btnAll = Ctl.Button(Loc.T("check.checkAll"), Icons.Pulse, 0);
            _btnAll.Click += (s, e) => Start(false);
            _btnSel = Ctl.Button(Loc.T("check.checkSel"), Icons.Check, 1);
            _btnSel.Click += (s, e) => Start(true);
            _btnStop = Ctl.Button(Loc.T("common.stop"), Icons.Stop, 2);
            _btnStop.IsEnabled = false;
            _btnStop.Click += (s, e) => { _stop = true; };
            _btnExport = Ctl.Button(Loc.T("check.export"), Icons.Save, 1);
            _btnExport.Click += (s, e) => Export();
            panel.Children.Add(Ctl.ButtonRow(_btnAll, _btnSel, _btnStop, _btnExport));

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

        static void StartCardShimmer(Border card)
        {
            if (card == null || !Theme.AnimationsEnabled || Theme.Mode == ThemeMode.System24) return;
            try
            {
                var tt = new TranslateTransform(-1.2, -1.2);
                Color baseC = Theme.Surface;
                Color accentTint = (Theme.Mode == ThemeMode.Light)
                    ? Theme.AccentDim
                    : Theme.AccentHi;
                Color peakC = Theme.Blend(Theme.Surface, accentTint, 0.22);

                var lgb = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    MappingMode = BrushMappingMode.RelativeToBoundingBox,
                    RelativeTransform = tt,
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(baseC, 0.0),
                        new GradientStop(baseC, 0.35),
                        new GradientStop(peakC, 0.50),
                        new GradientStop(baseC, 0.65),
                        new GradientStop(baseC, 1.0)
                    }
                };

                var animX = new DoubleAnimation(-1.2, 1.2, TimeSpan.FromMilliseconds(1300))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                var animY = new DoubleAnimation(-1.2, 1.2, TimeSpan.FromMilliseconds(1300))
                {
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                tt.BeginAnimation(TranslateTransform.XProperty, animX);
                tt.BeginAnimation(TranslateTransform.YProperty, animY);
                card.Background = lgb;
            }
            catch { }
        }

        static void StopCardShimmer(Border card)
        {
            if (card == null) return;
            try
            {
                if (card.Background is LinearGradientBrush)
                {
                    card.Background = Theme.BrSurface;
                }
            }
            catch { }
        }

        void SetRow(Endpoint r, string state, string latency, string detail, string when)
        {
            if (state == "checking") StartCardShimmer(r.Card);
            else StopCardShimmer(r.Card);

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
                    res = Core.TestTarget(row.T, timeout, delegate { return _stop; });
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
            // Тот же ButtonRow, что и вверху страницы: один ряд, равные ячейки,
            // край совпадает с краем блока. WrapPanel переносил кнопки как попало
            // и оставлял их разной ширины.
            _stratRunSel = Ctl.Button(Loc.T("check.strat.runSel"), Icons.Check, 0);
            _stratRunSel.Click += (s, e) => RunBatch(true);
            _stratRunAll = Ctl.Button(Loc.T("check.strat.runAll"), Icons.Bolt, 1);
            _stratRunAll.Click += (s, e) => RunBatch(false);
            _stratSelAll = Ctl.Button(Loc.T("check.strat.selectAll"), Icons.Grid, 3);
            _stratSelAll.Click += (s, e) => SetAllSel(true);
            _stratClear = Ctl.Button(Loc.T("check.strat.clearSel"), Icons.Cross, 3);
            _stratClear.Click += (s, e) => SetAllSel(false);
            _stratStopBtn = Ctl.Button(Loc.T("common.stop"), Icons.Stop, 2);
            _stratStopBtn.IsEnabled = false;
            _stratStopBtn.Click += (s, e) => { _stratCancel = true; };
            var autoPick = Ctl.Button(Loc.T("check.strat.autoPick"), Icons.Star, 0);
            autoPick.Click += (s, e) => AutoPick();
            panel.Children.Add(Ctl.ButtonRow(_stratRunSel, _stratRunAll, _stratSelAll,
                _stratClear, _stratStopBtn, autoPick));

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
            public Sev LastSev = Sev.Neutral;   // состояние после последнего прогона
            public string LastText;             // текст плашки этого состояния
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
            rr.Card.Margin = new Thickness(0, 0, 0, Theme.Dense(8));
            rr.Card.Tag = rr;
            _stratRows.Add(rr);
            return rr.Card;
        }

        void SetStratPill(StratRowRef rr, Sev sev, string text)
        {
            rr.LastSev = sev;
            rr.LastText = text;
            var np = Pill.Make(sev, text);
            np.VerticalAlignment = VerticalAlignment.Center;
            np.Margin = new Thickness(14, 0, 12, 0);
            var g = (Grid)rr.Card.Child;
            int idx = g.Children.IndexOf(rr.Pill);
            Grid.SetColumn(np, 2);
            if (idx >= 0) { g.Children.RemoveAt(idx); g.Children.Insert(idx, np); }
            rr.Pill = np;
        }

        // Цели для проверки стратегии берутся из utils/targets.txt — того же файла,
        // который читает utils/test zapret.ps1 (его запускает service.bat →
        // «Проверить стратегии»). Раньше здесь был хардкод из пяти адресов, поэтому
        // приложение судило о стратегии по другому набору целей и писало
        // «не работает» там, где service.bat показывал рабочую.
        //
        // PING-цели в вердикт не идут: winws перехватывает только TCP/UDP
        // (--wf-tcp/--wf-udp), ICMP через десинк не проходит вообще, и test
        // zapret.ps1 оценивает стратегию тоже по HTTP-жетонам (пинг у него —
        // лишь тай-брейкер при равном счёте). Доступность DNS видна во вкладке
        // «Проверка соединения», там targets.txt читается целиком.
        // Реальные проверки, без подмены результатов.
        static List<Target> StratProbes()
        {
            var all = new List<Target>();
            try { foreach (var t in Core.LoadTargets()) all.Add(t); } catch { }

            var l = new List<Target>();
            foreach (var t in all) if (t.Kind != "PING") l.Add(t);
            if (l.Count > 0) return l;

            // utils/targets.txt отсутствует или в нём нет HTTP-целей — запасной
            // набор, тот же, что использует test zapret.ps1 при пустом файле.
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
            if (!_stratBusy) return;
            _stratBusy = false;
            if (_stratStopBtn != null) _stratStopBtn.IsEnabled = false;
            if (_stratRunSel != null) _stratRunSel.IsEnabled = true;
            if (_stratRunAll != null) _stratRunAll.IsEnabled = true;
            if (_stratSelAll != null) _stratSelAll.IsEnabled = true;
            if (_stratClear != null) _stratClear.IsEnabled = true;
            if (_stratRows != null) foreach (var rr in _stratRows) if (rr != null && rr.Run != null) rr.Run.IsEnabled = true;
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
            bool wasService = Core.ServiceState() == "running";
            bool wasManual = !wasService && Core.IsWinwsRunning();
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
                    if (_stratCancel) { Dispatcher.Invoke((Action)delegate { StopCardShimmer(rr.Card); SetStratPill(rr, Sev.Neutral, Loc.T("check.strat.stopped")); }); continue; }
                    var row = rr;
                    Dispatcher.Invoke((Action)delegate { StartCardShimmer(row.Card); SetStratPill(row, Sev.Progress, Loc.T("net.checking")); row.Result.Text = Loc.T("check.strat.launching"); row.LastReports = null; });

                    int ok = 0, total = probes.Count;
                    string err = null;
                    bool isAlreadyRunning = wasManual && string.Equals(prevStrat, row.File, StringComparison.OrdinalIgnoreCase) && Core.IsWinwsRunning();
                    var reports = new List<ProbeReport>();
                    object reportsLock = new object();
                    try
                    {
                        if (!isAlreadyRunning)
                        {
                            Core.KillWinws();
                            if (!Core.StartWinws(row.File)) throw new Exception("winws.exe failed to start");
                            // Прогрев WinDivert: 5 секунд, как в utils/test zapret.ps1
                            for (int i = 0; i < Core.ProbeWarmupMs / 100 && !_stratCancel; i++) Thread.Sleep(100);
                        }
                        else
                        {
                            Thread.Sleep(200);
                        }

                        // Проверка целей не более чем в 8 потоков — как ранспейсы
                        // в test zapret.ps1. Все сразу нельзя: CurlCheck плодит по
                        // 3 curl.exe на цель, и толпа процессов сама себе создаёт
                        // таймауты.
                        int missed;
                        bool allInTime = Core.RunProbes(probes, delegate(Target tt)
                        {
                            bool isOk = false;
                            string detail = "";
                            long ms = -1;
                            try
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
                            }
                        }, delegate { return _stratCancel; }, out missed);
                        // №15: число недособранных проб больше не игнорируется —
                        // иначе таблица молча показывала меньше строк, чем целей,
                        // и «не проверено» выглядело как «не работает».
                        if (!allInTime || missed > 0)
                            Core.Warn(string.Format(Loc.T("net.probesTimeout"), missed, probes.Count));
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
                        StopCardShimmer(row.Card);
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
                            summaryText = string.Format(Loc.T("check.strat.summaryOk"), okCount, total, string.Join(", ", okParts.ToArray()));
                        }
                        else if (okCount > 0)
                        {
                            summaryText = string.Format(Loc.T("check.strat.summaryPartial"), okCount, total, string.Join(", ", failParts.ToArray()));
                        }
                        else
                        {
                            summaryText = string.Format(Loc.T("check.strat.summaryFail"), total);
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
                    if (!_stratCancel) ShowComparison(queue);
                });
              } catch { }
              finally
              {
                  try
                  {
                      Dispatcher.Invoke((Action)delegate
                      {
                          EndBusy();
                          foreach (var r in queue) StopCardShimmer(r.Card);
                      });
                  } catch { }
                  Core.EndWinwsOperation();
                  if (wasService || wasManual)
                  {
                      Dispatcher.Invoke((Action)delegate { try { _win.RestartInSameMode(wasService, wasManual, prevStrat); } catch { } });
                  }
              }
            });
        }

        // Итоги последнего прогона. Показываем только проверенные стратегии
        // (одну — одну, три — три) компактными строками: плашка состояния,
        // имя и счёт целей. Полный текст с адресами не дублируем — он
        // остаётся в карточке стратегии и в её тултипе.
        Border _comparisonCard;

        void ShowComparison(List<StratRowRef> tested)
        {
            if (_stratRows == null || _stratList == null) return;
            if (tested == null) return;

            var items = new List<StratRowRef>();
            foreach (var rr in tested) if (rr != null) items.Add(rr);
            if (items.Count == 0) return;

            // Прошедшие — первыми, дальше по имени.
            items.Sort(delegate (StratRowRef a, StratRowRef b)
            {
                bool aOk = a.LastSev == Sev.Ok, bOk = b.LastSev == Sev.Ok;
                if (aOk && !bOk) return -1;
                if (!aOk && bOk) return 1;
                return string.Compare(Core.PrettyName(a.File), Core.PrettyName(b.File), StringComparison.OrdinalIgnoreCase);
            });

            var col = new StackPanel();
            if (items.Count > 1)
            {
                var head = UI.T(Loc.T("check.strat.comparison"), Theme.FsSmall, Theme.BrMuted);
                head.Margin = new Thickness(0, 0, 0, 8);
                col.Children.Add(head);
            }
            for (int i = 0; i < items.Count; i++)
            {
                var rr = items[i];
                var line = new StackPanel { Orientation = Orientation.Horizontal };
                if (i > 0) line.Margin = new Thickness(0, Theme.Dense(6), 0, 0);

                var p = Pill.Make(rr.LastSev, rr.LastText != null ? rr.LastText : "");
                p.VerticalAlignment = VerticalAlignment.Center;
                p.Margin = new Thickness(0, 0, 10, 0);
                line.Children.Add(p);

                var nm = UI.T(Core.PrettyName(rr.File), Theme.FsBody, Theme.BrText, FontWeights.SemiBold);
                nm.VerticalAlignment = VerticalAlignment.Center;
                line.Children.Add(nm);

                if (rr.LastReports != null && rr.LastReports.Count > 0)
                {
                    int okN = 0;
                    foreach (var rp in rr.LastReports) if (rp.Ok) okN++;
                    var cnt = UI.T(okN + "/" + rr.LastReports.Count, Theme.FsSmall, Theme.BrMuted);
                    cnt.VerticalAlignment = VerticalAlignment.Center;
                    cnt.Margin = new Thickness(8, 0, 0, 0);
                    line.Children.Add(cnt);
                }
                col.Children.Add(line);
            }

            if (_comparisonCard != null && _stratList.Children.Contains(_comparisonCard))
                _stratList.Children.Remove(_comparisonCard);

            _comparisonCard = UI.Card(col, new Thickness(14, 12, 14, 12));
            _comparisonCard.Margin = new Thickness(0, 0, 0, Theme.Dense(12));
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
            bool wasService = Core.ServiceState() == "running";
            bool wasManual = !wasService && Core.IsWinwsRunning();
            string prevStrat = _win.CurrentStrategyFile();
            bool bestSelected = false;
            string bestFile = null;
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
                                if (sc.Error)
                                {
                                    SetStratPill(rr, Sev.Err, Loc.T("check.strat.fail"));
                                    rr.Result.Text = sc.ErrorMessage ?? Loc.T("check.strat.notStarted");
                                }
                                else
                                {
                                    Sev sev = sc.Ok == sc.Total ? Sev.Ok : (sc.Ok > 0 ? Sev.Warn : Sev.Err);
                                    string label = sc.Ok == sc.Total ? Loc.T("check.strat.pass") : (sc.Ok > 0 ? Loc.T("check.strat.partial") : Loc.T("check.strat.fail"));
                                    SetStratPill(rr, sev, label);
                                    rr.Result.Text = string.Format(Loc.T("check.strat.resultLine"), sc.Ok, sc.Total);
                                }
                                break;
                            }
                        }
                    });
                }

                Dispatcher.Invoke((Action)delegate
                {
                    EndBusy();
                    if (_stratCancel) { Core.Info(Loc.T("check.strat.batchStopped")); return; }
                    // Выбираем лучшую: макс Ok, затем мин AvgMs (пропуская упавшие).
                    Core.StratScore best = null;
                    foreach (var sc in scores)
                    {
                        if (sc.Error || sc.Busy) continue;
                        if (best == null) { best = sc; continue; }
                        if (sc.Ok > best.Ok || (sc.Ok == best.Ok && sc.AvgMs < best.AvgMs)) best = sc;
                    }
                    if (best != null && best.Ok > 0)
                    {
                        bestSelected = true;
                        bestFile = best.File;
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
              finally
              {
                  try { Dispatcher.Invoke((Action)delegate { EndBusy(); }); } catch { }
                  Core.EndWinwsOperation();
                  if (wasService || wasManual)
                  {
                      string stratToRestore = bestSelected && !string.IsNullOrEmpty(bestFile) ? bestFile : prevStrat;
                      Dispatcher.Invoke((Action)delegate { try { _win.RestartInSameMode(wasService, wasManual, stratToRestore); } catch { } });
                  }
              }
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
                MinHeight = 62
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
                MinHeight = 62
            });
            var wdBtn = Ctl.Button(Loc.T("check.diag.wdReset"), Icons.Refresh, 1);
            wdBtn.HorizontalAlignment = HorizontalAlignment.Left;
            wdBtn.Click += (s, e) =>
            {
                wdBtn.IsEnabled = false;
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    string wdErr;
                    bool ok = Core.ResetWinDivertDriver(out wdErr);
                    try
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            wdBtn.IsEnabled = true;
                            if (ok)
                            {
                                _win.ShowToast(Loc.T("check.diag.wdResetOk"), Sev.Ok);
                                Core.Good(Loc.T("check.diag.wdResetOk"));
                            }
                            else
                            {
                                // Причина важнее факта: «занято» и «нужна перезагрузка»
                                // — это разные действия пользователя.
                                string msg = string.IsNullOrEmpty(wdErr) ? Loc.T("mw.busy") : wdErr;
                                _win.ShowToast(msg, Sev.Warn);
                                Core.Warn(msg);
                            }
                        });
                    }
                    catch { }
                });
            };
            wdPanel.Children.Add(wdBtn);
            var wdCard = UI.Card(wdPanel, new Thickness(16, 14, 16, 14));
            Grid.SetColumn(wdCard, 2);
            repairGrid.Children.Add(wdCard);

            panel.Children.Add(repairGrid);

            panel.Children.Add(new Border { Height = 14 });

            // 3. Параметры системы и окружения — загружаем АСИНХРОННО, чтобы вкладка открывалась мгновенно
            panel.Children.Add(SectionLabel(Loc.T("check.diag.sub")));

            var diagRefreshBtn = Ctl.Button(Loc.T("check.diag.run"), Icons.Refresh, 1);
            diagRefreshBtn.HorizontalAlignment = HorizontalAlignment.Left;
            diagRefreshBtn.Margin = new Thickness(0, 0, 0, 12);
            panel.Children.Add(diagRefreshBtn);

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
