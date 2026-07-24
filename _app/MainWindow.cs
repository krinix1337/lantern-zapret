using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
        bool _serviceMode;             // запущено как служба
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
            var hostBorder = new Border { Child = _host, Background = Theme.BrBgDeep };
            Grid.SetRow(hostBorder, 1); Grid.SetColumn(hostBorder, 1);
            root.Children.Add(hostBorder);

            _rootGrid = root;
            Content = root;
        }

        Grid _rootGrid;

        // In-app toast уведомление (всплывает внизу справа, исчезает через 3 сек).
        public void ShowToast(string text, Sev sev)
        {
            if (!Core.GetBool("notifications", true)) return;
            if (_rootGrid == null) return;
            Color c = sev == Sev.Ok ? Theme.Ok : sev == Sev.Warn ? Theme.Warn : sev == Sev.Err ? Theme.Err : Theme.AccentMain;
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(UI.Icon(sev == Sev.Ok ? Icons.Check : sev == Sev.Err ? Icons.Cross : Icons.Info, 16, Theme.Frozen(c), 1.8));
            var tb = new TextBlock { Text = text, Foreground = Theme.BrText, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 320, TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(tb);
            var bd = new Border { Background = Theme.BrSurface, BorderBrush = Theme.Alpha(c, 100),
                BorderThickness = new Thickness(1), CornerRadius = Theme.R10, Padding = new Thickness(16, 12, 16, 12),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 20, 20) };
            bd.Child = sp;
            Grid.SetRow(bd, 1); Grid.SetColumn(bd, 1);
            _rootGrid.Children.Add(bd);

            var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400))
            { BeginTime = TimeSpan.FromSeconds(3) };
            fade.Completed += (s, e) => { try { _rootGrid.Children.Remove(bd); } catch { } };
            bd.BeginAnimation(OpacityProperty, fade);
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
            Core.Set("theme", next == ThemeMode.Light ? "light" : next == ThemeMode.Amoled ? "amoled" : "dark");
            Core.SaveConfig();
            Theme.Apply(next);
        }

        Border _sidebar;
        StackPanel _navPanel;
        readonly System.Collections.Generic.List<TextBlock> _navLabels = new System.Collections.Generic.List<TextBlock>();
        StackPanel _bottomBox;
        Button _collapseBtn;
        bool _collapsed;
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
            var sv = new ScrollViewer { Content = nav, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(sv, 0); outer.Children.Add(sv);

            // низ: кнопка сворачивания + версия, github, обновление
            var bottomWrap = new StackPanel { Margin = new Thickness(12, 8, 12, 12) };

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
            _updateLine = new TextBlock { Text = string.Format(Loc.T("mw.verLine"), Core.ZapretVersion()), Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap };
            _bottomBox.Children.Add(_updateLine);
            _tgVerSidebar = new TextBlock { Text = TgSidebarText(), Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0) };
            _bottomBox.Children.Add(_tgVerSidebar);
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
            var appv = new TextBlock { Text = string.Format(Loc.T("mw.appShell"), Core.AppVersion), Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, Margin = new Thickness(0, 10, 0, 0) };
            _bottomBox.Children.Add(appv);
            bottomWrap.Children.Add(_bottomBox);

            Grid.SetRow(bottomWrap, 1); outer.Children.Add(bottomWrap);

            _sidebar = new Border { Child = outer, BorderBrush = Theme.BrStroke, BorderThickness = new Thickness(0, 0, 1, 0),
                Width = _collapsed ? SideNarrow : SideWide };
            ApplyCollapsedState(false);
            return _sidebar;
        }

        System.Windows.Shapes.Path _collapseIcon;
        TextBlock _collapseLabel;
        StackPanel _collapseSp;
        Border _collapseBd;

        void ToggleCollapse()
        {
            _collapsed = !_collapsed;
            Core.SetBool("sidebar_collapsed", _collapsed); Core.SaveConfig();
            ApplyCollapsedState(true);
        }

        void ApplyCollapsedState(bool animate)
        {
            double target = _collapsed ? SideNarrow : SideWide;
            if (_collapseLabel != null) _collapseLabel.Text = _collapsed ? "" : Loc.T("mw.collapse");
            // Скрываем текстовые подписи в свёрнутом виде; иконки остаются.
            foreach (var lbl in _navLabels)
                lbl.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
            // В свёрнутом виде центрируем иконки и убираем горизонтальные отступы,
            // иначе иконка (18px) + отступы (24px) + поля (24px) не влезают в 64px и обрезаются.
            foreach (var kv in _navButtons)
            {
                var arr = (object[])kv.Value.Tag;
                var bd = (Border)arr[0]; var sp = (StackPanel)arr[3];
                bd.Padding = _collapsed ? new Thickness(0, 10, 0, 10) : new Thickness(12, 10, 12, 10);
                sp.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            }
            // Уменьшаем горизонтальные поля навигационной панели в свёрнутом виде,
            // чтобы иконки (18px) гарантированно помещались в 64px без обрезки.
            if (_navPanel != null)
                _navPanel.Margin = _collapsed ? new Thickness(4, 12, 4, 0) : new Thickness(12, 12, 12, 0);
            if (_collapseSp != null)
                _collapseSp.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            if (_collapseBd != null)
                _collapseBd.Padding = _collapsed ? new Thickness(0, 9, 0, 9) : new Thickness(12, 9, 12, 9);
            if (_bottomBox != null) _bottomBox.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
            if (_sidebar == null) return;
            if (animate && Theme.AnimationsEnabled)
            {
                var a = new System.Windows.Media.Animation.DoubleAnimation(_sidebar.ActualWidth, target, TimeSpan.FromMilliseconds(220))
                { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut } };
                _sidebar.BeginAnimation(FrameworkElement.WidthProperty, a);
            }
            else
            {
                _sidebar.BeginAnimation(FrameworkElement.WidthProperty, null);
                _sidebar.Width = target;
            }
        }

        TextBlock _updateLine;
        TextBlock _tgVerSidebar;

        static string TgSidebarText()
        {
            if (!Core.TgProxyInstalled()) return Loc.T("mw.tgVer.none");
            string v = Core.TgProxyLocalVersion();
            return string.Format(Loc.T("mw.tgVer"), string.IsNullOrEmpty(v) ? "?" : v.TrimStart('v', 'V'));
        }

        Button NavItem(string key, string icon, string label)
        {
            var b = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 4), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R8, Padding = new Thickness(12, 10, 12, 10), Background = Brushes.Transparent };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = UI.Icon(icon, 18, Theme.BrMuted, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
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
            b.MouseEnter += (s, e) => { if (_current != key) bd.Background = Theme.BrSurface; };
            b.MouseLeave += (s, e) => { if (_current != key) bd.Background = Brushes.Transparent; };
            b.Click += (s, e) => Navigate(key);
            Ctl.AutomationSetName(b, label);
            _navButtons[key] = b;
            return b;
        }

        string _activeNavKey;

        void PaintNav()
        {
            // Единый цикл по всем пунктам — тот же паттерн, что в PaintTabs (CheckPage).
            // Без BeginAnimation и UpdateLayout: они могут конфликтовать с рендерингом WPF.
            foreach (var kv in _navButtons)
            {
                var arr = (object[])kv.Value.Tag;
                var bd = (Border)arr[0];
                var ic = (System.Windows.Shapes.Path)arr[1];
                var tb = (TextBlock)arr[2];
                bool on = kv.Key == _current;
                bd.Background = on ? Theme.BrAccent : Brushes.Transparent;
                ic.Stroke = on ? Theme.BrOnAccent : Theme.BrMuted;
                tb.Foreground = on ? Theme.BrOnAccent : Theme.BrMuted;
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
            PaintNav();
            if (_navPanel != null) _navPanel.UpdateLayout();
            if (Theme.AnimationsEnabled && !Core.GetBool("reduce_motion", false))
            {
                // Плавное появление: затухание + лёгкий подъём снизу вверх.
                var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                p.BeginAnimation(OpacityProperty, fade);

                var tt = new TranslateTransform(0, 10);
                p.RenderTransform = tt;
                var slide = new System.Windows.Media.Animation.DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240))
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
        public bool IsActive2() { return _serviceMode ? Core.ServiceState() == "running" : Core.IsWinwsRunning(); }
        public string CurrentMode() { return _serviceMode ? Loc.T("mode.service") : (Core.IsWinwsRunning() ? Loc.T("mode.manual") : "—"); }
        public string CurrentStrategyFile() { return _currentStrategyFile; }
        public string CurrentStrategyName()
        {
            if (_serviceMode) { string s = Core.ServiceStrategy(); if (!string.IsNullOrEmpty(s)) return s; }
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
            if (IsActive2() && !_serviceMode) RunStrategy(file);
            else Core.Info(string.Format(Loc.T("mw.stratSelected"), Core.PrettyName(file)));
            RefreshTop();
        }

        public void RunStrategy(string file)
        {
            if (!Core.IsAdmin()) { NeedAdmin(Loc.T("mw.startBypassAct")); return; }
            try
            {
                Core.KillWinws();
                _serviceMode = false;
                Core.StartWinws(file);
                _currentStrategyFile = file;
                Core.Set("last_strategy", file); Core.SaveConfig();
                Core.Good(string.Format(Loc.T("mw.startedToast"), Core.PrettyName(file)));
                Notify(Loc.T("mw.startedTitle"), Core.PrettyName(file));
                ShowToast(string.Format(Loc.T("mw.startedToast"), Core.PrettyName(file)), Sev.Ok);
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.startErr"), ex.Message)); }
            RefreshTop();
        }

        public void StopAll()
        {
            try
            {
                if (_serviceMode && Core.ServiceState() == "running") Core.StopService();
                Core.KillWinws();
                _serviceMode = false;
                Core.Info(Loc.T("mw.stopped"));
                Notify(Loc.T("mw.stoppedTitle"), Loc.T("mw.stoppedBody"));
                ShowToast(Loc.T("mw.stopped"), Sev.Warn);
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.stopErr"), ex.Message)); }
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

        Grid VisualParentGrid(DependencyObject el)
        {
            var p = System.Windows.Media.VisualTreeHelper.GetParent(el);
            while (p != null && !(p is Grid)) p = System.Windows.Media.VisualTreeHelper.GetParent(p);
            return p as Grid;
        }

        // ---------- Обновления ----------
        volatile bool _updating;

        public void CheckUpdates()
        {
            if (_updating) return;
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
                        // --- zapret ---
                        if (latest == null) { Core.Warn(Loc.T("mw.verFail")); }
                        else if (SettingsPage.NormVer(latest) == SettingsPage.NormVer(local))
                        {
                            Core.Good(string.Format(Loc.T("mw.verOk"), latest));
                            _updateLine.Text = string.Format(Loc.T("mw.verCurrent"), local);
                        }
                        else
                        {
                            Core.Warn(string.Format(Loc.T("mw.verNew"), latest, local));
                            _updateLine.Text = string.Format(Loc.T("mw.verUpdate"), local);
                            var r = MessageBox.Show(string.Format(Loc.T("mw.verDlg"), latest, local),
                                Loc.T("mw.verDlgTitle"), MessageBoxButton.YesNo, MessageBoxImage.Information);
                            if (r == MessageBoxResult.Yes) DoUpdate(latest);
                        }

                        // --- TG proxy ---
                        if (Core.TgProxyInstalled() && !string.IsNullOrEmpty(tgLocal))
                        {
                            string tgLv = tgLocal.TrimStart('v', 'V');
                            if (tgLatest != null && SettingsPage.NormVer(tgLatest) != SettingsPage.NormVer(tgLocal))
                            {
                                _tgVerSidebar.Text = string.Format(Loc.T("mw.tgVer.update"), tgLv);
                                Core.Warn(string.Format(Loc.T("mw.verNew"), tgLatest, tgLv));
                            }
                            else
                            {
                                _tgVerSidebar.Text = string.Format(Loc.T("mw.tgVer"), tgLv);
                                Core.Good(string.Format(Loc.T("mw.verOk"), tgLv));
                            }
                        }

                        // --- app ---
                        if (appLatest != null && SettingsPage.NormVer(appLatest) != SettingsPage.NormVer(Core.AppVersion))
                            Core.Warn(string.Format(Loc.T("mw.verNew"), appLatest, Core.AppVersion));
                        else if (appLatest != null)
                            Core.Good(string.Format(Loc.T("mw.verOk"), Core.AppVersion));
                    });
                }
                catch { }
            });
        }

        void DoUpdate(string ver)
        {
            if (_updating) return;
            _updating = true;
            string zip = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zapret_update.zip");
            string root = Core.Root;
            Core.Info(string.Format(Loc.T("mw.updStart"), ver));
            _updateLine.Text = Loc.T("mw.updProgress");

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    bool ok = Core.DownloadFile(Core.ZapretZipUrl, zip, delegate (DlProgress p)
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                if (p.Failed) { _updateLine.Text = string.Format(Loc.T("dl.dlErr"), p.Error); return; }
                                string pct = p.Total > 0 ? " (" + (int)((double)p.BytesRead / p.Total * 100) + "%)" : "";
                                _updateLine.Text = Loc.T("mw.updProgress") + " " + Core.HumanSize(p.BytesRead) +
                                    (p.Total > 0 ? " / " + Core.HumanSize(p.Total) : "") + pct;
                            });
                        }
                        catch { }
                    }, null);

                    if (!ok)
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = Loc.T("mw.updFail");
                            Core.Fail(Loc.T("mw.updFail"));
                        });
                        return;
                    }

                    Dispatcher.Invoke((Action)delegate { _updateLine.Text = Loc.T("dl.extracting"); });
                    string err;
                    bool ex = Core.ExtractZapretZip(zip, root, out err);
                    try { System.IO.File.Delete(zip); } catch { }

                    string changelog = null;
                    if (ex) { try { changelog = Core.FetchChangelog(); } catch { } }

                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ex)
                        {
                            string newVer = Core.ZapretVersion();
                            _updateLine.Text = string.Format(Loc.T("mw.verCurrent"), newVer);
                            Core.Good(string.Format(Loc.T("mw.updDone"), newVer));
                            string msg = string.Format(Loc.T("mw.updDone"), newVer);
                            if (!string.IsNullOrEmpty(changelog))
                                msg += "\n\n" + Loc.T("mw.changelog") + ":\n" + changelog;
                            MessageBox.Show(msg, Loc.T("mw.verDlgTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            _updateLine.Text = Loc.T("mw.updFail");
                            Core.Fail(string.Format(Loc.T("dl.failExtract"), err != null ? ": " + err : ""));
                        }
                    });
                }
                catch { }
                finally { try { Dispatcher.Invoke((Action)delegate { _updating = false; }); } catch { _updating = false; } }
            });
        }

        void AfterLoad()
        {
            if (Core.GetBool("check_updates", true)) CheckUpdates();
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
                _tray.DoubleClick += (s, e) => Dispatcher.Invoke((Action)ShowWindow);
            }
            catch { }
        }

        System.Windows.Forms.ToolStripMenuItem _trayStatus;
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
            foreach (var p in _pages.Values) { try { p.OnHide(); } catch { } }
            Loc.LanguageChanged -= OnUiChanged;
            Theme.ThemeChanged -= OnUiChanged;
            try { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } } catch { }
        }
    }
}
