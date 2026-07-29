using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZapretStudio
{
    class LogPage : Page
    {
        public override string Title { get { return Loc.T("log.title"); } }
        public override string Subtitle { get { return Loc.T("log.sub"); } }

        readonly MainWindow _win;
        StackPanel _lines;
        ScrollViewer _sv;
        TextBox _search;
        bool _paused, _autoscroll = true;
        Sev? _levelFilter;
        StackPanel _filterBar;
        TextBlock _count;

        public LogPage(MainWindow win)
        {
            _win = win;
            BuildToolbar();
            BuildFilterBar();
            _lines = new StackPanel();
            _sv = new ScrollViewer { Content = _lines, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Theme.BrBgDeep, Padding = new Thickness(12) };
            var card = new Border { Child = _sv, CornerRadius = Theme.R10, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 8, 0, 0) };
            // Адаптивная высота: Grid с star-строкой вместо фиксированных 460px.
            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(card, 1);
            host.Children.Add(card);
            Body.Children.Add(host);
            Core.OnLog -= OnLog;
            Core.OnLog += OnLog;
        }

        public override void OnShow() { RebuildAll(); }
        public override void OnHide() { Core.OnLog -= OnLog; }

        void BuildToolbar()
        {
            var wrap = new WrapPanel();
            var sb = new Border { Background = Theme.BrSurface, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), CornerRadius = Theme.R10, Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 10, 10) };
            var sg = new StackPanel { Orientation = Orientation.Horizontal };
            sg.Children.Add(UI.Icon(Icons.Search, 16, Theme.BrMuted, 1.8));
            _search = new TextBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Foreground = Theme.BrText, CaretBrush = Theme.BrText, FontSize = Theme.FsBody, FontFamily = Theme.UiFont,
                Width = 220, Height = 38, VerticalContentAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            Ctl.AutomationSetName(_search, Loc.T("log.search"));
            _search.TextChanged += (s, e) => RebuildAll();
            sg.Children.Add(_search);
            sb.Child = sg;
            wrap.Children.Add(sb);

            var pause = Ctl.Button(Loc.T("log.pause"), Icons.Stop, 3);
            pause.Margin = new Thickness(0, 0, 10, 10);
            pause.Click += (s, e) => { _paused = !_paused; ((TextBlock)((StackPanel)((Border)pause.Content).Child).Children[1]).Text = _paused ? Loc.T("log.resume") : Loc.T("log.pause"); };
            wrap.Children.Add(pause);

            var scroll = Ctl.Button(Loc.T("log.autoscroll"), Icons.Down, 3);
            scroll.Margin = new Thickness(0, 0, 10, 10);
            scroll.Click += (s, e) => { _autoscroll = !_autoscroll; };
            wrap.Children.Add(scroll);

            var copy = Ctl.Button(Loc.T("log.copyBtn"), Icons.Copy, 1);
            copy.Margin = new Thickness(0, 0, 10, 10);
            copy.Click += (s, e) => CopyAll();
            wrap.Children.Add(copy);

            var save = Ctl.Button(Loc.T("log.save"), Icons.Save, 1);
            save.Margin = new Thickness(0, 0, 10, 10);
            save.Click += (s, e) => SaveAll();
            wrap.Children.Add(save);

            var diag = Ctl.Button(Loc.T("log.diag"), Icons.Shield, 1);
            diag.Margin = new Thickness(0, 0, 10, 10);
            diag.Click += (s, e) => CopyDiag();
            wrap.Children.Add(diag);

            var clear = Ctl.Button(Loc.T("log.clear"), Icons.Cross, 2);
            clear.Margin = new Thickness(0, 0, 10, 10);
            clear.Click += (s, e) => { lock (Core.Log) Core.Log.Clear(); RebuildAll(); };
            wrap.Children.Add(clear);

            _count = UI.Mono("0 events", Theme.FsTiny, Theme.BrFaint);
            _count.VerticalAlignment = VerticalAlignment.Center;
            _count.Margin = new Thickness(2, 0, 0, 10);
            wrap.Children.Add(_count);

            Body.Children.Add(wrap);
        }

        void BuildFilterBar()
        {
            _filterBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
            _filterBar.Children.Add(FilterChip(null, Loc.T("strat.all")));
            _filterBar.Children.Add(FilterChip(Sev.Info, "Info"));
            _filterBar.Children.Add(FilterChip(Sev.Ok, "OK"));
            _filterBar.Children.Add(FilterChip(Sev.Warn, "Warn"));
            _filterBar.Children.Add(FilterChip(Sev.Err, "Error"));
            Body.Children.Add(_filterBar);
        }

        Button FilterChip(Sev? level, string label)
        {
            var b = new Button { Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 6, 0) };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R8, Padding = new Thickness(10, 4, 10, 4),
                BorderThickness = new Thickness(1) };
            var tb = new TextBlock { Text = label, FontSize = Theme.FsSmall, FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold };
            bd.Child = tb;
            b.Content = bd;
            Action paint = delegate {
                bool on = _levelFilter == level;
                bd.Background = on ? Theme.BrAccent : Theme.BrSurface;
                bd.BorderBrush = on ? Theme.BrAccent : Theme.BrStroke;
                tb.Foreground = on ? Theme.BrOnAccent : Theme.BrMuted;
            };
            paint();
            b.Tag = paint;
            b.Click += (s, e) => { _levelFilter = level; RepaintFilters(); RebuildAll(); };
            return b;
        }

        void RepaintFilters()
        {
            if (_filterBar == null) return;
            foreach (var child in _filterBar.Children)
            {
                var b = child as Button;
                if (b != null && b.Tag is Action) ((Action)b.Tag)();
            }
        }

        void OnLog(LogEvent e)
        {
            if (_paused) return;
            try
            {
                Dispatcher.Invoke((Action)delegate
                {
                    if (!Match(e)) return;
                    _lines.Children.Add(LineFor(e));
                    UpdateCount();
                    if (_autoscroll) _sv.ScrollToEnd();
                });
            }
            catch { }
        }

        bool Match(LogEvent e)
        {
            if (_levelFilter.HasValue && e.Level != _levelFilter.Value) return false;
            string q = (_search.Text ?? "").Trim().ToLowerInvariant();
            if (q.Length > 0 && e.Text.ToLowerInvariant().IndexOf(q) < 0) return false;
            return true;
        }

        UIElement LineFor(LogEvent e)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var time = new TextBlock { Text = e.Time.ToString("HH:mm:ss.fff"), Foreground = Theme.BrFaint,
                FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, Width = 94, VerticalAlignment = VerticalAlignment.Top };
            Grid.SetColumn(time, 0); g.Children.Add(time);
            var level = new TextBlock { Text = LevelName(e.Level), Foreground = UI2.SevBrush(e.Level),
                FontSize = Theme.FsTiny, FontFamily = Theme.MonoFont, FontWeight = FontWeights.Bold, Width = 48,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 10, 0) };
            Grid.SetColumn(level, 1); g.Children.Add(level);
            var msg = new TextBlock { Text = e.Text, Foreground = Theme.BrText, FontSize = Theme.FsSmall,
                FontFamily = Theme.MonoFont, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(msg, 2); g.Children.Add(msg);
            var accent = UI2.SevColor(e.Level);
            return new Border { Child = g, Margin = new Thickness(0, 0, 0, 3), Padding = new Thickness(9, 7, 9, 7),
                Background = Theme.Alpha(accent, 10), BorderBrush = Theme.Alpha(accent, 54),
                BorderThickness = new Thickness(2, 0, 0, 0), CornerRadius = Theme.R6 };
        }

        static string LevelName(Sev level)
        {
            if (level == Sev.Ok) return "OK";
            if (level == Sev.Warn) return "WARN";
            if (level == Sev.Err) return "ERROR";
            if (level == Sev.Progress) return "WORK";
            return "INFO";
        }

        void UpdateCount()
        {
            if (_count == null) return;
            _count.Text = _lines.Children.Count + " events" + (_paused ? " - paused" : " - live");
        }

        void RebuildAll()
        {
            _lines.Children.Clear();
            lock (Core.Log)
                foreach (var e in Core.Log)
                    if (Match(e)) _lines.Children.Add(LineFor(e));
            UpdateCount();
            if (_autoscroll) _sv.ScrollToEnd();
        }

        string AllText()
        {
            var sb = new System.Text.StringBuilder();
            lock (Core.Log)
                foreach (var e in Core.Log)
                    sb.AppendLine(e.Time.ToString("HH:mm:ss") + " [" + e.Level + "] " + e.Text);
            return sb.ToString();
        }

        void CopyAll()
        {
            try { Clipboard.SetText(AllText()); Core.Good(Loc.T("log.copied")); }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("log.copyErr"), ex.Message)); }
        }

        void SaveAll()
        {
            try
            {
                string path = System.IO.Path.Combine(Core.Root, "zapret-gui-log.txt");
                System.IO.File.WriteAllText(path, AllText());
                Core.Good(string.Format(Loc.T("log.saved"), path));
                Core.OpenFile(path);
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("log.saveErr"), ex.Message)); }
        }

        void CopyDiag()
        {
            try
            {
                string d = Core.Diagnostics();
                Clipboard.SetText(d);
                Core.Good(Loc.T("log.diagCopied"));
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("log.diagErr"), ex.Message)); }
        }
    }
}
