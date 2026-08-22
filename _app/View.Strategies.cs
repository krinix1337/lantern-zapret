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
            _search.GotFocus += (s, e) => sb.BorderBrush = Theme.BrAccent;
            _search.LostFocus += (s, e) => sb.BorderBrush = Theme.BrStroke;
            sb.MouseEnter += (s, e) => { if (!_search.IsFocused) sb.BorderBrush = Theme.BrSurfaceHi; };
            sb.MouseLeave += (s, e) => { if (!_search.IsFocused) sb.BorderBrush = Theme.BrStroke; };
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

        public void Rebuild()
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

        void Relayout()
        {
            double avail = _list.ActualWidth;
            if (avail <= 1) return;
            int cols = Math.Max(1, (int)((avail) / (MinCardW + Gap)));
            double cardW = (avail - Gap * cols) / cols - 1; 
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

            var titleRow = new Grid();
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var star = new Button { Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Width = 24, Height = 24, Margin = new Thickness(0, 0, 8, 0) };
            Ctl.StripChrome(star);
            bool fav = _fav.Contains(file);
            var starBg = new Border { Width = 24, Height = 24, Background = Brushes.Transparent };
            starBg.Child = UI.Icon(fav ? Icons.StarFilled : Icons.Star, 18, fav ? Theme.BrWarn : Theme.BrFaint, 1.6);
            star.Content = starBg;
            Ctl.AutomationSetName(star, (fav ? Loc.T("strat.favRemove") : Loc.T("strat.favAdd")) + name);
            star.ToolTip = fav ? Loc.T("strat.favRemove") + name : Loc.T("strat.favAdd") + name;
            star.MouseEnter += (s, e) => { };
            star.MouseLeave += (s, e) => { };
            star.Click += (s, e) =>
            {
                bool adding = !_fav.Contains(file);
                if (adding) _fav.Add(file); else _fav.Remove(file);
                SaveFav();
                starBg.Background = Brushes.Transparent;
                var newIcon = UI.Icon(adding ? Icons.StarFilled : Icons.Star, 18, adding ? Theme.BrWarn : Theme.BrFaint, 1.6);
                starBg.Child = newIcon;
                star.ToolTip = (adding ? Loc.T("strat.favRemove") : Loc.T("strat.favAdd")) + name;
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
                _win.ShowToast(string.Format(adding ? Loc.T("strat.favAdded") : Loc.T("strat.favRemoved"), name),
                    adding ? Sev.Ok : Sev.Neutral);
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
                timer.Tick += (s2, e2) => { timer.Stop(); Rebuild(); };
                timer.Start();
            };
            Grid.SetColumn(star, 0);
            titleRow.Children.Add(star);

            var nameT = UI.T(name, Theme.FsBody, Theme.BrText, FontWeights.SemiBold);
            nameT.VerticalAlignment = VerticalAlignment.Center;
            nameT.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(nameT, 1);
            titleRow.Children.Add(nameT);

            var gear = new Button { Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                Width = 24, Height = 24, Margin = new Thickness(4, 0, 0, 0) };
            Ctl.StripChrome(gear);
            gear.Content = UI.Icon(Icons.Gear, 16, Theme.BrFaint, 1.6);
            Ctl.AutomationSetName(gear, Loc.T("strat.settings") + name);
            gear.ToolTip = Loc.T("strat.settings");
            Ctl.AddMotion(gear);
            gear.Click += (s, e) =>
            {
                try { System.Diagnostics.Process.Start("notepad.exe", System.IO.Path.Combine(Core.Root, file)); }
                catch { }
            };
            Grid.SetColumn(gear, 2);
            titleRow.Children.Add(gear);

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
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            var cardScale = new ScaleTransform(1, 1);
            card.RenderTransform = cardScale;

            Brush normalBackground = isCurrent ? Theme.Alpha(Theme.Ok, 14) : Theme.BrSurface;
            Brush normalBorder = isCurrent ? Theme.Alpha(Theme.Ok, 90) : Theme.BrStroke;
            card.BorderBrush = normalBorder;
            card.Background = normalBackground;

            bool selecting = false;
            card.MouseEnter += (s, e) =>
            {
                if (selecting) return;
                card.Background = isCurrent ? Theme.Alpha(Theme.Ok, 24) : Theme.BrSurfaceHi;
                card.BorderBrush = isCurrent ? Theme.BrOk : Theme.BrAccent;
                AnimateCardScale(cardScale, 1.018, 120);
            };
            card.MouseLeave += (s, e) =>
            {
                if (selecting) return;
                card.Background = isCurrent ? Theme.Alpha(Theme.Ok, 14) : Theme.BrSurface;
                card.BorderBrush = isCurrent ? Theme.Alpha(Theme.Ok, 90) : Theme.BrStroke;
                AnimateCardScale(cardScale, 1, 140);
            };
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (!IsInStar(e.OriginalSource as DependencyObject, star) &&
                    !IsInStar(e.OriginalSource as DependencyObject, gear)) AnimateCardScale(cardScale, 0.988, 70);
            };
            card.MouseLeftButtonUp += (s, e) =>
            {
                if (e.OriginalSource is DependencyObject && IsInStar((DependencyObject)e.OriginalSource, star)) return;
                if (e.OriginalSource is DependencyObject && IsInStar((DependencyObject)e.OriginalSource, gear)) return;
                selecting = true;
                card.Background = Theme.Alpha(Theme.AccentMain, 34);
                card.BorderBrush = Theme.BrAccent;
                _win.SelectStrategy(file);
                if (!Theme.AnimationsEnabled)
                {
                    Rebuild();
                    return;
                }

                cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                cardScale.ScaleX = 1.018;
                cardScale.ScaleY = 1.018;
                var confirm = new DoubleAnimation(1.045, TimeSpan.FromMilliseconds(110))
                {
                    AutoReverse = true,
                    FillBehavior = FillBehavior.Stop,
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                confirm.Completed += (s2, e2) => Rebuild();
                cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, confirm);
                cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, confirm);
            };
            return card;
        }

        static void AnimateCardScale(ScaleTransform scale, double target, int duration)
        {
            if (!Theme.AnimationsEnabled)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = target;
                scale.ScaleY = target;
                return;
            }
            var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(duration))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
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
