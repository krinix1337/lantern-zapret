using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ZapretStudio
{
    class StrategiesPage : Page
    {
        public override string Title { get { return Loc.T("strategies.title"); } }
        public override string Subtitle { get { return Loc.T("strategies.sub"); } }

        readonly MainWindow _win;
        TextBox _search;
        WrapPanel _list;
        string _filterCat = "all";
        HashSet<string> _fav = new HashSet<string>();
        const double MinCardW = 250, Gap = 12;
        StackPanel _catBar;

        public StrategiesPage(MainWindow win)
        {
            _win = win;
            LoadFav();
            var hint = NoteCard(Icons.Info, Theme.BrAccent, Loc.T("strat.pickHint"), Sev.Info);
            hint.Margin = new Thickness(0, 0, 0, 12);
            Body.Children.Add(hint);
            BuildToolbar();
            _list = new WrapPanel();
            _list.SizeChanged += (s, e) => { if (Math.Abs(e.PreviousSize.Width - e.NewSize.Width) > 1) Relayout(); };
            Body.Children.Add(_list);
        }

        public override void OnShow() { Rebuild(); }

        void LoadFav()
        {
            string s = Core.Get("favorites", "");
            foreach (var f in s.Split('|')) if (f.Length > 0) _fav.Add(f);
        }
        void SaveFav()
        {
            Core.Set("favorites", string.Join("|", _fav.ToArray()));
            Core.SaveConfig();
        }

        void DoRecommend()
        {
            _win.ShowToast(Loc.T("strat.recommend.busy"), Sev.Info);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string isp = Core.DetectIsp();
                    string rec = Core.RecommendStrategy(isp);
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (rec == null)
                        {
                            _win.ShowToast(Loc.T("strat.recommend.fail"), Sev.Warn);
                            return;
                        }
                        string name = Core.PrettyName(rec);
                        string msg = string.IsNullOrEmpty(isp)
                            ? string.Format(Loc.T("strat.recommend.result"), name)
                            : string.Format(Loc.T("strat.recommend.isp"), isp, name);
                        _win.ShowToast(msg, Sev.Ok);
                        Core.Info(msg);
                    });
                }
                catch { }
            });
        }

        void BuildToolbar()
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // поиск
            var sb = new Border { Background = Theme.BrSurface, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), CornerRadius = Theme.R10, Padding = new Thickness(12, 0, 12, 0) };
            var sg = new StackPanel { Orientation = Orientation.Horizontal };
            sg.Children.Add(UI.Icon(Icons.Search, 16, Theme.BrMuted, 1.8));
            _search = new TextBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent,
                Foreground = Theme.BrText, CaretBrush = Theme.BrText, FontSize = Theme.FsBody, FontFamily = Theme.UiFont,
                Width = 260, VerticalContentAlignment = VerticalAlignment.Center, Height = 38, Margin = new Thickness(8, 0, 0, 0) };
            Ctl.AutomationSetName(_search, Loc.T("strat.search"));
            _search.TextChanged += (s, e) => Rebuild();
            sg.Children.Add(_search);
            sb.Child = sg;
            Grid.SetColumn(sb, 0);
            g.Children.Add(sb);

            // фильтр по категории — внутренний ключ + локализованная подпись
            var cats = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            cats.Children.Add(CatChip("all", Loc.T("strat.all")));
            cats.Children.Add(CatChip("General", "General"));
            cats.Children.Add(CatChip("ALT", "ALT"));
            cats.Children.Add(CatChip("FAKE", "FAKE"));
            cats.Children.Add(CatChip("fav", Loc.T("strat.favorites")));
            _catBar = cats;
            Grid.SetColumn(cats, 1);
            g.Children.Add(cats);
            Body.Children.Add(g);

            // Рекомендация по провайдеру
            var recBtn = Ctl.Button(Loc.T("strat.recommend"), Icons.Bolt, 1);
            recBtn.Margin = new Thickness(0, 0, 0, 10);
            recBtn.Click += (s, e) => DoRecommend();
            Body.Children.Add(recBtn);
        }

        Button CatChip(string cat, string label)
        {
            var b = new Button { Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(6, 0, 0, 0) };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R8, Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(1) };
            var tb = new TextBlock { Text = label, FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold };
            bd.Child = tb;
            b.Content = bd;
            Action paint = delegate {
                bool on = _filterCat == cat;
                bd.Background = on ? Theme.BrAccent : Theme.BrSurface;
                bd.BorderBrush = on ? Theme.BrAccent : Theme.BrStroke;
                tb.Foreground = on ? Theme.BrOnAccent : Theme.BrMuted;
            };
            paint();
            b.Tag = paint;
            b.Click += (s, e) => { _filterCat = cat; RepaintChips(); Rebuild(); };
            Ctl.AutomationSetName(b, Loc.T("strat.filterPrefix") + label);
            return b;
        }

        void RepaintChips()
        {
            if (_catBar == null) return;
            foreach (var child in _catBar.Children)
            {
                var b = child as Button;
                if (b != null && b.Tag is Action) ((Action)b.Tag)();
            }
        }

        void Rebuild()
        {
            _list.Children.Clear();
            var files = Core.GetStrategyFiles();
            string q = (_search.Text ?? "").Trim().ToLowerInvariant();
            string current = _win.CurrentStrategyFile();
            int shown = 0;

            foreach (var f in files)
            {
                string name = Core.PrettyName(f);
                string cat = Core.CategoryOf(f);
                if (_filterCat == "fav" && !_fav.Contains(f)) continue;
                else if (_filterCat != "all" && _filterCat != "fav" && cat != _filterCat) continue;
                if (q.Length > 0 && name.ToLowerInvariant().IndexOf(q) < 0) continue;
                _list.Children.Add(StrategyCard(f, name, cat, f == current));
                shown++;
            }

            if (shown == 0)
                _list.Children.Add(new TextBlock { Text = Loc.T("strat.nothing"), Foreground = Theme.BrMuted,
                    FontSize = Theme.FsBody, FontFamily = Theme.UiFont, Margin = new Thickness(2, 12, 0, 0) });
            Relayout();
        }

        // Пересчитать ширину карточек так, чтобы аккуратно заполнить сетку (адаптивно).
        // У каждой карточки правый отступ = Gap, поэтому «след» карточки = cardW + Gap.
        // Влезает cols карточек, когда cols*(cardW+Gap) <= avail. Считаем от минимума
        // и раздаём остаток по ширине, оставляя место под правый отступ каждой карточки.
        void Relayout()
        {
            double avail = _list.ActualWidth;
            if (avail <= 1) return;
            int cols = Math.Max(1, (int)((avail) / (MinCardW + Gap)));
            double cardW = (avail - Gap * cols) / cols - 1; // -1px запас от переполнения при округлении
            if (cardW < 40) cardW = 40;
            foreach (var child in _list.Children)
            {
                var b = child as Border;
                if (b == null) continue;
                b.Width = cardW;
            }
        }

        Border StrategyCard(string file, string name, string cat, bool isCurrent)
        {
            var outer = new StackPanel();

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var star = new Button { Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Width = 32, Height = 32, Margin = new Thickness(-4, -4, 8, -4) };
            Ctl.StripChrome(star);
            bool fav = _fav.Contains(file);
            var starBg = new Border { Width = 32, Height = 32, CornerRadius = Theme.R8,
                Background = fav ? Theme.Alpha(Theme.Warn, 34) : Brushes.Transparent };
            starBg.Child = UI.Icon(fav ? Icons.StarFilled : Icons.Star, 18, fav ? Theme.BrWarn : Theme.BrFaint, 1.6);
            star.Content = starBg;
            Ctl.AutomationSetName(star, (fav ? Loc.T("strat.favRemove") : Loc.T("strat.favAdd")) + name);
            star.ToolTip = fav ? Loc.T("strat.favRemove") + name : Loc.T("strat.favAdd") + name;
            star.MouseEnter += (s, e) => { if (!_fav.Contains(file)) starBg.Background = Theme.BrSurfaceHi; };
            star.MouseLeave += (s, e) => { if (!_fav.Contains(file)) starBg.Background = Brushes.Transparent; };
            star.Click += (s, e) =>
            {
                bool adding = !_fav.Contains(file);
                if (adding) _fav.Add(file); else _fav.Remove(file);
                SaveFav();
                // Ясный переключатель: залитая звезда и тёплая подложка означают избранное.
                starBg.Background = adding ? Theme.Alpha(Theme.Warn, 34) : Brushes.Transparent;
                var newIcon = UI.Icon(adding ? Icons.StarFilled : Icons.Star, 18, adding ? Theme.BrWarn : Theme.BrFaint, 1.6);
                starBg.Child = newIcon;
                star.ToolTip = (adding ? Loc.T("strat.favRemove") : Loc.T("strat.favAdd")) + name;
                // Анимация пульса
                var st = new ScaleTransform(1, 1);
                newIcon.RenderTransform = st;
                newIcon.RenderTransformOrigin = new Point(0.5, 0.5);
                var anim = new DoubleAnimation(1.0, 1.45, TimeSpan.FromMilliseconds(120))
                {
                    AutoReverse = true,
                    FillBehavior = FillBehavior.Stop
                };
                st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                // Тост
                _win.ShowToast(string.Format(adding ? Loc.T("strat.favAdded") : Loc.T("strat.favRemoved"), name),
                    adding ? Sev.Ok : Sev.Neutral);
                // Пересобрать список (с задержкой чтобы анимация была видна)
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
                timer.Tick += (s2, e2) => { timer.Stop(); Rebuild(); };
                timer.Start();
            };
            titleRow.Children.Add(star);
            var nameT = UI.T(name, Theme.FsBody, Theme.BrText, FontWeights.SemiBold);
            nameT.VerticalAlignment = VerticalAlignment.Center;
            nameT.TextTrimming = TextTrimming.CharacterEllipsis;
            titleRow.Children.Add(nameT);
            outer.Children.Add(titleRow);

            var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            badgeRow.Children.Add(CatBadge(cat));
            if (isCurrent)
            {
                var cur = Pill.Make(Sev.Ok, Loc.T("strat.current"));
                cur.Margin = new Thickness(8, 0, 0, 0);
                cur.VerticalAlignment = VerticalAlignment.Center;
                badgeRow.Children.Add(cur);
            }
            outer.Children.Add(badgeRow);

            outer.Children.Add(new TextBlock { Text = Core.DescriptionOf(file), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap });

            var card = UI.Card(outer, new Thickness(16, 14, 16, 14));
            card.Margin = new Thickness(0, 0, Gap, Gap);
            card.Cursor = System.Windows.Input.Cursors.Hand;
            card.MouseLeftButtonUp += (s, e) =>
            {
                if (e.OriginalSource is DependencyObject && IsInStar((DependencyObject)e.OriginalSource, star)) return;
                _win.SelectStrategy(file);
                Rebuild();
            };
            if (isCurrent)
            {
                card.BorderBrush = Theme.Alpha(Theme.Ok, 90);
                card.Background = Theme.Alpha(Theme.Ok, 12);
            }
            return card;
        }

        static bool IsInStar(DependencyObject src, DependencyObject star)
        {
            var d = src;
            while (d != null)
            {
                if (d == star) return true;
                d = System.Windows.Media.VisualTreeHelper.GetParent(d);
            }
            return false;
        }

        Border CatBadge(string cat)
        {
            Color c = cat == "FAKE" ? Theme.AccentMain : cat == "ALT" ? Theme.Warn : Theme.TextMuted;
            var tb = new TextBlock { Text = cat, FontSize = Theme.FsTiny, FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold, Foreground = Theme.Frozen(c) };
            return new Border { Background = Theme.Alpha(c, 26), BorderBrush = Theme.Alpha(c, 80),
                BorderThickness = new Thickness(1), CornerRadius = Theme.R6, Padding = new Thickness(8, 2, 8, 2), Child = tb };
        }
    }
}
