using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace ZapretStudio
{
    class LogPage : Page
    {
        public override string Title { get { return Loc.T("log.title"); } }
        public override string Subtitle { get { return Loc.T("log.sub"); } }

        readonly MainWindow _win;

        // ---- Виртуализированный список ----
        // Раньше строки жили в StackPanel: тысячи визуальных элементов тормозили
        // интерфейс. ListBox + VirtualizingStackPanel создаёт контейнеры только
        // для видимых строк.
        ListBox _list;
        readonly List<LogRow> _rows = new List<LogRow>();
        TextBox _search;
        bool _paused, _autoscroll = true;
        Sev? _levelFilter;
        StackPanel _filterBar;
        TextBlock _count;

        // Строка журнала: готовые к биндингу значения (VM).
        // ВАЖНО: только свойства — WPF Binding не привязывается к полям
        // (тихо даёт пустые значения, строки схлопываются в нулевую высоту).
        class LogRow
        {
            public DateTime Time { get; set; }
            public Sev Level { get; set; }
            public string Text { get; set; }
            public string TimeText { get; set; }
            public string LevelText { get; set; }
            public Brush LevelBrush { get; set; }   // замороженная кисть уровня
            public Brush RowBg { get; set; }        // замороженный фон полосы слева
            public Brush RowBorder { get; set; }    // замороженная обводка полосы слева
        }

        public LogPage(MainWindow win)
        {
            _win = win;
            BuildToolbar();
            BuildFilterBar();
            BuildList();
            var card = new Border { Child = _list, CornerRadius = Theme.R10, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), Margin = new Thickness(0, 8, 0, 0), ClipToBounds = true };
            // Адаптивная высота: Grid со star-строкой вместо фиксированных пикселей.
            var host = new Grid();
            host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(card, 1);
            host.Children.Add(card);
            Body.Children.Add(host);
            Core.OnLog -= OnLog;
            Core.OnLog += OnLog;
        }

        void BuildList()
        {
            _list = new ListBox
            {
                Background = Theme.BrBgDeep,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 12, 16, 12),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                FocusVisualStyle = null
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_list, ScrollBarVisibility.Disabled);
            // Виртуализация: контейнеры создаются только для видимых строк.
            VirtualizingPanel.SetIsVirtualizing(_list, true);
            VirtualizingPanel.SetCacheLengthUnit(_list, VirtualizationCacheLengthUnit.Page);
            VirtualizingPanel.SetCacheLength(_list, new VirtualizationCacheLength(1));
            _list.ItemTemplate = MakeRowTemplate();
            // Убираем стандартную подсветку выделения — журнал не «выбирается».
            _list.ItemContainerStyle = MakeContainerStyle();
        }

        static Style MakeContainerStyle()
        {
            var st = new Style(typeof(ListBoxItem));
            st.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            var tmpl = new ControlTemplate(typeof(ListBoxItem));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.MarginProperty, new Thickness(0, 0, 0, 4));
            tmpl.VisualTree = presenter;
            st.Setters.Add(new Setter(TemplateProperty, tmpl));
            return st;
        }

        static DataTemplate MakeRowTemplate()
        {
            var t = new DataTemplate();

            var borderF = new FrameworkElementFactory(typeof(Border), "bd");
            borderF.SetValue(Border.CornerRadiusProperty, Theme.R6);
            borderF.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7));
            borderF.SetValue(Border.BorderThicknessProperty, new Thickness(2, 0, 0, 0));
            // Фон/обводка строки — из VM (замороженные снимки палитры на момент события).
            borderF.SetValue(Border.BackgroundProperty, new Binding("RowBg"));
            borderF.SetValue(Border.BorderBrushProperty, new Binding("RowBorder"));

            var g = new FrameworkElementFactory(typeof(Grid));
            g.AppendChild(Col(GridLength.Auto));
            g.AppendChild(Col(GridLength.Auto));
            g.AppendChild(Col(new GridLength(1, GridUnitType.Star)));

            var time = Col<TextBlock>(0);
            time.SetValue(TextBlock.TextProperty, new Binding("TimeText"));
            time.SetValue(TextBlock.ForegroundProperty, Theme.Frozen(Theme.TextFaint));
            time.SetValue(TextBlock.FontSizeProperty, Theme.FsSmall);
            time.SetValue(TextBlock.FontFamilyProperty, Theme.MonoFont);
            time.SetValue(FrameworkElement.WidthProperty, 68.0);
            g.AppendChild(time);

            var level = Col<TextBlock>(1);
            level.SetValue(TextBlock.TextProperty, new Binding("LevelText"));
            level.SetValue(TextBlock.ForegroundProperty, new Binding("LevelBrush"));
            level.SetValue(TextBlock.FontSizeProperty, Theme.FsTiny);
            level.SetValue(TextBlock.FontFamilyProperty, Theme.MonoFont);
            level.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            level.SetValue(FrameworkElement.WidthProperty, 48.0);
            level.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 1, 10, 0));
            g.AppendChild(level);

            var msg = Col<TextBlock>(2);
            msg.SetValue(TextBlock.TextProperty, new Binding("Text"));
            msg.SetValue(TextBlock.ForegroundProperty, Theme.Frozen(Theme.Text));
            msg.SetValue(TextBlock.FontSizeProperty, Theme.FsSmall);
            msg.SetValue(TextBlock.FontFamilyProperty, Theme.MonoFont);
            msg.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            g.AppendChild(msg);

            borderF.AppendChild(g);
            t.VisualTree = borderF;
            return t;
        }

        static FrameworkElementFactory Col(GridLength len)
        {
            return new FrameworkElementFactory(typeof(ColumnDefinition))
                .Set(ColumnDefinition.WidthProperty, len);
        }

        static FrameworkElementFactory Col<T>(int column) where T : FrameworkElement, new()
        {
            var f = new FrameworkElementFactory(typeof(T));
            f.SetValue(Grid.ColumnProperty, column);
            return f;
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
            pause.Click += (s, e) => { _paused = !_paused; Ctl.SetButtonText(pause, _paused ? Loc.T("log.resume") : Loc.T("log.pause")); };
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

            var winwsOut = Ctl.Button(Loc.T("log.winwsOut"), Icons.Terminal, 1);
            winwsOut.Margin = new Thickness(0, 0, 10, 10);
            winwsOut.Click += (s, e) => CopyWinwsOutput();
            wrap.Children.Add(winwsOut);

            var clear = Ctl.Button(Loc.T("log.clear"), Icons.Cross, 2);
            clear.Margin = new Thickness(0, 0, 10, 10);
            clear.Click += (s, e) => { lock (Core.Log) Core.Log.Clear(); RebuildAll(); };
            wrap.Children.Add(clear);

            _count = UI.Mono("", Theme.FsTiny, Theme.BrFaint);
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
                    AddLine(MakeRow(e));
                    UpdateCount();
                    if (_autoscroll && _rows.Count > 0) _list.ScrollIntoView(_rows[_rows.Count - 1]);
                });
            }
            catch { }
        }

        LogRow MakeRow(LogEvent e)
        {
            var accent = UI2.SevColor(e.Level);
            return new LogRow
            {
                Time = e.Time,
                Level = e.Level,
                Text = e.Text,
                TimeText = e.Time.ToString("HH:mm:ss"),
                LevelText = LevelName(e.Level),
                LevelBrush = Theme.Frozen(accent),
                RowBg = Theme.Alpha(accent, 10),
                RowBorder = Theme.Alpha(accent, 54)
            };
        }

        void AddLine(LogRow row)
        {
            _rows.Add(row);
            // Виртуализация рисует только видимое; ограничиваем лишь саму коллекцию.
            if (_rows.Count > MaxRows) _rows.RemoveAt(0);
            _list.Items.Add(row);
            while (_list.Items.Count > MaxRows) _list.Items.RemoveAt(0);
        }

        bool Match(LogEvent e)
        {
            if (_levelFilter.HasValue && e.Level != _levelFilter.Value) return false;
            string q = (_search.Text ?? "").Trim().ToLowerInvariant();
            if (q.Length > 0 && e.Text.ToLowerInvariant().IndexOf(q) < 0) return false;
            return true;
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
            _count.Text = string.Format(Loc.T("log.events"), _rows.Count)
                + (_paused ? " - " + Loc.T("log.paused") : " - " + Loc.T("log.live"));
        }

        // Коллекция строк UI: виртуализация делает рендер дешёвым, лимит держим
        // только ради памяти самих объектов.
        const int MaxRows = 4000;

        void RebuildAll()
        {
            _rows.Clear();
            _list.Items.Clear();
            lock (Core.Log)
                foreach (var e in Core.Log)
                    if (Match(e)) AddLine(MakeRow(e));
            UpdateCount();
            if (_autoscroll && _rows.Count > 0) _list.ScrollIntoView(_rows[_rows.Count - 1]);
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
                string path = System.IO.Path.Combine(SafeRootForOutputs(), "zapret-gui-log.txt");
                System.IO.File.WriteAllText(path, AllText());
                Core.Good(string.Format(Loc.T("log.saved"), path));
                Core.OpenFile(path);
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("log.saveErr"), ex.Message)); }
        }

        static string SafeRootForOutputs()
        {
            return string.IsNullOrEmpty(Core.Root) ? AppDomain.CurrentDomain.BaseDirectory : Core.Root;
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

        void CopyWinwsOutput()
        {
            try
            {
                string d = Core.WinwsLogAll();
                if (string.IsNullOrWhiteSpace(d))
                {
                    Core.Warn(Loc.T("log.winwsEmpty"));
                    return;
                }
                Clipboard.SetText(d);
                Core.Good(Loc.T("log.winwsCopied"));
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("log.diagErr"), ex.Message)); }
        }
    }

    // Мини-хелперы для компактной сборки шаблона из кода.
    internal static class TemplateEx
    {
        public static FrameworkElementFactory Set(this FrameworkElementFactory f, DependencyProperty p, object v)
        { f.SetValue(p, v); return f; }
    }
}
