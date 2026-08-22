using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ZapretStudio
{
    class MainWindow : Window
    {
        // Маршрутизация
        readonly Dictionary<string, Page> _pages = new Dictionary<string, Page>();
        readonly Dictionary<string, Button> _navButtons = new Dictionary<string, Button>();
        ContentControl _host;
        string _current = "";
        Border _topStatusPill;
        OverviewPage _overview;

        // Состояние запуска
        string _currentStrategyFile;   // выбранная/запущенная стратегия
        System.Windows.Forms.NotifyIcon _tray;

        public MainWindow()
        {
            Title = Core.AppName;
            try { var wi = Core.AppIconSource(); if (wi != null) Icon = wi; } catch { }
            Width = 1120; Height = 740;
            MinWidth = 1000; MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.BrBgDeep;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            _currentStrategyFile = Core.Get("last_strategy", null);
            _collapsed = Core.GetBool("sidebar_collapsed", false);

            AllowsTransparency = false;
            BuildChrome();
            BuildTray();

            SourceInitialized += (s, e) => ApplyRoundedCorners();
            Loaded += (s, e) => { Navigate("overview"); if (_overview != null) _overview.StartTimer(); AfterLoad(); };
            Closing += OnClosing;

            // Смена языка/темы — перестроить интерфейс.
            Loc.LanguageChanged += OnUiChanged;
            Theme.ThemeChanged += OnUiChanged;
        }

        void OnUiChanged()
        {
            try
            {
                foreach (var p in _pages.Values) { try { p.OnHide(); } catch { } }
                _pages.Clear();
                _navButtons.Clear();
                _activeNavKey = null;
                _overview = null;
                BuildChrome();
                RebuildTray();
                Navigate(string.IsNullOrEmpty(_current) ? "overview" : _current);
                if (_overview != null) _overview.StartTimer();
                RefreshTop();
                // Плавное проявление всего интерфейса после смены темы/языка.
                if (Theme.AnimationsEnabled && Content is UIElement)
                {
                    var el = (UIElement)Content;
                    var fade = new System.Windows.Media.Animation.DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(260))
                    { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                    el.BeginAnimation(OpacityProperty, fade);
                }
            }
            catch { }
        }

        void RebuildTray()
        {
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } } catch { }
            BuildTray();
            RefreshTop();
        }

        // Скруглённые углы окна через DWM (Windows 11). На Windows 10 вызов
        // безопасно игнорируется. Ничего в системе не меняется — только форма окна.
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        void ApplyRoundedCorners()
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
                int DWMWCP_ROUND = 2;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref DWMWCP_ROUND, sizeof(int));
            }
            catch { }
        }

        void BuildChrome()
        {
            // Кастомная рамка окна
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            var chrome = new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 48, ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0), GlassFrameThickness = new Thickness(0)
            };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });  // topbar
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // sidebar (own width)
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var topbar = BuildTopbar();
            Grid.SetRow(topbar, 0); Grid.SetColumn(topbar, 0); Grid.SetColumnSpan(topbar, 2);
            root.Children.Add(topbar);

            var side = BuildSidebar();
            Grid.SetRow(side, 1); Grid.SetColumn(side, 0);
            root.Children.Add(side);

            _host = new ContentControl();
            var contentLayer = new Grid { ClipToBounds = true };
            if (Theme.Mode == ThemeMode.Peter && Core.GetBool("peter_backdrop", true))
            {
                var source = PeterBackdrop();
                if (source != null)
                {
                    contentLayer.Children.Add(new Image { Source = source, Width = 560, Opacity = 0.28,
                        Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 20, 0),
                        IsHitTestVisible = false });
                    contentLayer.Children.Add(new Border { Background = Theme.Alpha(Theme.BgDeep, 150), IsHitTestVisible = false });
                }
            }
            contentLayer.Children.Add(_host);
            var hostBorder = new Border { Child = contentLayer, Background = Theme.BrBgDeep };
            Grid.SetRow(hostBorder, 1); Grid.SetColumn(hostBorder, 1);
            root.Children.Add(hostBorder);

            _toastHost = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 20, 20),
                MaxWidth = 380, IsHitTestVisible = false };
            Grid.SetRow(_toastHost, 1); Grid.SetColumn(_toastHost, 1);
            Panel.SetZIndex(_toastHost, 100);
            root.Children.Add(_toastHost);

            _rootGrid = root;
            Content = root;
        }

        Grid _rootGrid;
        StackPanel _toastHost;
        PeterMusicWidget _peterSidebarWidget;
        public readonly PeterMusicController PeterMusic = new PeterMusicController();
        static ImageSource _peterBackdrop;

        public static ImageSource PeterBackdrop()
        {
            if (_peterBackdrop != null) return _peterBackdrop;
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = asm.GetManifestResourceStream("ZapretStudio.Assets.PeterGriffin"))
                {
                    if (stream == null) return null;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                    image.Freeze();
                    _peterBackdrop = image;
                    return _peterBackdrop;
                }
            }
            catch { return null; }
        }

        public void TogglePeterMusic()
        {
            try
            {
                PeterMusic.ScanTracks();
                if (PeterMusic.TrackCount == 0)
                {
                    ShowToast(Loc.T("settings.peter.song.none"), Sev.Warn);
                    return;
                }
                bool started = PeterMusic.ToggleMusic(PeterBackdrop());
                if (started && PeterMusic.CurrentTrack != null)
                    ShowToast(string.Format(Loc.T("settings.peter.song.playing"), PeterMusic.CurrentTrack.Title), Sev.Ok);
            }
            catch (Exception ex) { Core.Fail(ex.Message); ShowToast(Loc.T("settings.peter.song.none"), Sev.Warn); }
        }

        public void PlayRandomPeterSong()
        {
            try
            {
                PeterMusic.ScanTracks();
                if (PeterMusic.TrackCount == 0) { ShowToast(Loc.T("settings.peter.song.none"), Sev.Warn); return; }
                PeterMusic.PlayRandom(PeterBackdrop());
                if (PeterMusic.CurrentTrack != null)
                    ShowToast(string.Format(Loc.T("settings.peter.song.playing"), PeterMusic.CurrentTrack.Title), Sev.Ok);
            }
            catch (Exception ex) { Core.Fail(ex.Message); ShowToast(Loc.T("settings.peter.song.none"), Sev.Warn); }
        }

        public void StopPeterSong()
        {
            PeterMusic.Stop();
        }

        // Toast-стек: быстрые уведомления отображаются одно над другим, без наложения.
        public void ShowToast(string text, Sev sev)
        {
            if (!Core.GetBool("notifications", true)) return;
            if (_rootGrid == null || _toastHost == null) return;
            Color c = sev == Sev.Ok ? Theme.Ok : sev == Sev.Warn ? Theme.Warn : sev == Sev.Err ? Theme.Err : Theme.AccentMain;
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var iconWrap = new Border { Width = 28, Height = 28, CornerRadius = Theme.R8,
                Background = Theme.Alpha(c, 30), VerticalAlignment = VerticalAlignment.Top };
            iconWrap.Child = UI.Icon(sev == Sev.Ok ? Icons.Check : sev == Sev.Err ? Icons.Cross : Icons.Info, 16, Theme.Frozen(c), 1.8);
            sp.Children.Add(iconWrap);
            var tb = new TextBlock { Text = text, Foreground = Theme.BrText, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(10, 2, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 300, TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(tb);
            var bd = new Border { Background = Theme.BrSurface, BorderBrush = Theme.Alpha(c, 100),
                BorderThickness = new Thickness(1), CornerRadius = Theme.R12, Padding = new Thickness(12, 10, 14, 10),
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 0, 8) };
            bd.Child = sp;
            _toastHost.Children.Add(bd);

            var move = new TranslateTransform(0, 12);
            bd.RenderTransform = move;
            bd.Opacity = 0;
            bd.BeginAnimation(OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
            move.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(180))
            { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } });
            var life = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            life.Tick += (s, e) =>
            {
                life.Stop();
                var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fade.Completed += (s2, e2) => { try { _toastHost.Children.Remove(bd); } catch { } };
                bd.BeginAnimation(OpacityProperty, fade);
            };
            life.Start();
        }

        Border BuildTopbar()
        {
            var g = new Grid { Background = Theme.BrBgBase };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0) };
            var logo = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(6),
                Background = Theme.BrAccent, VerticalAlignment = VerticalAlignment.Center };
            logo.Child = UI.Icon(Icons.Lantern, 14, Theme.BrOnAccent, 1.8);
            brand.Children.Add(logo);
            brand.Children.Add(new TextBlock { Text = Core.AppName, Foreground = Theme.BrText, FontSize = Theme.FsBody,
                FontWeight = FontWeights.Bold, FontFamily = Theme.UiFont, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(9, 0, 0, 0) });
            Grid.SetColumn(brand, 0); g.Children.Add(brand);

            _topStatusPill = Pill.Make(Sev.Neutral, "—");
            _topStatusPill.VerticalAlignment = VerticalAlignment.Center;
            _topStatusPill.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(_topStatusPill, 1); g.Children.Add(_topStatusPill);

            // область для перетаскивания окна
            var drag = new Border { Background = Brushes.Transparent };
            drag.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 1) DragMove(); };
            Grid.SetColumn(drag, 2); g.Children.Add(drag);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(TopIcon(Theme.Mode == ThemeMode.Light ? Icons.Moon : Icons.Sun,
                Loc.T("settings.theme"), delegate { ToggleTheme(); }));
            right.Children.Add(TopIcon(Icons.Refresh, Loc.T("common.checkUpdates"), delegate { CheckUpdates(); }));
            right.Children.Add(TopIcon(Icons.Gear, Loc.T("common.settings"), delegate { Navigate("settings"); }));
            right.Children.Add(WinBtn(Icons.Menu, Loc.T("common.minimize"), delegate { WindowState = WindowState.Minimized; }, false));
            right.Children.Add(WinBtn(Icons.Grid, Loc.T("common.maximize"), delegate { ToggleMax(); }, false));
            right.Children.Add(WinBtn(Icons.Cross, Loc.T("common.close"), delegate { Close(); }, true));
            Grid.SetColumn(right, 3); g.Children.Add(right);

            return new Border { Child = g, BorderBrush = Theme.BrStroke, BorderThickness = new Thickness(0, 0, 0, 1) };
        }

        Button TopIcon(string icon, string name, Action act)
        {
            var b = new Button { Cursor = Cursors.Hand, Width = 38, Height = 38, Margin = new Thickness(2, 0, 2, 0) };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R10, Background = Brushes.Transparent };
            bd.Child = UI.Icon(icon, 17, Theme.BrMuted, 1.8);
            b.Content = bd;
            b.MouseEnter += (s, e) => bd.Background = Theme.BrSurfaceHi;
            b.MouseLeave += (s, e) => bd.Background = Brushes.Transparent;
            b.Click += (s, e) => act();
            Ctl.AutomationSetName(b, name);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        Button WinBtn(string icon, string name, Action act, bool danger)
        {
            var b = new Button { Cursor = Cursors.Hand, Width = 44, Height = 48 };
            Ctl.StripChrome(b);
            var bd = new Border { Background = Brushes.Transparent };
            bd.Child = UI.Icon(icon, 15, Theme.BrMuted, 1.6);
            b.Content = bd;
            b.MouseEnter += (s, e) => bd.Background = danger ? Theme.Frozen(Theme.Err) : Theme.BrSurfaceHi;
            b.MouseLeave += (s, e) => bd.Background = Brushes.Transparent;
            b.Click += (s, e) => act();
            Ctl.AutomationSetName(b, name);
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        void ToggleMax() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }

        void ToggleTheme()
        {
            var next = Theme.NextMode();
            Core.Set("theme", next == ThemeMode.Light ? "light" : next == ThemeMode.Amoled ? "amoled" : next == ThemeMode.Aurora ? "aurora" : next == ThemeMode.Peter ? "peter" : "dark");
            Core.SaveConfig();
            Theme.Apply(next);
        }

        Border _sidebar;
        StackPanel _navPanel;
        readonly System.Collections.Generic.List<TextBlock> _navLabels = new System.Collections.Generic.List<TextBlock>();
        StackPanel _bottomBox;
        Button _collapseBtn;
        bool _collapsed;
        int _sidebarAnimationId;
        const double SideWide = 248, SideNarrow = 64;

        Border BuildSidebar()
        {
            var outer = new Grid { Background = Theme.BrBgBase };
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _navLabels.Clear();
            var nav = new StackPanel { Margin = new Thickness(12, 12, 12, 0) };
            _navPanel = nav;
            nav.Children.Add(NavItem("overview", Icons.Home, Loc.T("nav.overview")));
            nav.Children.Add(NavItem("strategies", Icons.Grid, Loc.T("nav.strategies")));
            nav.Children.Add(NavItem("check", Icons.Pulse, Loc.T("nav.check")));
            nav.Children.Add(NavItem("service", Icons.Server, Loc.T("nav.service")));
            nav.Children.Add(NavItem("filters", Icons.Filter, Loc.T("nav.filters")));
            nav.Children.Add(NavItem("settings", Icons.Gear, Loc.T("nav.settings")));
            nav.Children.Add(NavItem("log", Icons.List, Loc.T("nav.log")));
            nav.Children.Add(NavItem("about", Icons.Info, Loc.T("nav.about")));
            var sv = new SmoothScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(sv, 0); outer.Children.Add(sv);

            // низ: кнопка сворачивания + плеер + версия, github, обновление
            var bottomWrap = new StackPanel { Margin = new Thickness(12, 8, 12, 12) };

            _peterSidebarWidget = new PeterMusicWidget(PeterMusic);
            bottomWrap.Children.Add(_peterSidebarWidget);

            _collapseBtn = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 8),
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Ctl.StripChrome(_collapseBtn);
            _collapseBd = new Border { CornerRadius = Theme.R10, Padding = new Thickness(12, 9, 12, 9), Background = Brushes.Transparent };
            var cbSp = new StackPanel { Orientation = Orientation.Horizontal };
            _collapseSp = cbSp;
            _collapseIcon = UI.Icon(Icons.Menu, 18, Theme.BrMuted, 1.8);
            _collapseIcon.VerticalAlignment = VerticalAlignment.Center;
            cbSp.Children.Add(_collapseIcon);
            _collapseLabel = new TextBlock { Text = Loc.T("mw.collapse"), Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center };
            _navLabels.Add(_collapseLabel);
            cbSp.Children.Add(_collapseLabel);
            var cbBd = _collapseBd;
            cbBd.Child = cbSp; _collapseBtn.Content = cbBd;
            _collapseBtn.MouseEnter += (s, e) => cbBd.Background = Theme.BrSurface;
            _collapseBtn.MouseLeave += (s, e) => cbBd.Background = Brushes.Transparent;
            _collapseBtn.Click += (s, e) => ToggleCollapse();
            Ctl.AutomationSetName(_collapseBtn, Loc.T("mw.collapse"));
            bottomWrap.Children.Add(_collapseBtn);

            _bottomBox = new StackPanel { Margin = new Thickness(4, 0, 4, 0) };
            var line = new Border { Height = 1, Background = Theme.BrStroke, Margin = new Thickness(0, 0, 0, 12) };
            _bottomBox.Children.Add(line);
            _updateLine = new TextBlock { Text = string.Format(Loc.T("mw.verLine"), SettingsPage.NormVer(Core.ZapretVersion())), Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap };
            _bottomBox.Children.Add(_updateLine);
            _tgVerSidebar = new TextBlock { Text = TgSidebarText(), Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0) };
            _bottomBox.Children.Add(_tgVerSidebar);

            _appVerSidebar = new TextBlock { Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0) };
            _bottomBox.Children.Add(_appVerSidebar);
            UpdateAppSidebarVersion();

            var gh = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            Ctl.StripChrome(gh);
            var ghsp = new StackPanel { Orientation = Orientation.Horizontal };
            ghsp.Children.Add(UI.Icon(Icons.Github, 15, Theme.BrMuted, 1.6));
            ghsp.Children.Add(new TextBlock { Text = "GitHub", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            gh.Content = ghsp;
            gh.Click += (s, e) => Core.OpenUrl(Core.AppRepo);
            Ctl.AutomationSetName(gh, Loc.T("mw.openGithub"));
            _bottomBox.Children.Add(gh);

            _bottomBoxContainer = new Border { Child = _bottomBox, ClipToBounds = true };
            bottomWrap.Children.Add(_bottomBoxContainer);

            Grid.SetRow(bottomWrap, 1); outer.Children.Add(bottomWrap);

            _sidebar = new Border { Child = outer, BorderBrush = Theme.BrStroke, BorderThickness = new Thickness(0, 0, 1, 0),
                Width = _collapsed ? SideNarrow : SideWide, ClipToBounds = true };
            ApplyCollapsedState(false);
            return _sidebar;
        }

        System.Windows.Shapes.Path _collapseIcon;
        TextBlock _collapseLabel;
        StackPanel _collapseSp;
        Border _collapseBd;
        Border _bottomBoxContainer;

        void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            Core.SetBool("sidebar_collapsed", _collapsed); Core.SaveConfig();
            ApplyCollapsedState(true);
        }

        void ApplyCollapsedState(bool animate)
        {
            int animationId = ++_sidebarAnimationId;
            double target = _collapsed ? SideNarrow : SideWide;

            if (_collapseLabel != null) _collapseLabel.Text = _collapsed ? "" : Loc.T("mw.collapse");
            if (_collapseIcon != null) _collapseIcon.Data = Geometry.Parse(_collapsed ? Icons.MenuOpen : Icons.Menu);

            if (_sidebar == null) return;

            if (!animate || !Theme.AnimationsEnabled)
            {
                _sidebar.BeginAnimation(FrameworkElement.WidthProperty, null);
                _sidebar.Width = target;
                foreach (var lbl in _navLabels)
                {
                    lbl.BeginAnimation(OpacityProperty, null);
                    lbl.Opacity = _collapsed ? 0 : 1;
                    lbl.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
                }
                foreach (var kv in _navButtons)
                {
                    var arr = (object[])kv.Value.Tag;
                    var bd = (Border)arr[0]; var sp = (StackPanel)arr[3];
                    bd.Padding = _collapsed ? new Thickness(0, 10, 0, 10) : new Thickness(12, 10, 12, 10);
                    sp.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                    kv.Value.Width = _collapsed ? 44 : double.NaN;
                    kv.Value.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
                }
                if (_navPanel != null) _navPanel.Margin = _collapsed ? new Thickness(4, 12, 4, 0) : new Thickness(12, 12, 12, 0);
                if (_collapseSp != null) _collapseSp.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                if (_collapseBd != null) _collapseBd.Padding = _collapsed ? new Thickness(0, 9, 0, 9) : new Thickness(12, 9, 12, 9);
                if (_peterSidebarWidget != null) _peterSidebarWidget.SetCollapsedMode(_collapsed);
                if (_bottomBoxContainer != null)
                {
                    _bottomBoxContainer.BeginAnimation(FrameworkElement.HeightProperty, null);
                    _bottomBoxContainer.Height = _collapsed ? 0 : double.NaN;
                }
                if (_bottomBox != null)
                {
                    _bottomBox.BeginAnimation(OpacityProperty, null);
                    _bottomBox.Opacity = _collapsed ? 0 : 1;
                }
                return;
            }

            // Плавная анимация с поддержкой высокой герцовки
            double from = _sidebar.ActualWidth > 0 ? _sidebar.ActualWidth : (_collapsed ? SideWide : SideNarrow);

            if (_collapsed)
            {
                // Сворачивание: плавно растворяем текст и плавно уменьшаем высоту нижнего блока
                foreach (var lbl in _navLabels)
                {
                    var fadeOut = new DoubleAnimation(lbl.Opacity, 0, TimeSpan.FromMilliseconds(110))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    lbl.BeginAnimation(OpacityProperty, fadeOut);
                }
                if (_bottomBox != null)
                {
                    var fadeOut = new DoubleAnimation(_bottomBox.Opacity, 0, TimeSpan.FromMilliseconds(110))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    _bottomBox.BeginAnimation(OpacityProperty, fadeOut);
                }
                if (_bottomBoxContainer != null)
                {
                    double curH = _bottomBoxContainer.ActualHeight > 0 ? _bottomBoxContainer.ActualHeight : 92;
                    var hAnim = new DoubleAnimation(curH, 0, TimeSpan.FromMilliseconds(200))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                    _bottomBoxContainer.BeginAnimation(FrameworkElement.HeightProperty, hAnim);
                }

                var widthAnim = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };

                widthAnim.Completed += (s, e) =>
                {
                    if (animationId != _sidebarAnimationId || !_collapsed) return;
                    foreach (var lbl in _navLabels) { lbl.Visibility = Visibility.Collapsed; }
                    foreach (var kv in _navButtons)
                    {
                        var arr = (object[])kv.Value.Tag;
                        var bd = (Border)arr[0]; var sp = (StackPanel)arr[3];
                        bd.Padding = new Thickness(0, 10, 0, 10);
                        sp.HorizontalAlignment = HorizontalAlignment.Center;
                        kv.Value.Width = 44;
                        kv.Value.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                    if (_navPanel != null) _navPanel.Margin = new Thickness(4, 12, 4, 0);
                    if (_collapseSp != null) _collapseSp.HorizontalAlignment = HorizontalAlignment.Center;
                    if (_collapseBd != null) _collapseBd.Padding = new Thickness(0, 9, 0, 9);
                    if (_peterSidebarWidget != null) _peterSidebarWidget.SetCollapsedMode(true);
                };
                _sidebar.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            }
            else
            {
                // Разворачивание: возвращаем разметку и плавно расширяем + увеличиваем высоту нижнего блока
                foreach (var kv in _navButtons)
                {
                    var arr = (object[])kv.Value.Tag;
                    var bd = (Border)arr[0]; var sp = (StackPanel)arr[3];
                    bd.Padding = new Thickness(12, 10, 12, 10);
                    sp.HorizontalAlignment = HorizontalAlignment.Left;
                    kv.Value.Width = double.NaN;
                    kv.Value.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                if (_navPanel != null) _navPanel.Margin = new Thickness(12, 12, 12, 0);
                if (_collapseSp != null) _collapseSp.HorizontalAlignment = HorizontalAlignment.Left;
                if (_collapseBd != null) _collapseBd.Padding = new Thickness(12, 9, 12, 9);
                if (_peterSidebarWidget != null) _peterSidebarWidget.SetCollapsedMode(false);

                if (_bottomBoxContainer != null)
                {
                    var hAnim = new DoubleAnimation(0, 92, TimeSpan.FromMilliseconds(200))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                    hAnim.Completed += (s, e) =>
                    {
                        if (animationId == _sidebarAnimationId && !_collapsed)
                            _bottomBoxContainer.Height = double.NaN;
                    };
                    _bottomBoxContainer.BeginAnimation(FrameworkElement.HeightProperty, hAnim);
                }

                foreach (var lbl in _navLabels)
                {
                    lbl.Visibility = Visibility.Visible;
                    lbl.Opacity = 0;
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(40),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    lbl.BeginAnimation(OpacityProperty, fadeIn);
                }

                if (_bottomBox != null)
                {
                    _bottomBox.Opacity = 0;
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                    {
                        BeginTime = TimeSpan.FromMilliseconds(40),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    _bottomBox.BeginAnimation(OpacityProperty, fadeIn);
                }

                var widthAnim = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                _sidebar.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            }
        }

        TextBlock _updateLine;
        TextBlock _tgVerSidebar;
        TextBlock _appVerSidebar;

        static string TgSidebarText()
        {
            if (!Core.TgProxyInstalled()) return Loc.T("mw.tgVer.none");
            string v = Core.TgProxyLocalVersion();
            return string.Format(Loc.T("mw.tgVer"), string.IsNullOrEmpty(v) ? "?" : SettingsPage.NormVer(v));
        }

        void UpdateAppSidebarVersion()
        {
            if (_appVerSidebar == null) return;
            string appNorm = SettingsPage.NormVer(Core.AppVersion);
            bool hasUpdate = !string.IsNullOrEmpty(_lastAppLatest)
                && SettingsPage.CompareVersions(_lastAppLatest, Core.AppVersion) > 0;
            _appVerSidebar.Text = string.Format(Loc.T(hasUpdate ? "mw.appShell.update" : "mw.appShell"), appNorm);
            _appVerSidebar.Foreground = hasUpdate ? Theme.BrWarn : Theme.BrFaint;
        }

        public void RefreshSidebarVersions()
        {
            if (_updateLine != null)
            {
                string local = SettingsPage.NormVer(Core.ZapretVersion());
                if (!string.IsNullOrEmpty(_lastZapretLatest) && SettingsPage.CompareVersions(_lastZapretLatest, local) <= 0)
                    _updateLine.Text = string.Format(Loc.T("mw.verCurrent"), local);
                else
                    _updateLine.Text = string.Format(Loc.T("mw.verLine"), local);
            }
            if (_tgVerSidebar != null)
                _tgVerSidebar.Text = TgSidebarText();
            UpdateAppSidebarVersion();
            Page settingsPage;
            if (_pages.TryGetValue("settings", out settingsPage))
            {
                var settings = settingsPage as SettingsPage;
                if (settings != null)
                    settings.SetAutomaticUpdateResults(_lastZapretLatest, _lastZapretLocal,
                        _lastTgLatest, _lastTgLocal, _lastAppLatest, Core.AppVersion);
            }
        }

        Button NavItem(string key, string icon, string label)
        {
            var b = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 4), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R8, Padding = new Thickness(12, 10, 12, 10), Background = Brushes.Transparent };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = UI.Icon(icon, 18, Theme.BrMuted, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
            var animType = key == "settings" ? IconAnimType.Rotate90 :
                           key == "strategies" ? IconAnimType.Wiggle :
                           key == "check" ? IconAnimType.Pulse :
                           IconAnimType.ScaleBounce;
            UI.AttachIconHoverAnimation(b, ic, animType);
            sp.Children.Add(ic);
            var tb = new TextBlock { Text = label, Foreground = Theme.BrMuted, FontSize = Theme.FsBody, FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible };
            _navLabels.Add(tb);
            sp.Children.Add(tb);
            bd.Child = sp;
            b.Content = bd;
            b.Tag = new object[] { bd, ic, tb, sp };
            b.ToolTip = label;
            b.MouseEnter += (s, e) =>
            {
                if (_current != key)
                {
                    bd.Background = Theme.BrSurface;
                    if (ic.Stroke != null) ic.Stroke = Theme.BrText;
                    if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = Theme.BrText;
                    tb.Foreground = Theme.BrText;
                }
            };
            b.MouseLeave += (s, e) =>
            {
                if (_current != key)
                {
                    bd.Background = Brushes.Transparent;
                    if (ic.Stroke != null) ic.Stroke = Theme.BrMuted;
                    if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = Theme.BrMuted;
                    tb.Foreground = Theme.BrMuted;
                }
            };
            b.Click += (s, e) => Navigate(key);
            Ctl.AutomationSetName(b, label);
            _navButtons[key] = b;
            return b;
        }

        string _activeNavKey;

        void PaintNav()
        {
            // Единый цикл по всем пунктам — тот же паттерн, что в PaintTabs (CheckPage).
            foreach (var kv in _navButtons)
            {
                var arr = (object[])kv.Value.Tag;
                var bd = (Border)arr[0];
                var ic = (System.Windows.Shapes.Path)arr[1];
                var tb = (TextBlock)arr[2];
                bool on = kv.Key == _current;
                bd.Background = on ? Theme.BrAccent : Brushes.Transparent;
                Brush fg = on ? Theme.BrOnAccent : Theme.BrMuted;
                if (ic.Stroke != null) ic.Stroke = fg;
                if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = fg;
                tb.Foreground = fg;
                bd.InvalidateVisual();
            }
            _activeNavKey = _current;
            Title = Core.AppName + "  ·  " + _current;
        }

        Page PageFor(string key)
        {
            if (_pages.ContainsKey(key)) return _pages[key];
            Page p;
            switch (key)
            {
                case "overview": _overview = new OverviewPage(this); p = _overview; break;
                case "strategies": p = new StrategiesPage(this); break;
                case "check": p = new CheckPage(this); break;
                case "service": p = new ServicePage(this); break;
                case "filters": p = new FiltersPage(this); break;
                case "settings": p = new SettingsPage(this); break;
                case "log": p = new LogPage(this); break;
                case "about": p = new AboutPage(this); break;
                default: p = new OverviewPage(this); break;
            }
            _pages[key] = p;
            return p;
        }

        public void Navigate(string key)
        {
            // Скрываем предыдущую страницу (останавливаем её таймеры и т.п.).
            if (!string.IsNullOrEmpty(_current) && _pages.ContainsKey(_current) && _current != key)
            {
                try { _pages[_current].OnHide(); } catch { }
            }
            _current = key;
            var p = PageFor(key);
            _host.Content = p;
            p.OnShow();
            var settings = p as SettingsPage;
            if (settings != null && _haveUpdateResults)
                settings.SetAutomaticUpdateResults(_lastZapretLatest, _lastZapretLocal,
                    _lastTgLatest, _lastTgLocal, _lastAppLatest, Core.AppVersion);
            PaintNav();
            if (_navPanel != null) _navPanel.UpdateLayout();
            if (Theme.AnimationsEnabled && !Core.GetBool("reduce_motion", false))
            {
                // Аккуратное и плавное появление экрана (Fade + Slide)
                var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
                { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                p.BeginAnimation(OpacityProperty, fade);

                var tt = new TranslateTransform(0, 6);
                p.RenderTransform = tt;
                var slide = new System.Windows.Media.Animation.DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(180))
                { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                tt.BeginAnimation(TranslateTransform.YProperty, slide);
            }
            else
            {
                p.BeginAnimation(OpacityProperty, null);
                p.Opacity = 1;
                p.RenderTransform = null;
            }
        }

        // ---------- Публичный API для страниц ----------
        public bool IsActive2() { return Core.ServiceState() == "running" || Core.IsWinwsRunning(); }
        public string CurrentMode() { return Core.ServiceState() == "running" ? Loc.T("mode.service") : (Core.IsWinwsRunning() ? Loc.T("mode.manual") : "—"); }
        public string CurrentStrategyFile() { return _currentStrategyFile; }
        public string CurrentStrategyName()
        {
            if (Core.ServiceState() == "running") { string s = Core.ServiceStrategy(); if (!string.IsNullOrEmpty(s)) return s; }
            return string.IsNullOrEmpty(_currentStrategyFile) ? null : Core.PrettyName(_currentStrategyFile);
        }
        public string UptimeText()
        {
            if (!Core.StartedAt.HasValue || !IsActive2()) return "—";
            var d = DateTime.Now - Core.StartedAt.Value;
            if (d.TotalHours >= 1) return (int)d.TotalHours + Loc.T("time.h") + d.Minutes + Loc.T("time.m").TrimEnd();
            if (d.TotalMinutes >= 1) return d.Minutes + Loc.T("time.m") + d.Seconds + Loc.T("time.s");
            return d.Seconds + Loc.T("time.s");
        }

        public void ToggleRun()
        {
            if (IsActive2()) StopAll();
            else
            {
                if (string.IsNullOrEmpty(_currentStrategyFile))
                {
                    var files = Core.GetStrategyFiles();
                    if (files.Count == 0) { Warn(Loc.T("mw.noStrats")); return; }
                    _currentStrategyFile = files[0];
                }
                RunStrategy(_currentStrategyFile);
            }
        }

        // Выбрать стратегию без запуска. Если обход уже активен — перезапустить с новой.
        public void SelectStrategy(string file)
        {
            _currentStrategyFile = file;
            Core.Set("last_strategy", file); Core.SaveConfig();
            if (Core.IsWinwsRunning()) RunStrategy(file);
            else Core.Info(string.Format(Loc.T("mw.stratSelected"), Core.PrettyName(file)));
            RefreshTop();
        }

        public void RunStrategy(string file)
        {
            if (!Core.IsAdmin()) { NeedAdmin(Loc.T("mw.startBypassAct")); return; }
            if (_overview != null) _overview.SetZapretTransition(true);
            Dispatcher.BeginInvoke((Action)delegate { RunStrategyNow(file); }, DispatcherPriority.Background);
        }

        void RunStrategyNow(string file)
        {
            if (!Core.TryBeginWinwsOperation())
            {
                Warn(Loc.T("mw.busy"));
                if (_overview != null) _overview.SetZapretTransition(false);
                return;
            }
            try
            {
                Core.Info(string.Format(Loc.T("mw.startingLog"), Core.PrettyName(file)));
                if (Core.ServiceState() == "running" && !Core.StopService())
                    throw new Exception("Could not stop Windows service");
                Core.KillWinws();
                if (!Core.StartWinws(file)) throw new Exception("winws.exe was not started");
                _currentStrategyFile = file;
                Core.Set("last_strategy", file); Core.SaveConfig();
                Core.Good(string.Format(Loc.T("mw.startedToast"), Core.PrettyName(file)));
                Notify(Loc.T("mw.startedTitle"), Core.PrettyName(file));
                ShowToast(string.Format(Loc.T("mw.startedToast"), Core.PrettyName(file)), Sev.Ok);
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.startErr"), ex.Message)); }
            finally
            {
                Core.EndWinwsOperation();
                if (_overview != null) _overview.SetZapretTransition(false);
            }
            RefreshTop();
        }

        public void StopAll()
        {
            if (!Core.TryBeginWinwsOperation()) { Warn(Loc.T("mw.busy")); return; }
            try
            {
                if (Core.ServiceState() == "running" && !Core.StopService())
                    throw new Exception("Could not stop Windows service");
                if (!Core.KillWinws()) throw new Exception("Could not stop winws.exe");
                Core.Info(Loc.T("mw.stopped"));
                Notify(Loc.T("mw.stoppedTitle"), Loc.T("mw.stoppedBody"));
                ShowToast(Loc.T("mw.stopped"), Sev.Warn);
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.stopErr"), ex.Message)); }
            finally { Core.EndWinwsOperation(); }
            RefreshTop();
        }

        public void RestartCurrent()
        {
            if (string.IsNullOrEmpty(_currentStrategyFile)) { Warn(Loc.T("mw.noStratSel")); return; }
            Core.Info(Loc.T("mw.restarting"));
            StopAll();
            RunStrategy(_currentStrategyFile);
        }

        void NeedAdmin(string what)
        {
            MessageBox.Show(string.Format(Loc.T("mw.needAdminMsg"), what), Loc.T("service.noAdmin.title"),
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Core.Warn(string.Format(Loc.T("mw.needAdminLog"), what));
        }

        void Warn(string t) { Core.Warn(t); }

        void RefreshTop()
        {
            bool on = IsActive2();
            var np = Pill.Make(on ? Sev.Ok : Sev.Neutral, on ? Loc.T("state.running") : Loc.T("state.stopped"));
            np.VerticalAlignment = VerticalAlignment.Center;
            np.Margin = new Thickness(16, 0, 0, 0);
            var g = _topStatusPill.Parent as Grid;
            if (g != null)
            {
                int idx = g.Children.IndexOf(_topStatusPill);
                if (idx >= 0)
                {
                    Grid.SetColumn(np, 1);
                    g.Children.RemoveAt(idx);
                    g.Children.Insert(idx, np);
                    _topStatusPill = np;
                }
            }
            UpdateTray(on);
        }

        // ---------- Обновления ----------
        volatile bool _updating;
        volatile bool _checkingUpdates;
        bool _haveUpdateResults;
        string _lastZapretLatest, _lastZapretLocal, _lastTgLatest, _lastTgLocal, _lastAppLatest;

        void SetZapretUpdateProgress(string phase, int percent)
        {
            Page settingsPage;
            if (_pages.TryGetValue("settings", out settingsPage))
            {
                var settings = settingsPage as SettingsPage;
                if (settings != null) settings.SetZapretUpdateProgress(phase, percent);
            }
        }

        void FinishZapretUpdate(string text, bool ok)
        {
            Page settingsPage;
            if (_pages.TryGetValue("settings", out settingsPage))
            {
                var settings = settingsPage as SettingsPage;
                if (settings != null) settings.FinishZapretUpdate(text, ok);
            }
        }

        public void CheckUpdates()
        {
            if (_checkingUpdates) return;
            _checkingUpdates = true;
            Core.Info(Loc.T("mw.checkVer"));
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // zapret
                    string latest = Core.CheckLatestVersion();
                    string local = Core.ZapretVersion();
                    // TG proxy
                    string tgLatest = Core.TgProxyLatestVersion();
                    string tgLocal = Core.TgProxyInstalled() ? Core.TgProxyLocalVersion() : null;
                    // app
                    string appLatest = Core.AppLatestVersion();

                    Dispatcher.Invoke((Action)delegate
                    {
                        _lastZapretLatest = latest; _lastZapretLocal = local;
                        _lastTgLatest = tgLatest; _lastTgLocal = tgLocal;
                        _lastAppLatest = appLatest; _haveUpdateResults = true;
                        string zapLatestNorm = SettingsPage.NormVer(latest);
                        string zapLocalNorm = SettingsPage.NormVer(local);

                        // --- zapret ---
                        if (latest == null) { Core.Warn(Loc.T("mw.verFail")); }
                        else if (SettingsPage.CompareVersions(latest, local) == 0)
                        {
                            Core.Good(string.Format(Loc.T("mw.zapVerOk"), zapLocalNorm));
                            _updateLine.Text = string.Format(Loc.T("mw.verCurrent"), zapLocalNorm);
                        }
                        else if (SettingsPage.CompareVersions(latest, local) < 0)
                        {
                            Core.Info(string.Format(Loc.T("mw.zapLocalNewer"), zapLocalNorm, zapLatestNorm));
                            _updateLine.Text = string.Format(Loc.T("mw.verLine"), zapLocalNorm);
                        }
                        else
                        {
                            Core.Warn(string.Format(Loc.T("mw.zapVerNew"), zapLatestNorm, zapLocalNorm));
                            _updateLine.Text = string.Format(Loc.T("mw.verUpdate"), zapLocalNorm);
                        }

                        // --- TG proxy ---
                        if (Core.TgProxyInstalled() && !string.IsNullOrEmpty(tgLocal))
                        {
                            string tgLv = SettingsPage.NormVer(tgLocal);
                            string tgLt = SettingsPage.NormVer(tgLatest);
                            if (tgLatest != null && SettingsPage.CompareVersions(tgLatest, tgLocal) > 0)
                            {
                                _tgVerSidebar.Text = string.Format(Loc.T("mw.tgVer.update"), tgLv);
                                Core.Warn(string.Format(Loc.T("mw.tgVerNew"), tgLt, tgLv));
                            }
                            else
                            {
                                _tgVerSidebar.Text = string.Format(Loc.T("mw.tgVer"), tgLv);
                                Core.Good(string.Format(Loc.T("mw.tgVerOk"), tgLv));
                            }
                        }

                        // --- app ---
                        string appLv = SettingsPage.NormVer(Core.AppVersion);
                        string appLt = SettingsPage.NormVer(appLatest);
                        if (appLatest != null && SettingsPage.CompareVersions(appLatest, Core.AppVersion) > 0)
                            Core.Warn(string.Format(Loc.T("mw.appVerNew"), appLt, appLv));
                        else if (appLatest != null && SettingsPage.CompareVersions(appLatest, Core.AppVersion) == 0)
                            Core.Good(string.Format(Loc.T("mw.appVerOk"), appLv));
                        else if (appLatest != null)
                            Core.Info(string.Format(Loc.T("mw.appLocalNewer"), appLv, appLt));
                        UpdateAppSidebarVersion();

                        Page settingsPage;
                        if (_pages.TryGetValue("settings", out settingsPage))
                        {
                            var settings = settingsPage as SettingsPage;
                            if (settings != null)
                                settings.SetAutomaticUpdateResults(latest, local, tgLatest, tgLocal, appLatest, Core.AppVersion);
                        }
                    });
                }
                catch { }
                finally { _checkingUpdates = false; }
            });
        }

        void DoUpdate(string ver)
        {
            if (_updating) return;
            _updating = true;
            bool restartService = Core.ServiceState() == "running";
            bool restartManual = !restartService && Core.IsWinwsRunning();
            string restartStrategy = _currentStrategyFile;
            if (restartService || restartManual) StopAll();
            string zip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zapret_update.zip");
            string root = Core.Root;
            Core.Info(string.Format(Loc.T("mw.updStart"), ver));
            _updateLine.Text = Loc.T("mw.updProgress");
            SetZapretUpdateProgress(Loc.T("settings.update.downloading"), 0);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string url = Core.ZapretDownloadUrl(ver);
                    bool ok = Core.DownloadFile(url, zip, delegate (DlProgress p)
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                if (p.Failed) { _updateLine.Text = string.Format(Loc.T("dl.dlErr"), p.Error); return; }
                                string pct = p.Total > 0 ? " (" + (int)((double)p.BytesRead / p.Total * 100) + "%)" : "";
                                _updateLine.Text = Loc.T("mw.updProgress") + " " + Core.HumanSize(p.BytesRead) +
                                    (p.Total > 0 ? " / " + Core.HumanSize(p.Total) : "") + pct;
                                int progress = p.Total > 0 ? (int)(p.BytesRead * 88 / p.Total) : -1;
                                SetZapretUpdateProgress(Loc.T("settings.update.downloading"), progress);
                            });
                        }
                        catch { }
                    }, null);

                    if (!ok)
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = Loc.T("mw.updFail");
                            FinishZapretUpdate(Loc.T("mw.updFail"), false);
                            Core.Fail(Loc.T("mw.updFail"));
                        });
                        return;
                    }

                    string err;
                    bool ex = Core.ExtractZapretZip(zip, root, out err, delegate (string stage)
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                bool extract = stage == "extract";
                                _updateLine.Text = extract ? Loc.T("dl.extracting") : Loc.T("settings.update.replacing");
                                SetZapretUpdateProgress(Loc.T(extract ? "settings.update.unpacking" : "settings.update.replacing"),
                                    extract ? 90 : 96);
                            });
                        }
                        catch { }
                    });
                    try { System.IO.File.Delete(zip); } catch { }

                    string changelog = null;
                    if (ex) { try { changelog = Core.FetchChangelog(); } catch { } }

                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ex)
                        {
                            string newVer = Core.ZapretVersion();
                            _lastZapretLatest = newVer;
                            _lastZapretLocal = newVer;
                            _updateLine.Text = string.Format(Loc.T("mw.verCurrent"), newVer);
                            FinishZapretUpdate(Loc.T("settings.update.done") + ": " + newVer, true);
                            Core.Good(string.Format(Loc.T("mw.updDone"), newVer));
                            RefreshSidebarVersions();
                            string msg = string.Format(Loc.T("mw.updDone"), newVer);
                            if (!string.IsNullOrEmpty(changelog))
                                msg += "\n\n" + Loc.T("mw.changelog") + ":\n" + changelog;
                            MessageBox.Show(msg, Loc.T("mw.verDlgTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                            if (restartService && Core.ServiceExists()) Core.StartService();
                            else if (restartManual && !string.IsNullOrEmpty(restartStrategy)) RunStrategy(restartStrategy);
                            Page strategiesPage;
                            if (_pages.TryGetValue("strategies", out strategiesPage))
                            {
                                var sp = strategiesPage as StrategiesPage;
                                if (sp != null) sp.Rebuild();
                            }
                        }
                        else
                        {
                            _updateLine.Text = Loc.T("mw.updFail");
                            FinishZapretUpdate(Loc.T("mw.updFail"), false);
                            Core.Fail(string.Format(Loc.T("dl.failExtract"), err != null ? ": " + err : ""));
                        }
                    });
                }
                catch { }
                finally { try { Dispatcher.Invoke((Action)delegate { _updating = false; }); } catch { _updating = false; } }
            });
        }

        public void UpdateZapret(string latestVersion)
        {
            if (string.IsNullOrEmpty(latestVersion)) { Core.Warn(Loc.T("mw.verFail")); return; }
            if (SettingsPage.CompareVersions(latestVersion, Core.ZapretVersion()) <= 0) return;
            DoUpdate(latestVersion);
        }

        void AfterLoad()
        {
            // Проверка всегда выполняется в фоне при каждом входе в приложение.
            // Она ничего сама не скачивает и не показывает модальные окна.
            CheckUpdates();
            if (Core.GetBool("autostart_run", false) && !string.IsNullOrEmpty(_currentStrategyFile) && Core.IsAdmin())
                RunStrategy(_currentStrategyFile);
            RefreshTop();
            if (!Core.IsAdmin())
                Core.Warn(Loc.T("mw.noAdminWarn"));
            StartWatchdog();
            StartBypassMonitor();
        }

        // ---------- Автопереключение (watchdog) ----------
        DispatcherTimer _watchdogTimer;
        volatile bool _watchdogBusy;

        void StartWatchdog()
        {
            if (_watchdogTimer != null) { _watchdogTimer.Stop(); _watchdogTimer = null; }
            if (!Core.WatchdogEnabled) return;
            int min = Core.WatchdogIntervalMin;
            if (min < 1) min = 1;
            _watchdogTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(min) };
            _watchdogTimer.Tick += (s, e) => WatchdogTick();
            _watchdogTimer.Start();
            Core.Info(string.Format(Loc.T("mw.watchdogOn"), min));
        }

        public void RestartWatchdog() { StartWatchdog(); }

        // ---------- Монитор падения обхода (лёгкий, без переключения) ----------
        DispatcherTimer _bypassMonitor;
        bool _wasRunning;

        void StartBypassMonitor()
        {
            if (_bypassMonitor != null) { _bypassMonitor.Stop(); _bypassMonitor = null; }
            _wasRunning = Core.IsWinwsRunning();
            _bypassMonitor = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _bypassMonitor.Tick += (s, e) =>
            {
                bool now = Core.IsWinwsRunning();
                if (_wasRunning && !now)
                    Notify(Loc.T("mw.bypassDown.title"), Loc.T("mw.bypassDown.body"));
                _wasRunning = now;
            };
            _bypassMonitor.Start();
        }

        void WatchdogTick()
        {
            if (_watchdogBusy) return;
            if (!IsActive2() || string.IsNullOrEmpty(_currentStrategyFile)) return;
            _watchdogBusy = true;
            Core.Info(Loc.T("mw.watchdogCheck"));

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    bool ok = Core.QuickCheck();
                    if (ok) return;
                    // Текущая стратегия не работает — ищем замену.
                    Dispatcher.Invoke((Action)delegate { Core.Warn(Loc.T("mw.watchdogFail")); });
                    string next = Core.FindWorkingStrategy(_currentStrategyFile, () => false);
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (next != null)
                        {
                            Core.Info(string.Format(Loc.T("mw.watchdogSwitch"), Core.PrettyName(next)));
                            RunStrategy(next);
                            Notify(Loc.T("mw.watchdogSwitchTitle"), Core.PrettyName(next));
                        }
                        else
                        {
                            Core.Fail(Loc.T("mw.watchdogNone"));
                            Notify(Loc.T("mw.watchdogNoneTitle"), Loc.T("mw.watchdogNone"));
                        }
                    });
                }
                catch { }
                finally { _watchdogBusy = false; }
            });
        }

        // ---------- Системный трей ----------
        void BuildTray()
        {
            try
            {
                _tray = new System.Windows.Forms.NotifyIcon();
                _tray.Text = Core.AppName;
                _tray.Icon = Core.AppIcon() ?? System.Drawing.SystemIcons.Shield;
                _tray.Visible = true;
                var menu = new System.Windows.Forms.ContextMenuStrip();
                _trayStatus = new System.Windows.Forms.ToolStripMenuItem(Loc.T("state.stopped")) { Enabled = false };
                menu.Items.Add(_trayStatus);
                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                Add(menu, Loc.T("mw.tray.toggle"), delegate { Dispatcher.Invoke((Action)ToggleRun); });
                Add(menu, Loc.T("mw.tray.restart"), delegate { Dispatcher.Invoke((Action)RestartCurrent); });
                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                Add(menu, Loc.T("mw.tray.check"), delegate { Dispatcher.Invoke((Action)delegate { ShowWindow(); Navigate("check"); }); });
                Add(menu, Loc.T("mw.tray.open"), delegate { Dispatcher.Invoke((Action)ShowWindow); });
                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                Add(menu, Loc.T("mw.tray.exit"), delegate { Dispatcher.Invoke((Action)delegate { _forceClose = true; Close(); }); });
                _tray.ContextMenuStrip = menu;
                _tray.MouseClick += (s, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left)
                        Dispatcher.BeginInvoke((Action)ToggleTrayWidget);
                };
                _tray.DoubleClick += (s, e) => Dispatcher.Invoke((Action)ShowWindow);
            }
            catch { }
        }

        System.Windows.Forms.ToolStripMenuItem _trayStatus;
        TrayStatusWidget _trayWidget;
        bool _forceClose;

        void Add(System.Windows.Forms.ContextMenuStrip m, string text, Action act)
        {
            var it = new System.Windows.Forms.ToolStripMenuItem(text);
            it.Click += (s, e) => act();
            m.Items.Add(it);
        }

        void UpdateTray(bool on)
        {
            if (_trayStatus != null) _trayStatus.Text = on ? Loc.T("state.running") + " · " + (CurrentStrategyName() ?? "") : Loc.T("state.stopped");
            if (_trayWidget != null) _trayWidget.Refresh();
        }

        void Notify(string title, string text)
        {
            if (!Core.GetBool("notifications", true)) return;
            try { if (_tray != null) { _tray.BalloonTipTitle = title; _tray.BalloonTipText = text; _tray.ShowBalloonTip(2500); } } catch { }
        }

        void ShowWindow()
        {
            Show(); WindowState = WindowState.Normal; Activate(); Topmost = true; Topmost = false;
        }

        // Используется виджетом: сам виджет не знает о внутренней навигации окна.
        public void ShowWindowFromTray() { ShowWindow(); }

        public void ToggleTgProxyFromTray()
        {
            if (!Core.TgProxyInstalled())
            {
                ShowWindow();
                Navigate("overview");
                return;
            }
            if (Core.TgProxyRunning())
            {
                Core.TgProxyStop();
                Core.Info(Loc.T("tg.stoppedOk"));
            }
            else
            {
                string error;
                if (Core.TgProxyStart(out error)) Core.Good(Loc.T("tg.startedOk"));
                else Core.Fail(string.Format(Loc.T("tg.startErr"), error));
            }
            RefreshTop();
        }

        void ToggleTrayWidget()
        {
            if (_trayWidget == null) _trayWidget = new TrayStatusWidget(this);
            if (_trayWidget.IsVisible) _trayWidget.Hide();
            else _trayWidget.ShowAtCursor();
        }

        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_forceClose && Core.GetBool("tray_on_close", true))
            {
                e.Cancel = true;
                Hide();
                Notify(string.Format(Loc.T("mw.tray.minTitle"), Core.AppName), Loc.T("mw.tray.minBody"));
                return;
            }
            try { if (_watchdogTimer != null) _watchdogTimer.Stop(); } catch { }
            try { if (_bypassMonitor != null) _bypassMonitor.Stop(); } catch { }
            StopPeterSong();
            foreach (var p in _pages.Values) { try { p.OnHide(); } catch { } }
            Loc.LanguageChanged -= OnUiChanged;
            Theme.ThemeChanged -= OnUiChanged;
            try { if (_trayWidget != null) { _trayWidget.Close(); _trayWidget = null; } } catch { }
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } } catch { }
        }
    }
}
