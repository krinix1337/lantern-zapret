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
        CellRow _list, _favList;
        TextBlock _favLabel, _allLabel;
        Border _banner;
        HashSet<string> _fav = new HashSet<string>();
        const double MinCardW = 250, Gap = 12;

        public StrategiesPage(MainWindow win)
        {
            _win = win;
            LoadFav();
            var banner = new Border
            {
                Background = Theme.BrSurface,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10,
                Padding = new Thickness(14, 10, 14, 10),
                // Правый отступ тут был ни к чему: сетка карточек ниже идёт ровно
                // до края, и баннер выглядел «уехавшим» на 12px вправо.
                Margin = new Thickness(0, 0, 0, 14)
            };
            _banner = banner;

            var bGrid = new Grid();
            bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Горизонтальный StackPanel меряет детей при бесконечной ширине, поэтому
            // TextBlock c Wrap никогда не переносился и на узком окне наезжал на кнопку.
            var hintSp = new Grid { VerticalAlignment = VerticalAlignment.Center };
            hintSp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hintSp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var infoIcon = UI.Icon(Icons.Info, 18, Theme.BrAccent, 1.8);
            infoIcon.VerticalAlignment = VerticalAlignment.Center;
            infoIcon.Margin = new Thickness(0, 0, 10, 0);
            Grid.SetColumn(infoIcon, 0);
            hintSp.Children.Add(infoIcon);

            var hintTb = new TextBlock
            {
                Text = Loc.T("strat.pickHint"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(hintTb, 1);
            hintSp.Children.Add(hintTb);
            Grid.SetColumn(hintSp, 0);
            bGrid.Children.Add(hintSp);

            var recBtn = Ctl.Button(Loc.T("strat.recommend"), Icons.Bolt, 1);
            recBtn.VerticalAlignment = VerticalAlignment.Center;
            recBtn.Margin = new Thickness(14, 0, 0, 0);
            recBtn.Click += (s, e) => DoRecommend();
            Grid.SetColumn(recBtn, 1);
            bGrid.Children.Add(recBtn);

            banner.Child = bGrid;
            Body.Children.Add(banner);

            // CellRow вместо WrapPanel + ручного Relayout: колонки и ширину
            // карточек панель считает сама на каждом измерении. WrapPanel держал
            // ширину карточек в поле и пересчитывал её только по SizeChanged, из-за
            // чего при расширении окна ряд не успевал добрать ширину и справа
            // оставалась пустая полоса.
            // Избранное — отдельная секция над общим списком. Общий список
            // остаётся один и тот же CellRow: он нужен selftest для замера сетки.
            _favLabel = SectionLabel(Loc.T("strat.favSection"));
            _favLabel.Margin = new Thickness(2, 0, 0, 8);
            _favList = new CellRow { MinCell = MinCardW, Gap = Gap };
            _allLabel = SectionLabel(Loc.T("strat.allSection"));
            _list = new CellRow { MinCell = MinCardW, Gap = Gap };
            Body.Children.Add(_favLabel);
            Body.Children.Add(_favList);
            Body.Children.Add(_allLabel);
            Body.Children.Add(_list);
        }

        public override void OnShow()
        {
            // Страница проверки могла добавить избранное после того, как этот
            // экземпляр был построен, поэтому список перечитываем при показе.
            LoadFav();
            Rebuild();
        }

        void LoadFav()
        {
            _fav.Clear();
            foreach (var f in Core.GetFavoriteStrategies()) _fav.Add(f);
        }
        void SaveFav()
        {
            Core.SetFavoriteStrategies(_fav);
        }

        volatile bool _recommending;

        void DoRecommend()
        {
            if (_recommending) return;
            _recommending = true;
            _win.ShowToast(Loc.T("strat.recommend.busy"), Sev.Info);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var info = Core.DetectIspInfo();
                    string ispDisplay = info != null ? info.ToString() : Core.DetectIsp();
                    var candidates = Core.GetCandidateStrategies(info);

                    string bestFile = null;
                    bool liveVerified = false;
                    bool wasService = Core.ServiceState() == "running";
                    bool wasManual = !wasService && Core.IsWinwsRunning();
                    string prevStrat = _win.CurrentStrategyFile();

                    if (Core.TryBeginWinwsOperation())
                    {
                        try
                        {
                            var quickProbes = new List<Target>();
                            Core.AddQ(quickProbes, "Discord", "https://discord.com");
                            Core.AddQ(quickProbes, "YouTube", "https://www.youtube.com");

                            int testCount = Math.Min(4, candidates.Count);
                            for (int i = 0; i < testCount; i++)
                            {
                                string cand = candidates[i];
                                Core.KillWinws();
                                if (!Core.StartWinws(cand)) continue;
                                // Прогрев тот же, что в проверке стратегий: 500 мс
                                // WinDivert не хватало, и рабочая стратегия
                                // помечалась как непроверенная.
                                System.Threading.Thread.Sleep(Core.ProbeWarmupMs);

                                int ok = 0;
                                int missed;
                                Core.RunProbes(quickProbes, delegate(Target q)
                                {
                                    if (Core.CurlCheck(q.Url, 5).Verdict == "ok")
                                        System.Threading.Interlocked.Increment(ref ok);
                                }, null, out missed);
                                if (ok >= 1)
                                {
                                    bestFile = cand;
                                    liveVerified = true;
                                    break;
                                }
                            }
                        }
                        finally
                        {
                            Core.KillWinws();
                            Core.EndWinwsOperation();
                            if (wasService || wasManual)
                            {
                                // Это finally фонового потока. Сам Dispatcher.Invoke
                                // бросает, если диспетчер уже погашен (окно закрылось),
                                // поэтому исключение из finally убило бы поток пула молча.
                                try { Dispatcher.Invoke((Action)delegate { try { _win.RestartInSameMode(wasService, wasManual, prevStrat); } catch { } }); }
                                catch { }
                            }
                        }
                    }

                    if (bestFile == null && candidates.Count > 0)
                        bestFile = candidates[0];

                    Dispatcher.Invoke((Action)delegate
                    {
                        if (bestFile == null)
                        {
                            _win.ShowToast(Loc.T("strat.recommend.fail"), Sev.Warn);
                            return;
                        }
                        string name = Core.PrettyName(bestFile);
                        string verifiedNote = liveVerified ? Loc.T("strat.recommend.verified") : "";
                        string msg = string.IsNullOrEmpty(ispDisplay)
                            ? string.Format(Loc.T("strat.recommend.result"), name) + verifiedNote
                            : string.Format(Loc.T("strat.recommend.isp"), ispDisplay, name) + verifiedNote;

                        var res = MessageBox.Show(
                            msg + Environment.NewLine + Environment.NewLine + Loc.T("strat.recommend.applyQ"),
                            Loc.T("strat.recommend.title"),
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (res == MessageBoxResult.Yes)
                        {
                            _win.RunStrategy(bestFile);
                            _win.ShowToast(string.Format(Loc.T("strat.recommend.applied"), name), Sev.Ok);
                        }
                        else
                        {
                            _win.ShowToast(msg, Sev.Ok);
                        }
                        Core.Info(msg);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke((Action)delegate { Core.Fail("DoRecommend: " + ex.Message); });
                }
                finally
                {
                    _recommending = false;
                }
            });
        }

#if SELFTEST
        // Тесту нужно померять ряд карточек и баннер над ним (приватные поля).
        // Если все стратегии оказались в избранном, общий ряд пуст — меряем избранное.
        public System.Windows.Controls.Panel DemoStratList
        {
            get { return _list.Children.Count > 0 ? (System.Windows.Controls.Panel)_list : (System.Windows.Controls.Panel)_favList; }
        }
        public FrameworkElement DemoBanner { get { return _banner; } }
#endif

        public void Rebuild()
        {
            _favList.Children.Clear();
            _list.Children.Clear();
            var files = Core.GetStrategyFiles();
            string current = _win.CurrentStrategyFile();
            int favCount = 0, restCount = 0;

            foreach (var f in files)
            {
                string name = Core.PrettyName(f);
                string cat = Core.CategoryOf(f);
                if (_fav.Contains(f))
                {
                    _favList.Children.Add(StrategyCard(f, name, cat, f == current));
                    favCount++;
                }
                else
                {
                    _list.Children.Add(StrategyCard(f, name, cat, f == current));
                    restCount++;
                }
            }

            bool hasFav = favCount > 0;
            _favLabel.Visibility = hasFav ? Visibility.Visible : Visibility.Collapsed;
            _favList.Visibility = hasFav ? Visibility.Visible : Visibility.Collapsed;
            _allLabel.Visibility = hasFav && restCount > 0 ? Visibility.Visible : Visibility.Collapsed;
            _list.Visibility = files.Count == 0 || restCount > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (files.Count == 0)
                _list.Children.Add(new TextBlock { Text = Loc.T("strat.nothing"), Foreground = Theme.BrMuted,
                    FontSize = Theme.FsBody, FontFamily = Theme.UiFont, Margin = new Thickness(2, 12, 0, 0) });
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
                _win.ShowToast(string.Format(adding ? Loc.T("strat.favAdded") : Loc.T("strat.favRemoved"), name),
                    adding ? Sev.Ok : Sev.Neutral);
                if (Theme.Mode == ThemeMode.System24 || !Theme.AnimationsEnabled)
                {
                    Rebuild();
                    return;
                }
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
                var cur = Pill.Make(Sev.Info, Loc.T("strat.current"));
                cur.Margin = new Thickness(8, 0, 0, 0);
                cur.VerticalAlignment = VerticalAlignment.Center;
                badgeRow.Children.Add(cur);
            }
            outer.Children.Add(badgeRow);

            outer.Children.Add(new TextBlock { Text = Core.DescriptionOf(file), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap });

            Brush normalBackground = isCurrent ? Theme.Alpha(Theme.AccentMain, 14) : Theme.BrSurface;
            Brush normalBorder = isCurrent ? Theme.BrAccent : Theme.BrStroke;

            var card = new Border
            {
                Background = normalBackground,
                BorderBrush = normalBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R12,
                Padding = new Thickness(16, 14, 16, 14),
                Child = outer,
                Margin = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            bool selecting = false;
            RadialGradientBrush stratSpot = null;
            RadialGradientBrush stratBgSpot = null;
            card.MouseEnter += (s, e) =>
            {
                if (selecting) return;
                if (Theme.Mode == ThemeMode.System24)
                {
                    card.Background = isCurrent ? Theme.Alpha(Theme.AccentMain, 24) : Theme.BrSurfaceHi;
                    if (Theme.AnimationsEnabled)
                    {
                        var anim = new ColorAnimation(isCurrent ? Theme.AccentMain : Theme.Stroke, Theme.AccentMain, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        var brush = new SolidColorBrush(isCurrent ? Theme.AccentMain : Theme.Stroke);
                        card.BorderBrush = brush;
                        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                    }
                    else card.BorderBrush = Theme.BrAccent;
                }
                else
                {
                    if (Theme.AnimationsEnabled)
                    {
                        if (stratSpot == null)
                        {
                            stratSpot = new RadialGradientBrush
                            {
                                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                                RadiusX = 0.45,
                                RadiusY = 0.45
                            };
                        }
                        if (stratBgSpot == null)
                        {
                            stratBgSpot = new RadialGradientBrush
                            {
                                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                                RadiusX = 0.55,
                                RadiusY = 0.55
                            };
                        }
                        Color outerCol = isCurrent ? Theme.AccentMain : Theme.Stroke;
                        stratSpot.GradientStops.Clear();
                        stratSpot.GradientStops.Add(new GradientStop(Color.FromArgb(235, Theme.AccentHi.R, Theme.AccentHi.G, Theme.AccentHi.B), 0.0));
                        stratSpot.GradientStops.Add(new GradientStop(Color.FromArgb(90, Theme.AccentMain.R, Theme.AccentMain.G, Theme.AccentMain.B), 0.45));
                        stratSpot.GradientStops.Add(new GradientStop(outerCol, 1.0));

                        Color bgCenter = isCurrent ? Color.FromArgb(44, Theme.AccentHi.R, Theme.AccentHi.G, Theme.AccentHi.B) : Theme.Blend(Theme.SurfaceHi, Theme.AccentHi, 0.16);
                        Color bgEdge = isCurrent ? Color.FromArgb(20, Theme.AccentMain.R, Theme.AccentMain.G, Theme.AccentMain.B) : Theme.SurfaceHi;
                        stratBgSpot.GradientStops.Clear();
                        stratBgSpot.GradientStops.Add(new GradientStop(bgCenter, 0.0));
                        stratBgSpot.GradientStops.Add(new GradientStop(bgEdge, 0.85));

                        var pos = e.GetPosition(card);
                        var relX = card.ActualWidth > 0 ? pos.X / card.ActualWidth : 0.5;
                        var relY = card.ActualHeight > 0 ? pos.Y / card.ActualHeight : 0.5;
                        var relPos = new Point(relX, relY);

                        stratSpot.Center = relPos;
                        stratSpot.GradientOrigin = relPos;
                        stratBgSpot.Center = relPos;
                        stratBgSpot.GradientOrigin = relPos;

                        card.BorderBrush = stratSpot;
                        card.Background = stratBgSpot;
                    }
                    else
                    {
                        card.Background = isCurrent ? Theme.Alpha(Theme.AccentMain, 24) : Theme.BrSurfaceHi;
                        card.BorderBrush = Theme.BrAccent;
                    }
                }
            };
            card.MouseMove += (s, e) =>
            {
                if (selecting || !Theme.AnimationsEnabled || Theme.Mode == ThemeMode.System24) return;
                var pos = e.GetPosition(card);
                var relX = card.ActualWidth > 0 ? pos.X / card.ActualWidth : 0.5;
                var relY = card.ActualHeight > 0 ? pos.Y / card.ActualHeight : 0.5;
                var relPos = new Point(relX, relY);
                if (stratSpot != null && card.BorderBrush == stratSpot)
                {
                    stratSpot.Center = relPos;
                    stratSpot.GradientOrigin = relPos;
                }
                if (stratBgSpot != null && card.Background == stratBgSpot)
                {
                    stratBgSpot.Center = relPos;
                    stratBgSpot.GradientOrigin = relPos;
                }
            };
            card.MouseLeave += (s, e) =>
            {
                if (selecting) return;
                card.Background = normalBackground;
                if (Theme.Mode == ThemeMode.System24)
                {
                    if (isCurrent)
                    {
                        card.BorderBrush = Theme.BrAccent;
                    }
                    else if (Theme.AnimationsEnabled)
                    {
                        var anim = new ColorAnimation(Theme.AccentMain, Theme.Stroke, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        var brush = new SolidColorBrush(Theme.AccentMain);
                        card.BorderBrush = brush;
                        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                    }
                    else card.BorderBrush = Theme.BrStroke;
                }
                else
                {
                    card.BorderBrush = normalBorder;
                }
            };
                card.MouseLeftButtonUp += (s, e) =>
            {
                if (e.OriginalSource is DependencyObject && IsInStar((DependencyObject)e.OriginalSource, star)) return;
                if (e.OriginalSource is DependencyObject && IsInStar((DependencyObject)e.OriginalSource, gear)) return;
                if (Core.WinwsOperationActive()) { _win.Warn(Loc.T("mw.busy")); return; }
                selecting = true;
                card.Background = Theme.Alpha(Theme.AccentMain, 34);
                card.BorderBrush = Theme.BrAccent;
                _win.SelectStrategy(file);
                Rebuild();
            };
            card.Focusable = true;
            card.GotKeyboardFocus += (s, e) =>
            {
                card.BorderBrush = Theme.BrAccentHi;
                card.BorderThickness = new Thickness(2);
                card.BringIntoView();
            };
            card.LostKeyboardFocus += (s, e) =>
            {
                card.BorderBrush = isCurrent ? Theme.BrAccent : normalBorder;
                card.BorderThickness = new Thickness(1);
            };
            card.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Space)
                {
                    if (Core.WinwsOperationActive()) { _win.Warn(Loc.T("mw.busy")); e.Handled = true; return; }
                    selecting = true;
                    card.Background = Theme.Alpha(Theme.AccentMain, 34);
                    card.BorderBrush = Theme.BrAccent;
                    _win.SelectStrategy(file);
                    Rebuild();
                    e.Handled = true;
                }
            };
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
            var tb = new TextBlock
            {
                Text = cat,
                FontSize = Theme.FsTiny,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold,
                Foreground = Theme.Frozen(c),
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                LineHeight = double.NaN,
                Margin = new Thickness(0, 2, 0, 0)
            };
            return new Border
            {
                Background = Theme.Alpha(c, 26),
                BorderBrush = Theme.Alpha(c, 80),
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R6,
                Height = 20,
                Padding = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = tb
            };
        }
    }
}
