using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
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
        // Только для автотеста: раньше NavCheck ограничивался «не бросило исключение»,
        // и упавшая навигация засчитывалась как PASS.
        internal string CurrentPageKey { get { return _current; } }
        Border _topStatusPill;
        Border _topAdminPill;
        OverviewPage _overview;

        // Состояние запуска. Поле читают и пишут как UI-поток, так и фоновые
        // операции запуска стратегии — отсюда volatile.
        volatile string _currentStrategyFile;   // выбранная/запущенная стратегия
        System.Windows.Forms.NotifyIcon _tray;
        bool _maximized;                        // переживает Minimize, в отличие от WindowState
        DispatcherTimer _windowSaveTimer;

        public MainWindow()
        {
            Title = Core.AppName;
            try { var wi = Core.AppIconSource(); if (wi != null) Icon = wi; } catch { }
            double waH = SystemParameters.WorkArea.Height;
            double waW = SystemParameters.WorkArea.Width;
            // Минимальный размер — это про запас раскладки, а не про «почти весь
            // экран». Раньше min = clamp(760..920) по рабочей области, и на любом
            // мониторе окно физически нельзя было сузить ниже 920 px: за край его
            // тянешь, а оно стоит. Нижняя граница 760 — с запасом над самой плотной
            // страницей; проверяется аудитом разметки на 820 (SelfTest, 6b).
            MinHeight = 480;
            MinWidth = 760;
            Height = Math.Min(740, Math.Max(MinHeight, waH - 30));
            Width = Math.Min(1120, Math.Max(MinWidth, waW - 60));
            // Пользовательский размер переживает перезапуск. Кламп по рабочей
            // области обязателен: сохранённые габариты с другого монитора иначе
            // открыли бы окно за краем экрана.
            int savedW = Core.GetInt("window_width", 0);
            int savedH = Core.GetInt("window_height", 0);
            if (savedW >= MinWidth) Width = Math.Min(savedW, waW);
            if (savedH >= MinHeight) Height = Math.Min(savedH, waH);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _maximized = Core.GetBool("window_maximized", false);
            if (_maximized) WindowState = WindowState.Maximized;
            Background = Theme.BrBgDeep;
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Ideal);

            _currentStrategyFile = Core.Get("last_strategy", null);
            _collapsed = Core.GetBool("sidebar_collapsed", false);
            // Последние известные версии с GitHub: кэш позволяет сайдбару сразу
            // показывать «— актуально» после перезапуска, даже если стартовая
            // проверка ещё идёт или временно не удалась (лимиты GitHub API).
            _lastZapretLatest = Core.Get("latest_zapret", null);
            _lastTgLatest = Core.Get("latest_tg", null);
            _lastAppLatest = Core.Get("latest_app", null);
            if (_lastZapretLatest != null || _lastTgLatest != null || _lastAppLatest != null)
            {
                _haveUpdateResults = true;
                _lastZapretLocal = Core.ZapretVersion();
                _lastTgLocal = Core.TgProxyInstalled() ? Core.TgProxyLocalVersion() : null;
            }

            AllowsTransparency = false;
            BuildChrome();
            BuildTray();

            bool startInTray = false;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-tray", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-minimized", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/tray", StringComparison.OrdinalIgnoreCase))
                {
                    startInTray = true;
                    break;
                }
            }
            if (startInTray)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
                Visibility = Visibility.Hidden;
            }

            SourceInitialized += (s, e) =>
            {
                ApplyRoundedCorners();
                try
                {
                    var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                    if (source != null) source.AddHook(WndProc);
                }
                catch { }
            };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    ToggleCollapse();
                    e.Handled = true;
                }
            };
            StateChanged += (s, e) =>
            {
                // Состояние «развёрнуто» запоминаем отдельно: после Minimize
                // WindowState уже не говорит, было ли окно развёрнуто.
                if (IsLoaded)
                {
                    if (WindowState == WindowState.Maximized) _maximized = true;
                    else if (WindowState == WindowState.Normal) _maximized = false;
                    ScheduleWindowStateSave();
                }
                if (WindowState == WindowState.Minimized)
                {
                    PauseActivePage();
                    Core.TrimMemory();
                }
                else if (WindowState == WindowState.Normal || WindowState == WindowState.Maximized)
                {
                    ResumeActivePage();
                }
            };
            SizeChanged += (s, e) => { ScheduleWindowStateSave(); ConstrainTopStatus(); };
            Loaded += (s, e) =>
            {
                Navigate("overview");
                if (_overview != null) _overview.StartTimer();
#if SELFTEST
                // Автотест ограничивается построением интерфейса: AfterLoad ходит в
                // сеть (CheckUpdates), поднимает обход по autostart_run, запускает
                // TgWsProxy и вешает 15-секундный монитор падения.
#else
                AfterLoad();
#endif
                if (startInTray)
                {
                    PauseActivePage();
                    Hide();
                    Notify(string.Format(Loc.T("mw.tray.minTitle"), Core.AppName), Loc.T("mw.tray.minBody"));
                    Core.TrimMemory();
                }
                else
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
                    {
                        BringToFront();
                    });
                }
            };
            Closing += OnClosing;
            if (Application.Current != null) Application.Current.SessionEnding += OnSessionEnding;

            // Смена языка/темы — перестроить интерфейс.
            Loc.LanguageChanged += OnUiChanged;
            Theme.ThemeChanged += OnUiChanged;
        }

        void OnUiChanged()
        {
            try
            {
                // Страховка от застрявшего мышиного захвата: если пересборку всё же
                // запустили из обработчика выбора комбо, его Popup мог не успеть
                // закрыться, и ввод (колесо, клики) навсегда уходил в отцепленный
                // элемент. Явно отпускаем захват перед пересборкой.
                try { System.Windows.Input.Mouse.Capture(null); } catch { }

                FontFamily = Theme.UiFont;
                double savedScroll = 0;
                if (!string.IsNullOrEmpty(_current) && _pages.ContainsKey(_current))
                {
                    try { savedScroll = _pages[_current].ScrollOffset; } catch { }
                }

                // Запоминаем текущий элемент с фокусом ввода, чтобы после пересборки страницы
                // фокус не сбрасывался в самый верх окна (кнопка темы / заголовок)
                string savedFocusName = null;
                string savedFocusTag = null;
                Type savedFocusType = null;
                var curFocused = FocusManager.GetFocusedElement(this) as DependencyObject;
                if (curFocused != null)
                {
                    savedFocusName = System.Windows.Automation.AutomationProperties.GetName(curFocused);
                    savedFocusType = curFocused.GetType();
                    if (curFocused is FrameworkElement) savedFocusTag = ((FrameworkElement)curFocused).Tag as string;
                }

                RenderTargetBitmap oldBmp = null;
                if (Theme.AnimationsEnabled && ActualWidth > 0 && ActualHeight > 0 && Content is FrameworkElement)
                {
                    try
                    {
                        int rw = (int)Math.Max(1, ActualWidth);
                        int rh = (int)Math.Max(1, ActualHeight);
                        oldBmp = new RenderTargetBitmap(rw, rh, 96, 96, PixelFormats.Pbgra32);
                        oldBmp.Render((FrameworkElement)Content);
                        oldBmp.Freeze();
                    }
                    catch { oldBmp = null; }
                }

                foreach (var p in _pages.Values)
                {
                    try
                    {
                        var chk = p as CheckPage;
                        if (chk != null) chk.CancelAll();
                        p.OnHide();
                    }
                    catch { }
                }
                _pages.Clear();
                _navButtons.Clear();
                _activeNavKey = null;
                _overview = null;
                CloseFlavorPopup();
                BuildChrome();
                RebuildTray();
                try { if (_trayWidget != null) { _trayWidget.Close(); _trayWidget = null; } } catch { }
                Navigate(string.IsNullOrEmpty(_current) ? "overview" : _current, true);
                if (_overview != null) _overview.StartTimer();
                RefreshTop();

                // Восстанавливаем скролл и фокус, чтобы при смене темы/языка не выбрасывало наверх
                if (!string.IsNullOrEmpty(_current) && _pages.ContainsKey(_current))
                {
                    var curPage = _pages[_current];
                    if (savedScroll > 0) curPage.ScrollOffset = savedScroll;
                    Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)delegate
                    {
                        try
                        {
                            if (savedScroll > 0) curPage.ScrollOffset = savedScroll;
                            if (!string.IsNullOrEmpty(savedFocusTag) || !string.IsNullOrEmpty(savedFocusName))
                            {
                                var match = FindElementForRestore(curPage, savedFocusTag, savedFocusName, savedFocusType);
                                if (match != null)
                                {
                                    match.Focus();
                                    Keyboard.Focus(match);
                                }
                            }
                        }
                        catch { }
                    });
                }

                // Сочная радиальная анимация смены темы (Telegram-style Circular Reveal)
                if (Theme.AnimationsEnabled && oldBmp != null && _rootGrid != null)
                {
                    PlayCircularReveal(oldBmp);
                }
                else if (Content is UIElement)
                {
                    var el = (UIElement)Content;
                    el.BeginAnimation(OpacityProperty, null);
                    el.Opacity = 1;
                    if (el is FrameworkElement) ((FrameworkElement)el).Effect = null;
                }
            }
            catch { }
        }

        static FrameworkElement FindElementForRestore(DependencyObject parent, string tag, string name, Type type)
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement)
                {
                    var fe = (FrameworkElement)child;
                    if (type == null || fe.GetType() == type)
                    {
                        if (!string.IsNullOrEmpty(tag) && string.Equals(fe.Tag as string, tag, StringComparison.OrdinalIgnoreCase))
                            return fe;
                        if (!string.IsNullOrEmpty(name) && string.Equals(System.Windows.Automation.AutomationProperties.GetName(fe), name, StringComparison.OrdinalIgnoreCase))
                            return fe;
                    }
                }
                var sub = FindElementForRestore(child, tag, name, type);
                if (sub != null) return sub;
            }
            return null;
        }

        Point _themeOrigin = new Point(-1, -1);

        public void SetThemeOrigin(Point pt) { _themeOrigin = pt; }

        void PlayCircularReveal(RenderTargetBitmap oldBmp)
        {
            try
            {
                if (_rootGrid == null) return;

                var oldImage = new Image
                {
                    Source = oldBmp,
                    Width = ActualWidth,
                    Height = ActualHeight,
                    Stretch = Stretch.Fill,
                    IsHitTestVisible = false,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1.0, 1.0)
                };

                Point origin;
                if (_themeOrigin.X >= 0 && _themeOrigin.Y >= 0)
                {
                    origin = _themeOrigin;
                }
                else
                {
                    try
                    {
                        Point mousePt = System.Windows.Input.Mouse.GetPosition(this);
                        if (mousePt.X >= 0 && mousePt.X <= ActualWidth && mousePt.Y >= 0 && mousePt.Y <= ActualHeight)
                            origin = mousePt;
                        else
                            origin = new Point(ActualWidth > 0 ? ActualWidth - 160 : 1000, 24);
                    }
                    catch
                    {
                        origin = new Point(ActualWidth > 0 ? ActualWidth - 160 : 1000, 24);
                    }
                }
                _themeOrigin = new Point(-1, -1);

                double dX = Math.Max(origin.X, Math.Max(0, ActualWidth - origin.X));
                double dY = Math.Max(origin.Y, Math.Max(0, ActualHeight - origin.Y));
                double maxRadius = Math.Sqrt(dX * dX + dY * dY) + 36;

                var glowRing = new System.Windows.Shapes.Ellipse
                {
                    Width = 0,
                    Height = 0,
                    Stroke = Theme.BrAccentHi,
                    StrokeThickness = 3,
                    Opacity = 0.7,
                    IsHitTestVisible = false,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Theme.AccentMain,
                        BlurRadius = 16,
                        ShadowDepth = 0,
                        Opacity = 0.9
                    }
                };
                Canvas.SetLeft(glowRing, origin.X);
                Canvas.SetTop(glowRing, origin.Y);

                var overlayCanvas = new Canvas
                {
                    Width = ActualWidth,
                    Height = ActualHeight,
                    IsHitTestVisible = false,
                    ClipToBounds = true
                };
                overlayCanvas.Children.Add(oldImage);
                overlayCanvas.Children.Add(glowRing);

                Grid.SetRow(overlayCanvas, 0);
                Grid.SetRowSpan(overlayCanvas, 2);
                Grid.SetColumn(overlayCanvas, 0);
                Grid.SetColumnSpan(overlayCanvas, 2);
                Panel.SetZIndex(overlayCanvas, 9999);
                _rootGrid.Children.Add(overlayCanvas);

                var dur = TimeSpan.FromMilliseconds(400);
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var imgOpacityAnim = new DoubleAnimation(1.0, 0.0, dur) { EasingFunction = ease };
                var imgScaleAnim = new DoubleAnimation(1.0, 1.02, dur) { EasingFunction = ease };

                var ringSizeAnim = new DoubleAnimation(0, maxRadius * 2, dur) { EasingFunction = ease };
                var ringXAnim = new DoubleAnimation(origin.X, origin.X - maxRadius, dur) { EasingFunction = ease };
                var ringYAnim = new DoubleAnimation(origin.Y, origin.Y - maxRadius, dur) { EasingFunction = ease };
                var ringFade = new DoubleAnimation(0.7, 0.0, dur) { EasingFunction = ease };

                imgOpacityAnim.Completed += (s, e) =>
                {
                    try 
                    {
                        if (overlayCanvas == null) return;
                        if (_rootGrid != null && _rootGrid.Children != null)
                        {
                            _rootGrid.Children.Remove(overlayCanvas);
                        }
                        oldImage.Source = null;
                        overlayCanvas.Children.Clear();
                        oldBmp = null;
                        Core.TrimMemory();
                    } 
                    catch { }
                };

                oldImage.BeginAnimation(UIElement.OpacityProperty, imgOpacityAnim);
                var scaleTransform = oldImage.RenderTransform as ScaleTransform;
                if (scaleTransform != null)
                {
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, imgScaleAnim);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, imgScaleAnim);
                }

                glowRing.BeginAnimation(FrameworkElement.WidthProperty, ringSizeAnim);
                glowRing.BeginAnimation(FrameworkElement.HeightProperty, ringSizeAnim);
                glowRing.BeginAnimation(Canvas.LeftProperty, ringXAnim);
                glowRing.BeginAnimation(Canvas.TopProperty, ringYAnim);
                glowRing.BeginAnimation(UIElement.OpacityProperty, ringFade);
            }
            catch { }
        }

        void CleanupTray()
        {
            try { if (_trayClickTimer != null) { _trayClickTimer.Stop(); _trayClickTimer = null; } } catch { }
            try
            {
                if (_tray != null)
                {
                    _tray.Visible = false;
                    if (_tray.ContextMenuStrip != null)
                    {
                        _tray.ContextMenuStrip.Dispose();
                        _tray.ContextMenuStrip = null;
                    }
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { }
        }

        void RebuildTray()
        {
            CleanupTray();
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
            FontFamily = Theme.UiFont;
            // Кастомная рамка окна
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            double topH = Theme.S(48);
            var chrome = new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = topH, ResizeBorderThickness = new Thickness(6),
                CornerRadius = new CornerRadius(0), GlassFrameThickness = new Thickness(0)
            };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

            Background = Theme.BrBgBase;

            var root = new Grid();
            root.Background = Brushes.Transparent;
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(topH) });  // topbar
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
            contentLayer.Children.Add(_host);
            var hostBorder = new Border
            {
                Child = contentLayer,
                Background = Theme.BrBgDeep,
                CornerRadius = new CornerRadius(0),
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                Margin = new Thickness(0),
                ClipToBounds = true
            };
            Grid.SetRow(hostBorder, 1); Grid.SetColumn(hostBorder, 1);
            root.Children.Add(hostBorder);

            _toastHost = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 20, 20),
                MaxWidth = 380, IsHitTestVisible = false };
            Grid.SetRow(_toastHost, 1); Grid.SetColumn(_toastHost, 1);
            Panel.SetZIndex(_toastHost, 100);
            root.Children.Add(_toastHost);

            root.PreviewMouseWheel += (s, e) =>
            {
                if (SmoothScrollViewer.IsInsideDropdown(e.OriginalSource as DependencyObject, e.Source)) return;
                CloseFlavorPopup();
                SmoothScrollViewer.CloseAnyOpenDropdown(this);
            };

            _rootGrid = root;
            Content = root;
        }

        Grid _rootGrid;
        StackPanel _toastHost;

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
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 0: brand
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 1: _topStatusPill
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 2: _topAdminPill
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // 3: drag
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // 4: right

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(18, 0, 0, 0) };
            var logo = new Border { Width = 22, Height = 22, CornerRadius = Theme.R6,
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
            ConstrainTopStatus();

            _topAdminPill = Pill.Make(Sev.Warn, Loc.T("mw.noAdminBadge"));
            _topAdminPill.VerticalAlignment = VerticalAlignment.Center;
            _topAdminPill.Margin = new Thickness(8, 0, 0, 0);
            _topAdminPill.Cursor = System.Windows.Input.Cursors.Hand;
            _topAdminPill.ToolTip = Loc.T("mw.noAdminTip");
            _topAdminPill.MouseLeftButtonUp += (s, e) =>
            {
                if (MessageBox.Show(Loc.T("mw.restartAsAdminPrompt"), Loc.T("service.noAdmin.title"),
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    Core.RestartAsAdmin();
                }
            };
            _topAdminPill.Visibility = Core.IsAdmin() ? Visibility.Collapsed : Visibility.Visible;
            Grid.SetColumn(_topAdminPill, 2); g.Children.Add(_topAdminPill);

            // область для перетаскивания окна
            var drag = new Border { Background = Brushes.Transparent };
            drag.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 1) DragMove(); };
            Grid.SetColumn(drag, 3); g.Children.Add(drag);

            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            Button themeBtn = null;
            themeBtn = TopIcon(Theme.Mode == ThemeMode.Light ? Icons.Moon : Icons.Sun,
                Loc.T("settings.theme"), delegate
                {
                    try
                    {
                        if (themeBtn != null)
                            _themeOrigin = themeBtn.TranslatePoint(new Point(themeBtn.ActualWidth / 2, themeBtn.ActualHeight / 2), this);
                    }
                    catch { _themeOrigin = new Point(ActualWidth - 160, 24); }
                    ToggleTheme();
                });
            right.Children.Add(themeBtn);
            // Подсказка: показываем какая тема будет следующей
            string nextKey = Theme.LocKey(Theme.NextMode());
            themeBtn.ToolTip = Loc.T("settings.theme") + ": " + Loc.T(nextKey);
            if (Theme.Mode == ThemeMode.System24)
            {
                Button flavorBtn = null;
                flavorBtn = TopIcon(Icons.Palette, Loc.T("settings.system24.flavor"), delegate
                {
                    ShowSystem24FlavorMenu(flavorBtn);
                });
                flavorBtn.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Down)
                    {
                        ShowSystem24FlavorMenu(flavorBtn);
                        e.Handled = true;
                    }
                };
                right.Children.Add(flavorBtn);
            }
            // Перезапуск в шапке поднимает заново весь рабочий набор: обход в своём
            // режиме и Telegram-прокси, плюс перепроверку версий. Тему не трогает.
            _topRestartBtn = TopIcon(Icons.Restart, Loc.T("mw.restartAllTip"), delegate { RestartAll(); }, out _topRestartIcon);
            right.Children.Add(_topRestartBtn);
            right.Children.Add(TopIcon(Icons.Gear, Loc.T("common.settings"), delegate { Navigate("settings"); }));
            right.Children.Add(WinBtn(Icons.Minimize, Loc.T("common.minimize"), delegate { WindowState = WindowState.Minimized; }, false));
            right.Children.Add(WinBtn(Icons.Maximize, Loc.T("common.maximize"), delegate { ToggleMax(); }, false));
            right.Children.Add(WinBtn(Icons.Cross, Loc.T("common.close"), delegate { Close(); }, true));
            Grid.SetColumn(right, 4); g.Children.Add(right);

            return new Border
            {
                Child = g,
                Background = Theme.BrBgBase,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        Button TopIcon(string icon, string name, Action act)
        {
            System.Windows.Shapes.Path unused;
            return TopIcon(icon, name, act, out unused);
        }

        // Перегрузка отдаёт саму иконку: кнопке перезапуска она нужна, чтобы
        // крутить её всё время операции (SetTopRestartBusy).
        Button TopIcon(string icon, string name, Action act, out System.Windows.Shapes.Path iconShape)
        {
            var b = new Button { Cursor = Cursors.Hand, Width = Theme.S(38), Height = Theme.S(38), Margin = new Thickness(2, 0, 2, 0) };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R10, Background = Brushes.Transparent,
                BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
            var ic = UI.Icon(icon, Theme.S(17), Theme.BrMuted, 1.8);
            bd.Child = ic;
            b.Content = Ctl.SplitSurface(bd);
            var animType = (icon == Icons.Restart || icon == Icons.Refresh) ? IconAnimType.Rotate360 : (icon == Icons.Gear ? IconAnimType.Rotate90 : IconAnimType.ScaleBounce);
            UI.AttachIconHoverAnimation(b, ic, animType);
            b.MouseEnter += (s, e) =>
            {
                bd.Background = Theme.BrSurfaceHi;
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
            };
            b.MouseLeave += (s, e) =>
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
            };
            // WindowChrome area — FocusVisualStyle не работает, рисуем рамку вручную.
            b.GotKeyboardFocus += (s, e) =>
            {
                bd.BorderBrush = Theme.BrAccentHi;
                bd.BorderThickness = new Thickness(2);
            };
            b.LostKeyboardFocus += (s, e) =>
            {
                bd.BorderBrush = Brushes.Transparent;
                bd.BorderThickness = new Thickness(1);
            };
            b.Click += (s, e) => { if (act != null) act(); };
            Ctl.AutomationSetName(b, name);
            b.ToolTip = name;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            Ctl.AttachGlow(b, bd);
            iconShape = ic;
            return b;
        }

        Button WinBtn(string icon, string name, Action act, bool danger)
        {
            var b = new Button { Cursor = Cursors.Hand, Width = Theme.S(38), Height = Theme.S(38), Margin = new Thickness(2, 0, 2, 0) };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R10, Background = Brushes.Transparent,
                BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
            var ic = UI.Icon(icon, Theme.S(14), Theme.BrMuted, 1.6);
            bd.Child = ic;
            b.Content = bd;
            b.MouseEnter += (s, e) =>
            {
                bd.Background = danger ? Theme.Frozen(Theme.Err) : Theme.BrSurfaceHi;
                if (danger)
                {
                    if (ic.Stroke != null) ic.Stroke = Brushes.White;
                    if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = Brushes.White;
                }
                if (Theme.Mode == ThemeMode.System24 && !danger)
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
            };
            b.MouseLeave += (s, e) =>
            {
                bd.Background = Brushes.Transparent;
                if (ic.Stroke != null) ic.Stroke = Theme.BrMuted;
                if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = Theme.BrMuted;
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
            };
            // WindowChrome area не имеет AdornerLayer — FocusVisualStyle не рисуется.
            // Показываем рамку фокуса вручную через BorderBrush при навигации Tab.
            b.GotKeyboardFocus += (s, e) =>
            {
                bd.BorderBrush = Theme.BrAccentHi;
                bd.BorderThickness = new Thickness(2);
            };
            b.LostKeyboardFocus += (s, e) =>
            {
                bd.BorderBrush = Brushes.Transparent;
                bd.BorderThickness = new Thickness(1);
            };
            UI.AttachIconHoverAnimation(b, ic, IconAnimType.ScaleBounce);
            b.Click += (s, e) => { if (act != null) act(); };
            Ctl.AutomationSetName(b, name);
            b.ToolTip = name;
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            return b;
        }

        void ToggleMax() { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }

        void ToggleTheme()
        {
            var next = Theme.NextMode();
            Core.Set("theme", Theme.Name(next));
            Core.SaveConfig();
            Theme.Apply(next);
        }

        System.Windows.Controls.Primitives.Popup _system24FlavorPopup;

        public void CloseFlavorPopup()
        {
            if (_system24FlavorPopup != null)
            {
                try { _system24FlavorPopup.IsOpen = false; } catch { }
                _system24FlavorPopup = null;
            }
        }

        void ShowSystem24FlavorMenu(Button anchor)
        {
            if (anchor == null) return;
            if (_system24FlavorPopup != null && _system24FlavorPopup.IsOpen)
            {
                CloseFlavorPopup();
                return;
            }

            var pop = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = anchor,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.None
            };

            var stack = new StackPanel { Margin = new Thickness(0) };

            var headerTb = new TextBlock
            {
                Text = Loc.T("settings.system24.flavor"),
                Foreground = Theme.Frozen(Theme.TextFaint),
                FontSize = Theme.FsSmall,
                FontWeight = FontWeights.SemiBold,
                FontFamily = Theme.UiFont,
                Margin = new Thickness(12, 8, 12, 6)
            };
            stack.Children.Add(headerTb);

            stack.Children.Add(new Border
            {
                Height = 1,
                Background = Theme.Frozen(Theme.StrokeSoft),
                Margin = new Thickness(0, 0, 0, 4)
            });

            string currentFlavor = Theme.System24Flavor;
            Button focusTarget = null;

            foreach (var f in Theme.System24Flavors)
            {
                string flavorKey = f;
                bool isSelected = string.Equals(flavorKey, currentFlavor, StringComparison.OrdinalIgnoreCase);

                var row = new Grid { MinWidth = 200 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

                Color dotColor = Theme.GetFlavorAccent(flavorKey);
                var dot = new System.Windows.Shapes.Rectangle
                {
                    Width = 8,
                    Height = 8,
                    Fill = Theme.Frozen(dotColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(dot, 0);
                row.Children.Add(dot);

                var tb = new TextBlock
                {
                    Text = Theme.GetFlavorName(flavorKey),
                    Foreground = isSelected ? Theme.Frozen(dotColor) : Theme.Frozen(Theme.Text),
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                    FontSize = Theme.FsBody,
                    FontFamily = Theme.UiFont,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 6, 0)
                };
                Grid.SetColumn(tb, 1);
                row.Children.Add(tb);

                if (isSelected)
                {
                    var chk = UI.Icon(Icons.Check, 13, Theme.Frozen(dotColor), 2.0);
                    chk.VerticalAlignment = VerticalAlignment.Center;
                    chk.HorizontalAlignment = HorizontalAlignment.Center;
                    Grid.SetColumn(chk, 2);
                    row.Children.Add(chk);
                }

                var itemBtn = new Button
                {
                    Background = isSelected ? Theme.Alpha(dotColor, 28) : (Brush)Brushes.Transparent,
                    BorderBrush = isSelected ? Theme.Alpha(dotColor, 80) : (Brush)Brushes.Transparent,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 7, 8, 7),
                    Margin = new Thickness(3, 1, 3, 1),
                    Cursor = Cursors.Hand,
                    Content = row,
                    FocusVisualStyle = null
                };
                Ctl.StripChrome(itemBtn);
                itemBtn.FocusVisualStyle = null;
                Ctl.AutomationSetName(itemBtn, Theme.GetFlavorName(flavorKey));

                // Ручной шаблон: Border с привязкой к свойствам кнопки
                var tmpl = new ControlTemplate(typeof(Button));
                var tBd = new FrameworkElementFactory(typeof(Border));
                tBd.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                tBd.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                tBd.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                tBd.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
                var tCp = new FrameworkElementFactory(typeof(ContentPresenter));
                tBd.AppendChild(tCp);
                tmpl.VisualTree = tBd;
                itemBtn.Template = tmpl;

                itemBtn.MouseEnter += (s, e) =>
                {
                    if (!itemBtn.IsKeyboardFocused && !isSelected) itemBtn.Background = Theme.Frozen(Theme.SurfaceHi);
                };
                itemBtn.MouseLeave += (s, e) =>
                {
                    if (!itemBtn.IsKeyboardFocused && !isSelected) itemBtn.Background = Brushes.Transparent;
                };

                // Подсветка и автопрокрутка при Tab/Arrow навигации
                itemBtn.GotKeyboardFocus += (s, e) =>
                {
                    itemBtn.BorderBrush = Theme.BrAccentHi;
                    itemBtn.Background = isSelected ? Theme.Alpha(dotColor, 45) : Theme.Frozen(Theme.SurfaceHi);
                    itemBtn.BringIntoView();
                };
                itemBtn.LostKeyboardFocus += (s, e) =>
                {
                    itemBtn.BorderBrush = isSelected ? Theme.Alpha(dotColor, 80) : (Brush)Brushes.Transparent;
                    itemBtn.Background = isSelected ? Theme.Alpha(dotColor, 28) : (Brush)Brushes.Transparent;
                };

                if (isSelected) focusTarget = itemBtn;

                itemBtn.Click += (s, e) =>
                {
                    pop.IsOpen = false;
                    try
                    {
                        if (anchor != null)
                            _themeOrigin = anchor.TranslatePoint(new Point(anchor.ActualWidth / 2, anchor.ActualHeight / 2), this);
                    }
                    catch { _themeOrigin = new Point(ActualWidth - 160, 24); }

                    Theme.ApplySystem24Flavor(flavorKey);
                    if (anchor != null) anchor.Focus();
                };

                stack.Children.Add(itemBtn);
            }

            if (focusTarget == null)
            {
                for (int i = 0; i < stack.Children.Count; i++)
                {
                    var b = stack.Children[i] as Button;
                    if (b != null) { focusTarget = b; break; }
                }
            }

            var scroll = new ScrollViewer
            {
                Content = stack,
                MaxHeight = 360,
                CanContentScroll = false,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Focusable = false
            };

            var outerBorder = new Border
            {
                Background = Theme.Frozen(Theme.Surface),
                BorderBrush = Theme.Frozen(Theme.Stroke),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(2, 4, 2, 4),
                CornerRadius = new CornerRadius(0),
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 12,
                    ShadowDepth = 3,
                    Opacity = 0.55,
                    Direction = 270
                },
                Child = scroll
            };

            FocusManager.SetIsFocusScope(outerBorder, true);
            if (focusTarget != null)
            {
                FocusManager.SetFocusedElement(outerBorder, focusTarget);
            }
            KeyboardNavigation.SetTabNavigation(outerBorder, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(outerBorder, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetTabNavigation(stack, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetDirectionalNavigation(stack, KeyboardNavigationMode.Cycle);

            outerBorder.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CloseFlavorPopup();
                    if (anchor != null) anchor.Focus();
                    e.Handled = true;
                }
            };

            outerBorder.PreviewMouseWheel += (s, e) =>
            {
                SmoothWheel.Scroll(scroll, e.Delta, 36);
                e.Handled = true;
            };

            pop.Child = outerBorder;
            _system24FlavorPopup = pop;

            pop.Opened += (s, e) =>
            {
                UiAnim.Popup(outerBorder);
                pop.Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)delegate
                {
                    if (focusTarget != null)
                    {
                        focusTarget.Focus();
                        Keyboard.Focus(focusTarget);
                        focusTarget.BringIntoView();
                    }
                });
            };

            pop.IsOpen = true;
        }



        Border _sidebar;
        StackPanel _navPanel;
        readonly System.Collections.Generic.List<TextBlock> _navLabels = new System.Collections.Generic.List<TextBlock>();
        StackPanel _bottomBox;
        Button _collapseBtn;
        bool _collapsed;
        int _sidebarAnimationId;
        const double SideWide = 260, SideNarrow = 64;

        Border BuildSidebar()
        {
            var outer = new Grid { Background = Theme.BrBgBase };
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _navLabels.Clear();
            var nav = new StackPanel();
            _navPanel = nav;
            nav.Children.Add(NavItem("overview", Icons.Home, Loc.T("nav.overview")));
            nav.Children.Add(NavItem("strategies", Icons.Grid, Loc.T("nav.strategies")));
            nav.Children.Add(NavItem("check", Icons.Pulse, Loc.T("nav.check")));
            nav.Children.Add(NavItem("service", Icons.Server, Loc.T("nav.service")));
            nav.Children.Add(NavItem("filters", Icons.Filter, Loc.T("nav.filters")));
            nav.Children.Add(NavItem("settings", Icons.Gear, Loc.T("nav.settings")));
            nav.Children.Add(NavItem("updates", Icons.Download, Loc.T("nav.updates")));
            nav.Children.Add(NavItem("log", Icons.List, Loc.T("nav.log")));
            nav.Children.Add(NavItem("about", Icons.Info, Loc.T("nav.about")));
            KeyboardNavigation.SetDirectionalNavigation(nav, KeyboardNavigationMode.Contained);
            KeyboardNavigation.SetTabNavigation(nav, KeyboardNavigationMode.Continue);

            // Внутренние отступы подобраны под широкие шрифты (Unbounded):
            // подпись «Проверка соединения» должна влезать в сайдбар без обрезки.
            var navHost = new Grid { Margin = _collapsed ? new Thickness(4, 6, 4, 0) : new Thickness(8, 6, 8, 0) };
            _navHost = navHost;
            _navPill = new Border
            {
                Background = Theme.BrAccent,
                CornerRadius = Theme.R8,
                BorderThickness = new Thickness(1),
                BorderBrush = (Theme.Mode == ThemeMode.System24) ? Theme.BrAccent : Brushes.Transparent,
                Height = Theme.Dense(38),
                HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
                Width = _collapsed ? 44 : double.NaN,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            var navPillTx = new TranslateTransform(0, 0);
            _navPill.RenderTransform = navPillTx;
            _navPillTx = navPillTx;
            _navPillInitialized = false;

            navHost.Children.Add(_navPill);
            navHost.Children.Add(nav);

            var sv = new SmoothScrollViewer { Content = navHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(sv, 0); outer.Children.Add(sv);

            // низ: кнопка сворачивания + плеер + версия, github, обновление
            var bottomWrap = new StackPanel { Margin = new Thickness(10, 4, 10, 6) };

            _collapseBtn = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 4),
                HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Ctl.StripChrome(_collapseBtn);
            _collapseBd = new Border { CornerRadius = Theme.R10, Padding = new Thickness(12, 7, 12, 7),
                Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
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
            _collapseBtn.MouseEnter += (s, e) =>
            {
                cbBd.Background = Theme.BrSurface;
                if (Theme.Mode == ThemeMode.System24)
                {
                    if (Theme.AnimationsEnabled)
                    {
                        var bAnim = new ColorAnimation(Colors.Transparent, Theme.AccentMain, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        var brush = new SolidColorBrush(Colors.Transparent);
                        cbBd.BorderBrush = brush;
                        brush.BeginAnimation(SolidColorBrush.ColorProperty, bAnim);
                    }
                    else cbBd.BorderBrush = Theme.BrAccent;
                }
            };
            _collapseBtn.MouseLeave += (s, e) =>
            {
                cbBd.Background = Brushes.Transparent;
                if (Theme.Mode == ThemeMode.System24)
                {
                    if (Theme.AnimationsEnabled)
                    {
                        var bAnim = new ColorAnimation(Theme.AccentMain, Colors.Transparent, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        var brush = new SolidColorBrush(Theme.AccentMain);
                        cbBd.BorderBrush = brush;
                        brush.BeginAnimation(SolidColorBrush.ColorProperty, bAnim);
                    }
                    else cbBd.BorderBrush = Brushes.Transparent;
                }
                else cbBd.BorderBrush = Brushes.Transparent;
            };
            _collapseBtn.GotKeyboardFocus += (s, e) =>
            {
                cbBd.BorderBrush = Theme.BrAccentHi;
                cbBd.BorderThickness = new Thickness(2);
                cbBd.Background = Theme.BrSurface;
                _collapseLabel.Foreground = Theme.BrText;
                if (_collapseIcon.Stroke != null) _collapseIcon.Stroke = Theme.BrText;
            };
            _collapseBtn.LostKeyboardFocus += (s, e) =>
            {
                cbBd.BorderBrush = Brushes.Transparent;
                cbBd.BorderThickness = new Thickness(1);
                cbBd.Background = Brushes.Transparent;
                _collapseLabel.Foreground = Theme.BrMuted;
                if (_collapseIcon.Stroke != null) _collapseIcon.Stroke = Theme.BrMuted;
            };
            _collapseBtn.Click += (s, e) => ToggleCollapse();
            Ctl.AutomationSetName(_collapseBtn, Loc.T("mw.collapse"));
            bottomWrap.Children.Add(_collapseBtn);

            _bottomBox = new StackPanel { Margin = new Thickness(2, 0, 2, 0) };
            var line = new Border { Height = 1, Background = Theme.BrStroke, Margin = new Thickness(0, 0, 0, 4) };
            _bottomBox.Children.Add(line);
            _updateLine = new TextBlock { Foreground = Theme.BrFaint,
                FontSize = 10.5, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis };
            _bottomBox.Children.Add(_updateLine);
            _tgVerSidebar = new TextBlock { Foreground = Theme.BrFaint,
                FontSize = 10.5, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0) };
            _bottomBox.Children.Add(_tgVerSidebar);

            _appVerSidebar = new TextBlock { Foreground = Theme.BrFaint,
                FontSize = 10.5, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0) };
            _bottomBox.Children.Add(_appVerSidebar);
            UpdateZapretSidebarVersion();
            UpdateTgSidebarVersion();
            UpdateAppSidebarVersion();

            var gh = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            Ctl.StripChrome(gh);
            var ghsp = new StackPanel { Orientation = Orientation.Horizontal };
            ghsp.Children.Add(UI.Icon(Icons.Github, 15, Theme.BrMuted, 1.6));
            ghsp.Children.Add(new TextBlock { Text = "GitHub", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            gh.Content = ghsp;
            gh.Click += (s, e) => Core.OpenUrl(Core.AppRepo);
            Ctl.AutomationSetName(gh, Loc.T("mw.openGithub"));
            _bottomBox.Children.Add(gh);

            _bottomBoxContainer = new Border { Child = _bottomBox, ClipToBounds = _collapsed };
            bottomWrap.Children.Add(_bottomBoxContainer);

            Grid.SetRow(bottomWrap, 1); outer.Children.Add(bottomWrap);

            _sidebar = new Border
            {
                Child = outer,
                Background = Brushes.Transparent,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(0, 0, 1, 0),
                CornerRadius = new CornerRadius(0),
                Margin = new Thickness(0),
                Width = _collapsed ? SideNarrow : SideWide,
                ClipToBounds = true
            };
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

            // --- Немедленно отменяем все текущие анимации, чтобы при быстрых кликах
            // не накапливались обработчики Completed с конфликтующими состояниями ---
            _sidebar.BeginAnimation(FrameworkElement.WidthProperty, null);
            foreach (var lbl in _navLabels) lbl.BeginAnimation(OpacityProperty, null);
            if (_bottomBox != null) _bottomBox.BeginAnimation(OpacityProperty, null);
            if (_bottomBoxContainer != null)
            {
                _bottomBoxContainer.BeginAnimation(FrameworkElement.HeightProperty, null);
                if (!_collapsed)
                {
                    _bottomBoxContainer.Height = double.NaN;
                    _bottomBoxContainer.ClipToBounds = false;
                }
            }
            if (_bottomBox != null && !_collapsed) _bottomBox.Visibility = Visibility.Visible;

            if (!animate || !Theme.AnimationsEnabled)
            {
                _sidebar.Width = target;
                foreach (var lbl in _navLabels)
                {
                    lbl.Opacity = _collapsed ? 0 : 1;
                    lbl.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
                }
                foreach (var kv in _navButtons)
                {
                    var arr = (object[])kv.Value.Tag;
                    var bd = (Border)arr[0]; var sp = (StackPanel)arr[3];
                    var tx = sp.RenderTransform as TranslateTransform;
                    if (tx != null) { tx.BeginAnimation(TranslateTransform.XProperty, null); tx.X = 0; }
                    bd.Padding = _collapsed ? new Thickness(0, 8, 0, 8) : new Thickness(10, 8, 10, 8);
                    sp.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                    kv.Value.Width = _collapsed ? 44 : double.NaN;
                    kv.Value.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
                }
                if (_navPill != null)
                {
                    _navPill.Width = _collapsed ? 44 : double.NaN;
                    _navPill.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
                }
                if (_navHost != null) _navHost.Margin = _collapsed ? new Thickness(4, 6, 4, 0) : new Thickness(8, 6, 8, 0);
                if (_collapseSp != null) _collapseSp.HorizontalAlignment = _collapsed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
                if (_collapseBd != null) _collapseBd.Padding = _collapsed ? new Thickness(0, 7, 0, 7) : new Thickness(12, 7, 12, 7);

                if (_bottomBoxContainer != null)
                {
                    _bottomBoxContainer.Height = _collapsed ? 0 : double.NaN;
                    _bottomBoxContainer.ClipToBounds = _collapsed;
                }
                if (_bottomBox != null)
                {
                    _bottomBox.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
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
                    _bottomBoxContainer.ClipToBounds = true;
                    _bottomBox.Measure(new Size(SideWide, double.PositiveInfinity));
                    double curH = _bottomBoxContainer.ActualHeight > 0 ? _bottomBoxContainer.ActualHeight : (_bottomBox.DesiredSize.Height > 0 ? _bottomBox.DesiredSize.Height : 100);
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
                        var tx = sp.RenderTransform as TranslateTransform;
                        if (tx != null) { tx.BeginAnimation(TranslateTransform.XProperty, null); tx.X = 0; }
                        bd.Padding = Theme.Dense(new Thickness(0, 8, 0, 8));
                        sp.HorizontalAlignment = HorizontalAlignment.Center;
                        kv.Value.Width = 44;
                        kv.Value.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                    if (_navPill != null)
                    {
                        _navPill.Width = 44;
                        _navPill.HorizontalAlignment = HorizontalAlignment.Center;
                    }
                    if (_navHost != null) _navHost.Margin = new Thickness(4, 6, 4, 0);
                    if (_collapseSp != null) _collapseSp.HorizontalAlignment = HorizontalAlignment.Center;
                    if (_collapseBd != null) _collapseBd.Padding = new Thickness(0, 7, 0, 7);
                    // В сложенном виде нижний блок полностью снят с раскладки:
                    // иначе его тексты остаются Visible и аудит видит их обрезанными.
                    if (_bottomBox != null) _bottomBox.Visibility = Visibility.Collapsed;
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
                    bd.Padding = Theme.Dense(new Thickness(10, 8, 10, 8));
                    sp.HorizontalAlignment = HorizontalAlignment.Left;
                    kv.Value.Width = double.NaN;
                    kv.Value.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                if (_navPill != null)
                {
                    _navPill.Width = double.NaN;
                    _navPill.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                if (_navHost != null) _navHost.Margin = new Thickness(8, 6, 8, 0);
                if (_collapseSp != null) _collapseSp.HorizontalAlignment = HorizontalAlignment.Left;
                if (_collapseBd != null) _collapseBd.Padding = new Thickness(12, 7, 12, 7);

                if (_bottomBoxContainer != null)
                {
                    if (_bottomBox != null) _bottomBox.Visibility = Visibility.Visible;
                    _bottomBox.Measure(new Size(SideWide, double.PositiveInfinity));
                    double targetH = _bottomBox.DesiredSize.Height > 0 ? _bottomBox.DesiredSize.Height : 105;
                    double curH = _bottomBoxContainer.ActualHeight;
                    var hAnim = new DoubleAnimation(curH, targetH, TimeSpan.FromMilliseconds(200))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
                    hAnim.Completed += (s, e) =>
                    {
                        if (animationId == _sidebarAnimationId && !_collapsed)
                        {
                            _bottomBoxContainer.Height = double.NaN;
                            _bottomBoxContainer.ClipToBounds = false;
                        }
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

        void UpdateZapretSidebarVersion()
        {
            if (_updateLine == null) return;
            string local = SettingsPage.NormVer(Core.ZapretVersion());
            bool hasLatest = !string.IsNullOrEmpty(_lastZapretLatest);
            bool hasUpdate = hasLatest && SettingsPage.CompareVersions(_lastZapretLatest, local) > 0;
            string key = hasUpdate ? "mw.verUpdate" : (hasLatest ? "mw.verCurrent" : "mw.verLine");
            _updateLine.Text = string.Format(Loc.T(key), local);
            _updateLine.Foreground = hasUpdate ? Theme.BrWarn : Theme.BrFaint;
        }

        void UpdateTgSidebarVersion()
        {
            if (_tgVerSidebar == null) return;
            if (!Core.TgProxyInstalled())
            {
                _tgVerSidebar.Text = Loc.T("mw.tgVer.none");
                _tgVerSidebar.Foreground = Theme.BrFaint;
                return;
            }
            string tgLocal = Core.TgProxyLocalVersion();
            string tgLv = string.IsNullOrEmpty(tgLocal) ? "?" : SettingsPage.NormVer(tgLocal);
            bool hasLatest = !string.IsNullOrEmpty(_lastTgLatest);
            bool hasUpdate = hasLatest && !string.IsNullOrEmpty(tgLocal) && SettingsPage.CompareVersions(_lastTgLatest, tgLocal) > 0;
            string key = hasUpdate ? "mw.tgVer.update" : (hasLatest ? "mw.tgVer" : "mw.tgVerPlain");
            _tgVerSidebar.Text = string.Format(Loc.T(key), tgLv);
            _tgVerSidebar.Foreground = hasUpdate ? Theme.BrWarn : Theme.BrFaint;
        }

        void UpdateAppSidebarVersion()
        {
            if (_appVerSidebar == null) return;
            string appNorm = SettingsPage.NormVer(Core.AppVersion);
            bool hasLatest = !string.IsNullOrEmpty(_lastAppLatest);
            bool hasUpdate = hasLatest && SettingsPage.CompareVersions(_lastAppLatest, Core.AppVersion) > 0;
            string key = hasUpdate ? "mw.appShell.update" : (hasLatest ? "mw.appShell" : "mw.appShellPlain");
            _appVerSidebar.Text = string.Format(Loc.T(key), appNorm);
            _appVerSidebar.Foreground = hasUpdate ? Theme.BrWarn : Theme.BrFaint;
        }

        public void RefreshSidebarVersions()
        {
            UpdateZapretSidebarVersion();
            UpdateTgSidebarVersion();
            UpdateAppSidebarVersion();
            Page updatesPage;
            if (_pages.TryGetValue("updates", out updatesPage))
            {
                var updates = updatesPage as UpdatesPage;
                if (updates != null && _haveUpdateResults)
                    updates.SetAutomaticUpdateResults(_lastZapretLatest, _lastZapretLocal,
                        _lastTgLatest, _lastTgLocal, _lastAppLatest, Core.AppVersion);
            }
        }

        // Вызывается настройками после успешного обновления Telegram-прокси.
        // Кеш результатов проверки хранит локальную версию, снятую до установки;
        // без его обновления повторный вход в настройки возвращал кнопку
        // «Обновить», хотя новая сборка уже стояла.
        public void NoteTgProxyUpdated(string local)
        {
            if (!string.IsNullOrEmpty(local)) _lastTgLocal = local;
            RefreshSidebarVersions();
        }

        Button NavItem(string key, string icon, string label)
        {
            var b = new Button { Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 3), HorizontalContentAlignment = HorizontalAlignment.Stretch };
            Ctl.StripChrome(b);
            var bd = new Border { CornerRadius = Theme.R8, Padding = Theme.Dense(new Thickness(10, 8, 10, 8)),
                Background = Brushes.Transparent, BorderThickness = new Thickness(1), BorderBrush = Brushes.Transparent };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var navTx = new TranslateTransform(0, 0);
            sp.RenderTransform = navTx;
            var ic = UI.Icon(icon, 18, Theme.BrMuted, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
            var animType = key == "settings" ? IconAnimType.Rotate90 :
                           key == "strategies" ? IconAnimType.Wiggle :
                           key == "check" ? IconAnimType.Pulse :
                           IconAnimType.ScaleBounce;
            UI.AttachIconHoverAnimation(b, ic, animType);
            sp.Children.Add(ic);
            var tb = new TextBlock { Text = label, Foreground = Theme.BrMuted, FontSize = Theme.FsBody, FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible };
            _navLabels.Add(tb);
            sp.Children.Add(tb);
            bd.Child = sp;
            b.Content = bd;
            b.Tag = new object[] { bd, ic, tb, sp };
            b.ToolTip = label;
            b.MouseEnter += (s, e) =>
            {
                if (Theme.Mode == ThemeMode.System24 && !_collapsed)
                {
                    if (Theme.AnimationsEnabled)
                    {
                        var slide = new DoubleAnimation(6, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        navTx.BeginAnimation(TranslateTransform.XProperty, slide);
                    }
                    else
                    {
                        navTx.BeginAnimation(TranslateTransform.XProperty, null);
                        navTx.X = 6;
                    }
                }
                if (_current != key)
                {
                    bd.Background = Theme.BrSurface;
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
                    if (ic.Stroke != null) ic.Stroke = Theme.BrText;
                    if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = Theme.BrText;
                    tb.Foreground = Theme.BrText;
                }
            };
            b.MouseLeave += (s, e) =>
            {
                if (Theme.Mode == ThemeMode.System24)
                {
                    if (Theme.AnimationsEnabled)
                    {
                        var slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        navTx.BeginAnimation(TranslateTransform.XProperty, slide);
                    }
                    else
                    {
                        navTx.BeginAnimation(TranslateTransform.XProperty, null);
                        navTx.X = 0;
                    }
                }
                else if (navTx.X != 0)
                {
                    navTx.BeginAnimation(TranslateTransform.XProperty, null);
                    navTx.X = 0;
                }

                if (_current != key)
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
                    if (ic.Stroke != null) ic.Stroke = Theme.BrMuted;
                    if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = Theme.BrMuted;
                    tb.Foreground = Theme.BrMuted;
                }
            };
            b.GotKeyboardFocus += (s, e) =>
            {
                bd.BorderBrush = Theme.BrAccentHi;
                bd.BorderThickness = new Thickness(2);
                if (_current != key) bd.Background = Theme.BrSurface;
                tb.Foreground = Theme.BrText;
                if (ic.Stroke != null) ic.Stroke = Theme.BrText;
                b.BringIntoView();
            };
            b.LostKeyboardFocus += (s, e) =>
            {
                bd.BorderThickness = new Thickness(1);
                if (_current != key)
                {
                    bd.BorderBrush = Brushes.Transparent;
                    bd.Background = Brushes.Transparent;
                    tb.Foreground = Theme.BrMuted;
                    if (ic.Stroke != null) ic.Stroke = Theme.BrMuted;
                }
                else
                {
                    bd.BorderBrush = (Theme.Mode == ThemeMode.System24) ? Theme.BrAccent : Brushes.Transparent;
                }
            };
            b.Click += (s, e) => Navigate(key);
            Ctl.AutomationSetName(b, label);
            _navButtons[key] = b;
            return b;
        }

        string _activeNavKey;
        Border _navPill;
        TranslateTransform _navPillTx;
        Grid _navHost;
        bool _navPillInitialized;
        int _navPillGen;

        void PaintNav()
        {
            int pillGen = ++_navPillGen;
            Button targetBtn = null;
            if (!string.IsNullOrEmpty(_current) && _navButtons.ContainsKey(_current))
                targetBtn = _navButtons[_current];

            // Единый цикл по всем пунктам — обновляем цвета текста и иконки
            foreach (var kv in _navButtons)
            {
                var arr = (object[])kv.Value.Tag;
                var bd = (Border)arr[0];
                var ic = (System.Windows.Shapes.Path)arr[1];
                var tb = (TextBlock)arr[2];
                bool on = kv.Key == _current;
                bd.Background = Brushes.Transparent;
                bd.BorderThickness = new Thickness(1);
                bd.BorderBrush = (on && Theme.Mode == ThemeMode.System24) ? Theme.BrAccent : Brushes.Transparent;
                Brush fg = on ? Theme.BrOnAccent : Theme.BrMuted;
                if (ic.Stroke != null) ic.Stroke = fg;
                if (ic.Fill != null && ic.Fill != Brushes.Transparent) ic.Fill = fg;
                tb.Foreground = fg;
                bd.InvalidateVisual();
            }

            if (_navPill != null && targetBtn != null && _navPillTx != null && _navPanel != null)
            {
                Action updatePill = delegate
                {
                    // Пока ждали раскладку, пользователь мог уйти на другой раздел:
                    // устаревший колбэк не должен дёргать пилюлю назад.
                    if (pillGen != _navPillGen) return;
                    try
                    {
                        Point pt = targetBtn.TranslatePoint(new Point(0, 0), _navPanel);
                        double targetY = pt.Y;
                        double targetH = targetBtn.ActualHeight;
                        if (targetH <= 0) targetH = _navPill.ActualHeight > 0 ? _navPill.ActualHeight : 44;
                        _navPill.Height = targetH;

                        if (!_navPillInitialized || !Theme.AnimationsEnabled)
                        {
                            _navPillTx.BeginAnimation(TranslateTransform.YProperty, null);
                            _navPillTx.Y = targetY;
                            _navPillInitialized = true;
                        }
                        else
                        {
                            // Стартуем от ТЕКУЩЕГО положения, а не от базового Y:
                            // иначе при быстром переключении пилюля сначала прыгает
                            // к предыдущему пункту и на промежуточном кадре обрезается.
                            double fromY = _navPillTx.Y;
                            var anim = new DoubleAnimation(fromY, targetY, TimeSpan.FromMilliseconds(220))
                            {
                                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                            };
                            _navPillTx.BeginAnimation(TranslateTransform.YProperty, anim);
                        }
                    }
                    catch { }
                };

                if (targetBtn.IsLoaded && targetBtn.ActualHeight > 0) updatePill();
                else Dispatcher.BeginInvoke(DispatcherPriority.Loaded, updatePill);
            }
            _activeNavKey = _current;
            Title = Core.AppName + "  ·  " + Loc.T("nav." + _current);
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
                case "updates": p = new UpdatesPage(this); break;
                case "log": p = new LogPage(this); break;
                case "about": p = new AboutPage(this); break;
                default: p = new OverviewPage(this); break;
            }
            _pages[key] = p;
            return p;
        }

        public void Navigate(string key, bool isUiSwitch = false)
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
            var updates = p as UpdatesPage;
            if (updates != null && _haveUpdateResults)
            {
                string zapLocal = _lastZapretLocal ?? Core.ZapretVersion();
                string tgLocal = _lastTgLocal ?? (Core.TgProxyInstalled() ? Core.TgProxyLocalVersion() : null);
                updates.SetAutomaticUpdateResults(_lastZapretLatest, zapLocal,
                    _lastTgLatest, tgLocal, _lastAppLatest, Core.AppVersion);
            }
            PaintNav();
            if (_navPanel != null) _navPanel.UpdateLayout();
            // Появление экрана (Fade + Slide). UiAnim сам раскладывает страницу до
            // старта часов, иначе тяжёлая вёрстка съедает первые кадры и переход
            // выглядит мгновенным.
            if (!isUiSwitch) UiAnim.Appear(p, 10, 220, 260);
            else UiAnim.Reset(p);
            if (!isUiSwitch)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, (Action)delegate
                {
                    GC.Collect(1, GCCollectionMode.Optimized);
                });
            }
        }

        // ---------- Публичный API для страниц ----------
        public void OpenTgProxySettings()
        {
            Navigate("settings");
            var s = PageFor("settings") as SettingsPage;
            if (s != null) s.RevealTgSection();
        }

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
            if (!IsActive2()) return "—";
            DateTime? started = Core.StartedAt;
            if (!started.HasValue)
            {
                if (Core.IsWinwsRunning()) started = Core.WinwsProcessStartTime();
            }
            if (!started.HasValue)
            {
                if (Core.ServiceState() == "running") return Loc.T("ov.up.unknown");
                return "—";
            }
            var d = DateTime.UtcNow - started.Value.ToUniversalTime();
            if (d.TotalSeconds < 0) d = TimeSpan.Zero;
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

        int _strategyRequest;

        // Выбрать стратегию без запуска. Если обход уже активен — перезапустить с новой.
        public void SelectStrategy(string file)
        {
            System.Threading.Interlocked.Increment(ref _strategyRequest);
            _currentStrategyFile = file;
#if SELFTEST
            // Автотест кликает по карточкам стратегий ради проверки вёрстки: писать
            // рабочую last_strategy и перезапускать обход (стоп службы до 20 с) ему
            // нельзя. Обновление шапки оставляем — иначе пропадает покрытие раскладки.
            RefreshTop();
#else
            Core.Set("last_strategy", file); Core.SaveConfig();
            if (Core.IsWinwsRunning()) RunStrategy(file);
            else Core.Info(string.Format(Loc.T("mw.stratSelected"), Core.PrettyName(file)));
            RefreshTop();
#endif
        }

        // Выполнить действие в UI-потоке. Вызывать можно из любого потока: фоновые
        // операции (sc stop, taskkill, ожидание winws) занимают до ~25 с, поэтому
        // сами они идут в пуле, а окно обновляется через этот помощник.
        void UiPost(Action a)
        {
            if (a == null) return;
            try
            {
                if (Dispatcher.CheckAccess()) a();
                else Dispatcher.BeginInvoke(DispatcherPriority.Normal, a);
            }
            catch { }
        }

        public void RunStrategy(string file)
        {
            if (!Core.IsAdmin()) { UiPost(delegate { NeedAdmin(Loc.T("mw.startBypassAct")); }); return; }
            int req = System.Threading.Interlocked.Increment(ref _strategyRequest);
            UiPost(delegate { if (_overview != null) _overview.SetZapretTransition(true); });
            // Раньше запуск выполнялся через Dispatcher.BeginInvoke, то есть в
            // UI-потоке: остановка службы (до 20 с) и ожидание завершения winws
            // полностью замораживали окно. Теперь работа идёт в пуле потоков,
            // а сам метод можно вызывать из любого потока.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { RunStrategyCore(file, req); });
        }

        // Возвращает признак успеха: он нужен полному перезапуску (RestartAll),
        // чтобы отличить «перезапущено» от «перезапуск с ошибками».
        bool RunStrategyCore(string file)
        {
            return RunStrategyCore(file, _strategyRequest);
        }

        bool RunStrategyCore(string file, int req)
        {
            if (!Core.TryBeginWinwsOperation())
            {
                Warn(Loc.T("mw.busy"));
                if (req == _strategyRequest)
                {
                    UiPost(delegate { if (_overview != null) _overview.SetZapretTransition(false); });
                }
                return false;
            }
            bool ok = false;
            try
            {
                Core.Info(string.Format(Loc.T("mw.startingLog"), Core.PrettyName(file)));
                if (Core.ServiceState() == "running")
                {
                    if (!Core.StopService()) throw new Exception("Could not stop Windows service");
                    // «sc stop» возвращает управление на STOP_PENDING: без ожидания
                    // следующий шаг (KillWinws / замена файлов) работал с ещё живым
                    // winws.exe и отменял обновление как «файлы заняты».
                    Core.WaitServiceStopped(8000);
                }
                Core.KillWinws();
                if (!Core.StartWinws(file)) throw new Exception("winws.exe was not started");
                if (req == _strategyRequest)
                {
                    _currentStrategyFile = file;
                    Core.Set("last_strategy", file); Core.SaveConfig();
                }
                ok = true;
                Core.Good(string.Format(Loc.T("mw.startedToast"), Core.PrettyName(file)));
                UiPost(delegate
                {
                    Notify(Loc.T("mw.startedTitle"), Core.PrettyName(file));
                    ShowToast(string.Format(Loc.T("mw.startedToast"), Core.PrettyName(file)), Sev.Ok);
                });
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.startErr"), ex.Message)); }
            finally
            {
                Core.EndWinwsOperation();
                UiPost(delegate
                {
                    if (req == _strategyRequest)
                    {
                        if (_overview != null) _overview.SetZapretTransition(false);
                    }
                    RefreshTop();
                });
            }
            return ok;
        }

        // Остановка из UI: не блокирует окно.
        public void StopAll()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { StopAllCore(); });
        }

        // Синхронная остановка для фоновых сценариев (например, обновление zapret,
        // где распаковка обязана идти уже после освобождения файлов winws).
        public bool StopAllCore()
        {
            if (!Core.TryBeginWinwsOperation()) { Warn(Loc.T("mw.busy")); return false; }
            bool ok = false;
            try
            {
                if (Core.ServiceState() == "running")
                {
                    if (!Core.StopService()) throw new Exception("Could not stop Windows service");
                    // «sc stop» возвращает управление на STOP_PENDING: без ожидания
                    // следующий шаг (KillWinws / замена файлов) работал с ещё живым
                    // winws.exe и отменял обновление как «файлы заняты».
                    Core.WaitServiceStopped(8000);
                }
                if (!Core.KillWinws()) throw new Exception("Could not stop winws.exe");
                ok = true;
                Core.Info(Loc.T("mw.stopped"));
                UiPost(delegate
                {
                    Notify(Loc.T("mw.stoppedTitle"), Loc.T("mw.stoppedBody"));
                    ShowToast(Loc.T("mw.stopped"), Sev.Warn);
                });
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.stopErr"), ex.Message)); }
            finally
            {
                Core.EndWinwsOperation();
                UiPost(delegate { RefreshTop(); });
            }
            return ok;
        }

        public void RestartCurrent()
        {
            if (string.IsNullOrEmpty(_currentStrategyFile))
            {
                var files = Core.GetStrategyFiles();
                if (files.Count > 0) _currentStrategyFile = files[0];
            }
            if (string.IsNullOrEmpty(_currentStrategyFile)) { Warn(Loc.T("mw.noStratSel")); return; }
            Core.Info(Loc.T("mw.restarting"));
            UiPost(delegate { ShowToast(Loc.T("mw.restarting"), Sev.Info); });
            // Отдельный StopAll не нужен: RunStrategyCore сам останавливает службу
            // и снимает winws. Прежняя пара StopAll()+RunStrategy() при переходе на
            // асинхронный запуск дала бы две конкурирующие операции и «занято».
            RunStrategy(_currentStrategyFile);
        }

        // ---------- Полный перезапуск (кнопка в шапке) ----------
        volatile bool _restartingAll;
        Button _topRestartBtn;
        System.Windows.Shapes.Path _topRestartIcon;

        // Раньше кнопка в шапке звала RestartCurrent(): она поднимала только winws
        // и всегда в ручном режиме, то есть режим «служба» молча терялся, а
        // Telegram-прокси и версии оставались как были. Теперь перезапускается всё
        // работающее: обход в своём режиме (служба или ручной запуск) и прокси,
        // после чего заново запрашиваются версии zapret, прокси и приложения.
        // Тема, язык и открытая страница не меняются — окно не пересобирается.
        public void RestartAll()
        {
            if (_restartingAll || Core.WinwsOperationActive()) { Warn(Loc.T("mw.busy")); ShowToast(Loc.T("mw.busy"), Sev.Warn); return; }

            if (string.IsNullOrEmpty(_currentStrategyFile))
            {
                var files = Core.GetStrategyFiles();
                if (files.Count > 0) _currentStrategyFile = files[0];
            }
            string strategy = _currentStrategyFile;
            bool asService = Core.ServiceState() == "running";
            bool asManual = !asService && Core.IsWinwsRunning();
            bool doBypass = asService || asManual || !string.IsNullOrEmpty(strategy);
            // Прокси поднимаем заново только если он работал: запускать то, что
            // пользователь сам выключил, кнопка перезапуска не должна.
            bool doTg = Core.TgProxyInstalled() && Core.TgProxyRunning();

            if (doBypass && !Core.IsAdmin()) { NeedAdmin(Loc.T("mw.startBypassAct")); return; }
            if (!doBypass) Core.Warn(Loc.T("mw.restartNoBypass"));
            if (!doTg) Core.Info(Loc.T("mw.restartTgSkip"));

            _restartingAll = true;
            SetTopRestartBusy(true);
            Core.Info(Loc.T("mw.restartAll"));
            ShowToast(Loc.T("mw.restartAll"), Sev.Info);
            if (_overview != null)
            {
                if (doBypass) _overview.SetZapretTransition(true);
                if (doTg) _overview.SetTgTransition(true);
            }
            SetSettingsChecking();

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool bypassOk = !doBypass;
                bool tgOk = !doTg;
                try
                {
                    if (doBypass) bypassOk = asService ? RestartServiceCore() : RunStrategyCore(strategy);
                    if (doTg) tgOk = RestartTgProxyCore();
                }
                catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.restartAllErr"), ex.Message)); }
                finally
                {
                    UiPost(delegate
                    {
                        _restartingAll = false;
                        SetTopRestartBusy(false);
                        if (_overview != null)
                        {
                            _overview.SetZapretTransition(false);
                            _overview.SetTgTransition(false);
                            _overview.Refresh();
                        }
                        RefreshTop();
                        RefreshSidebarVersions();
                        ShowToast(bypassOk && tgOk ? Loc.T("mw.restartAllDone") : Loc.T("mw.restartAllPart"),
                            bypassOk && tgOk ? Sev.Ok : Sev.Warn);
                        // Версии перепроверяем в конце: карточки настроек покажут
                        // строку и кнопку, только если обновление действительно есть.
                        CheckUpdates();
                    });
                }
            });
        }

        // Режим «служба»: RunStrategyCore здесь не подходит — он поднимает winws
        // вручную и тем самым переводит обход из службы в ручной запуск.
        bool RestartServiceCore()
        {
            if (!Core.TryBeginWinwsOperation()) { Warn(Loc.T("mw.busy")); return false; }
            try
            {
                Core.Info(Loc.T("mw.restartSvc"));
                if (!Core.StopService()) throw new Exception("Could not stop Windows service");
                Core.WaitServiceStopped(8000);
                Core.KillWinws();
                if (!Core.ServiceExists()) throw new Exception("Windows service is not installed");
                if (!Core.StartService()) throw new Exception("Could not start Windows service");
                Core.Good(Loc.T("mw.restartSvcOk"));
                return true;
            }
            catch (Exception ex) { Core.Fail(string.Format(Loc.T("mw.startErr"), ex.Message)); return false; }
            finally { Core.EndWinwsOperation(); }
        }

        public void RestartInSameMode(bool wasService, bool wasManual, string prevStrat)
        {
            try
            {
                if (wasService)
                {
                    Core.StartService();
                }
                else if (wasManual && !string.IsNullOrEmpty(prevStrat))
                {
                    RunStrategy(prevStrat);
                }
            }
            catch (Exception ex)
            {
                Core.Warn("RestartInSameMode: " + Core.Short(ex.Message));
            }
        }

        public void RestartInSameMode(string prevStrat)
        {
            bool asService = Core.ServiceState() == "running";
            bool asManual = !asService && Core.IsWinwsRunning();
            RestartInSameMode(asService, asManual, prevStrat);
        }

        bool RestartTgProxyCore()
        {
            Core.TgProxyStop();
            // Порт освобождается не мгновенно: без паузы повторный старт иногда
            // падает на «address already in use».
            System.Threading.Thread.Sleep(300);
            string err;
            if (Core.TgProxyStart(out err)) { Core.Good(Loc.T("tg.startedOk")); return true; }
            Core.Fail(string.Format(Loc.T("tg.startErr"), err));
            return false;
        }

        void SetSettingsChecking()
        {
            Page updatesPage;
            if (_pages.TryGetValue("updates", out updatesPage))
            {
                var updates = updatesPage as UpdatesPage;
                if (updates != null) updates.SetCheckingUpdates();
            }
        }

        // Пока идёт перезапуск, кнопка недоступна и непрерывно крутится.
        void SetTopRestartBusy(bool busy)
        {
            if (_topRestartBtn == null) return;
            _topRestartBtn.IsEnabled = !busy;
            _topRestartBtn.Opacity = busy ? 0.7 : 1.0;
            _topRestartBtn.ToolTip = Loc.T(busy ? "mw.restartAllBusy" : "mw.restartAllTip");
            if (_topRestartIcon == null) return;
            RotateTransform rt = null;
            var tg = _topRestartIcon.RenderTransform as TransformGroup;
            if (tg != null)
                foreach (Transform t in tg.Children) { rt = t as RotateTransform; if (rt != null) break; }
            if (rt == null) return;
            if (busy && Theme.AnimationsEnabled)
            {
                var spin = new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900));
                spin.RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever;
                rt.BeginAnimation(RotateTransform.AngleProperty, spin);
            }
            else rt.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        void NeedAdmin(string what)
        {
            Core.Warn(string.Format(Loc.T("mw.needAdminLog"), what));
            var res = MessageBox.Show(string.Format(Loc.T("mw.needAdminPrompt"), what), Loc.T("service.noAdmin.title"),
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res == MessageBoxResult.Yes)
            {
                Core.RestartAsAdmin();
            }
        }

        public void Warn(string t) { Core.Warn(t); ShowToast(t, Sev.Warn); }

        public void RefreshTop()
        {
            bool bypass = IsActive2();
            bool tg = Core.TgProxyRunning();
            Border np;
            if (bypass && tg) np = Pill.Make(Theme.AccentMain, Loc.T("state.bypassTg"));
            else if (bypass) np = Pill.Make(Theme.AccentMain, Loc.T("state.bypass"));
            else if (tg) np = Pill.Make(Theme.AccentMain, Loc.T("state.tg"));
            else np = Pill.Make(Sev.Neutral, Loc.T("state.stopped"));
            np.ToolTip = string.Format(Loc.T("state.tooltip"),
                bypass ? Loc.T("state.running") : Loc.T("state.stopped"),
                tg ? Loc.T("state.running") : Loc.T("state.stopped"));
            ReplaceTopPill(np);
            if (_topAdminPill != null)
                _topAdminPill.Visibility = Core.IsAdmin() ? Visibility.Collapsed : Visibility.Visible;
            UpdateTray(bypass, tg);
        }

        void ReplaceTopPill(Border np)
        {
            np.VerticalAlignment = VerticalAlignment.Center;
            np.Margin = new Thickness(16, 0, 0, 0);
            if (_topStatusPill == null) return;
            var g = _topStatusPill.Parent as Grid;
            if (g == null) return;
            int idx = g.Children.IndexOf(_topStatusPill);
            if (idx < 0) return;
            Grid.SetColumn(np, 1);
            g.Children.RemoveAt(idx);
            g.Children.Insert(idx, np);
            _topStatusPill = np;
            ConstrainTopStatus();
        }

        // Длинная плашка «Обход и TG-прокси запущены» на 820 px с Unbounded
        // вылезала на кнопки шапки. Ограничиваем текст и даём многоточие.
        void ConstrainTopStatus()
        {
            if (_topStatusPill == null) return;
            double max = Math.Max(170, ActualWidth - 560);
            _topStatusPill.MaxWidth = max;
            var sp = _topStatusPill.Child as StackPanel;
            if (sp == null) return;
            foreach (var ch in sp.Children)
            {
                var tb = ch as TextBlock;
                if (tb == null) continue;
                tb.TextTrimming = TextTrimming.CharacterEllipsis;
                tb.MaxWidth = Math.Max(80, max - 50);
            }
        }

#if SELFTEST
        public void ShowDemoTopStatus(string locKey, bool accent)
        {
            var np = accent ? Pill.Make(Theme.AccentMain, Loc.T(locKey)) : Pill.Make(Sev.Ok, Loc.T(locKey));
            ReplaceTopPill(np);
        }
#endif

        // ---------- Обновления ----------
        volatile bool _updating;
        volatile bool _checkingUpdates;
        bool _haveUpdateResults;
        string _lastZapretLatest, _lastZapretLocal, _lastTgLatest, _lastTgLocal, _lastAppLatest;

        void SetZapretUpdateProgress(string phase, int percent, long bytesRead = -1, long totalBytes = -1, double speedBps = 0)
        {
            Page updatesPage;
            if (_pages.TryGetValue("updates", out updatesPage))
            {
                var updates = updatesPage as UpdatesPage;
                if (updates != null) updates.SetZapretUpdateProgress(phase, percent, bytesRead, totalBytes, speedBps);
            }
        }

        void FinishZapretUpdate(string text, bool ok)
        {
            Page updatesPage;
            if (_pages.TryGetValue("updates", out updatesPage))
            {
                var updates = updatesPage as UpdatesPage;
                if (updates != null) updates.FinishZapretUpdate(text, ok);
            }
        }

        public void CheckUpdates()
        {
            if (_checkingUpdates) return;
            _checkingUpdates = true;
            SetSettingsChecking();
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
                        if (latest != null) { _lastZapretLatest = latest; Core.Set("latest_zapret", latest); }
                        _lastZapretLocal = local;
                        if (tgLatest != null) { _lastTgLatest = tgLatest; Core.Set("latest_tg", tgLatest); }
                        _lastTgLocal = tgLocal;
                        if (appLatest != null) { _lastAppLatest = appLatest; Core.Set("latest_app", appLatest); }
                        _haveUpdateResults = true;
                        if (latest != null || tgLatest != null || appLatest != null) Core.SaveConfig();
                        string zapLatestNorm = SettingsPage.NormVer(latest);
                        string zapLocalNorm = SettingsPage.NormVer(local);

                        // --- zapret ---
                        if (latest == null) { Core.Warn(Loc.T("mw.verFail")); }
                        else if (SettingsPage.CompareVersions(latest, local) <= 0)
                        {
                            Core.Good(string.Format(Loc.T("mw.zapVerOk"), zapLocalNorm));
                        }
                        else
                        {
                            Core.Warn(string.Format(Loc.T("mw.zapVerNew"), zapLatestNorm, zapLocalNorm));
                        }

                        // --- TG proxy ---
                        if (Core.TgProxyInstalled() && !string.IsNullOrEmpty(tgLocal))
                        {
                            string tgLv = SettingsPage.NormVer(tgLocal);
                            string tgLt = SettingsPage.NormVer(tgLatest);
                            if (tgLatest != null && SettingsPage.CompareVersions(tgLatest, tgLocal) > 0)
                            {
                                Core.Warn(string.Format(Loc.T("mw.tgVerNew"), tgLt, tgLv));
                            }
                            else if (tgLatest != null)
                            {
                                Core.Good(string.Format(Loc.T("mw.tgVerOk"), tgLv));
                            }
                        }

                        // --- app ---
                        string appLv = SettingsPage.NormVer(Core.AppVersion);
                        string appLt = SettingsPage.NormVer(appLatest);
                        if (appLatest != null && SettingsPage.CompareVersions(appLatest, Core.AppVersion) > 0)
                            Core.Warn(string.Format(Loc.T("mw.appVerNew"), appLt, appLv));
                        else if (appLatest != null)
                            Core.Good(string.Format(Loc.T("mw.appVerOk"), appLv));

                        UpdateZapretSidebarVersion();
                        UpdateTgSidebarVersion();
                        UpdateAppSidebarVersion();

                        Page updatesPage;
                        if (_pages.TryGetValue("updates", out updatesPage))
                        {
                            var updates = updatesPage as UpdatesPage;
                            if (updates != null)
                                updates.SetAutomaticUpdateResults(latest, local, tgLatest, tgLocal, appLatest, Core.AppVersion);
                        }
                    });
                }
                catch
                {
                    try
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            Page updatesPage;
                            if (_pages.TryGetValue("updates", out updatesPage))
                            {
                                var updates = updatesPage as UpdatesPage;
                                if (updates != null) updates.StopSpinAnimation();
                            }
                        });
                    }
                    catch { }
                }
                finally { _checkingUpdates = false; }
            });
        }

        // Архив + распаковка в %TEMP% + замена файлов в корне: запас берём с
        // избытком, потому что реальный размер архива до скачивания неизвестен.
        const long UpdateFreeSpaceNeeded = 300L * 1024 * 1024;

        void DoUpdate(string ver)
        {
            if (_updating) return;
            _updating = true;
            string tmpDir = null, zip = null, root = null, restartStrategy = null;
            bool restartService = false, restartManual = false;
            // Подготовка (ServiceState, IsWinwsRunning, CreateDirectory в %TEMP%,
            // доступ к _updateLine) шла ДО try: исключение здесь оставляло
            // _updating == true навсегда, и кнопка «Обновить» не отвечала до
            // перезапуска приложения.
            try
            {
                restartService = Core.ServiceState() == "running";
                restartManual = !restartService && Core.IsWinwsRunning();
                restartStrategy = _currentStrategyFile;
                tmpDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lantern_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(tmpDir);
                zip = System.IO.Path.Combine(tmpDir, "zapret_update.zip");
                root = Core.Root;
                Core.Info(string.Format(Loc.T("mw.updStart"), ver));
                _updateLine.Text = Loc.T("mw.updProgress");
                SetZapretUpdateProgress(Loc.T("settings.update.downloading"), 0);
            }
            catch (Exception exPrep)
            {
                _updating = false;
                Core.DeleteDirSafe(tmpDir);
                string msg = Loc.T("mw.updFail") + ": " + Core.Short(exPrep.Message);
                try
                {
                    _updateLine.Text = Loc.T("mw.updFail");
                    FinishZapretUpdate(msg, false);
                }
                catch { }
                Core.Fail(string.Format(Loc.T("mw.updErr"), Core.Short(exPrep.Message)));
                return;
            }

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // Остановка обязана завершиться до распаковки, иначе winws
                    // держит файлы в bin. Ждём здесь, в фоновом потоке: асинхронный
                    // StopAll() не давал такой гарантии, а синхронный вызов в
                    // UI-потоке замораживал окно на время «sc stop».
                    // Результат больше не отбрасывается: при незавершённой
                    // остановке пофайловая замена оставляла установку из двух версий.
                    if ((restartService || restartManual) && !StopAllCore())
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = Loc.T("mw.updStopFail");
                            FinishZapretUpdate(Loc.T("mw.updStopFail"), false);
                            Core.Fail(Loc.T("mw.updStopFail"));
                        });
                        return;
                    }
                    // winws остановлен, но служба WinDivert ещё держит
                    // bin/WinDivert64.sys — без её остановки замена падает
                    // на проверке блокировок уже после скачивания архива.
                    string wdErr;
                    if (!Core.PrepareWinDivertForUpdate(out wdErr))
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = Loc.T("mw.updWdFail");
                            FinishZapretUpdate(wdErr, false);
                            Core.Fail(wdErr);
                        });
                        return;
                    }
                    // Архив и распаковка идут в %TEMP%, замена файлов идёт в корень
                    // zapret. Без проверки места оба этапа обрывались на середине.
                    long freeTmp = Core.FreeSpaceOf(System.IO.Path.GetTempPath());
                    long freeRoot = Core.FreeSpaceOf(root);
                    long worst = Math.Min(freeTmp < 0 ? long.MaxValue : freeTmp,
                                          freeRoot < 0 ? long.MaxValue : freeRoot);
                    if (worst >= 0 && worst < UpdateFreeSpaceNeeded)
                    {
                        string noSpace = string.Format(Loc.T("mw.updNoSpace"),
                            Core.HumanSize(worst), Core.HumanSize(UpdateFreeSpaceNeeded));
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = noSpace;
                            FinishZapretUpdate(noSpace, false);
                            Core.Fail(noSpace);
                        });
                        return;
                    }
                    // Имя архива и его SHA-256 берём из GitHub API одним
                    // запросом. Без контрольной суммы обновление не ставим:
                    // bin\winws.exe из архива затем работает службой SYSTEM.
                    string url, zipSha;
                    if (!Core.ZapretZipAssets(out url, out zipSha))
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = Loc.T("mw.updFail");
                            FinishZapretUpdate(Loc.T("dl.hashMissing"), false);
                            Core.Fail(Loc.T("dl.hashMissing"));
                        });
                        return;
                    }
                    Core.Info(string.Format(Loc.T("upd.zipSource"), url));
                    string dlErr;
                    bool ok = Core.DownloadVerified(url, zipSha, zip, delegate (DlProgress p)
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
                                SetZapretUpdateProgress(Loc.T("settings.update.downloading"), progress, p.BytesRead, p.Total, p.SpeedBps);
                            });
                        }
                        catch { }
                    }, null, out dlErr);

                    if (!ok)
                    {
                        string failMsg = string.IsNullOrEmpty(dlErr) ? Loc.T("mw.updFail") : dlErr;
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = Loc.T("mw.updFail");
                            FinishZapretUpdate(failMsg, false);
                            Core.Fail(failMsg);
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
                    Core.DeleteDirSafe(tmpDir);

                    string changelog = null;
                    if (ex) { try { changelog = Core.FetchChangelog(); } catch { } }

                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ex)
                        {
                            string newVer = Core.ZapretVersion();
                            // Раньше и «последней», и «локальной» версией здесь
                            // записывался результат чтения service.bat. Если архив
                            // его не обновил (частичная распаковка, занятые файлы),
                            // карточка сообщала «актуальная версия», а следующая
                            // проверка возвращала кнопку — обновление выглядело как
                            // откат. Теперь «последняя» — запрошенный тег, а
                            // расхождение видно в журнале.
                            _lastZapretLatest = string.IsNullOrEmpty(ver) ? newVer : ver;
                            _lastZapretLocal = newVer;
                            if (SettingsPage.CompareVersions(_lastZapretLatest, newVer) > 0)
                                Core.Warn(string.Format(Loc.T("mw.zapVerNew"),
                                    SettingsPage.NormVer(_lastZapretLatest), SettingsPage.NormVer(newVer)));
                            Core.Set("latest_zapret", _lastZapretLatest); Core.SaveConfig();
                            _updateLine.Text = string.Format(Loc.T("mw.verCurrent"), newVer);
                            FinishZapretUpdate(Loc.T("settings.update.done") + ": " + newVer, true);
                            Core.Good(string.Format(Loc.T("mw.updDone"), newVer));
                            RefreshSidebarVersions();
                            string msg = string.Format(Loc.T("mw.updDone"), newVer);
                            if (!string.IsNullOrEmpty(changelog))
                                msg += "\n\n" + Loc.T("mw.changelog") + ":\n" + changelog;
                            else
                                // Раньше при недоступном API окно молча выходило без
                                // описания, и это читалось как «обновление пустое».
                                msg += "\n\n" + Loc.T("mw.changelogUnavailable");
                            MessageBox.Show(msg, Loc.T("mw.verDlgTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                            Page strategiesPage;
                            if (_pages.TryGetValue("strategies", out strategiesPage))
                            {
                                var sp = strategiesPage as StrategiesPage;
                                if (sp != null) sp.Rebuild();
                            }
                        }
                        else
                        {
                            // Причина (занятые файлы, путь к списку незаменённых)
                            // уходит в карточку «Центра обновлений» и в журнал.
                            // В боковой строке оставляем короткий текст: у неё NoWrap.
                            _updateLine.Text = Loc.T("mw.updFail");
                            FinishZapretUpdate(Loc.T("mw.updFail") + (string.IsNullOrEmpty(err) ? "" : ": " + err), false);
                            Core.Fail(string.Format(Loc.T("dl.failExtract"), err != null ? ": " + err : ""));
                        }
                    });

                    // Восстановление обхода после обновления делаем здесь, в фоновом
                    // потоке: «sc start» отвечает до 20 с и в UI-потоке подвешивал окно.
                    if (ex)
                    {
                        if (restartService) { if (Core.ServiceExists()) Core.StartService(); }
                        else if (restartManual && !string.IsNullOrEmpty(restartStrategy)) RunStrategy(restartStrategy);
                    }
                }
                catch (Exception exUpd)
                {
                    // Здесь было catch { }: сбой обновления не попадал ни в
                    // журнал, ни в «Центр обновлений», а флаг _updating сбрасывался
                    // так, будто всё прошло штатно.
                    Core.Fail(string.Format(Loc.T("mw.updErr"), Core.Short(exUpd.Message)));
                    try
                    {
                        Dispatcher.Invoke((Action)delegate
                        {
                            _updateLine.Text = Loc.T("mw.updFail");
                            FinishZapretUpdate(Loc.T("mw.updFail") + ": " + Core.Short(exUpd.Message), false);
                        });
                    }
                    catch { }
                }
                finally
                {
                    // Без этого в %TEMP% оставался lantern_<guid>\zapret_update.zip
                    // после каждой попытки, включая неудачное скачивание.
                    Core.DeleteDirSafe(tmpDir);
                    try { Dispatcher.Invoke((Action)delegate { _updating = false; }); } catch { _updating = false; }
                }
            });
        }

        public void UpdateZapret(string latestVersion, bool force = false)
        {
            string ver = string.IsNullOrEmpty(latestVersion) ? Core.ZapretVersion() : latestVersion;
            if (!force && (string.IsNullOrEmpty(ver) || SettingsPage.CompareVersions(ver, Core.ZapretVersion()) <= 0)) return;
            if (string.IsNullOrEmpty(ver)) { Core.Warn(Loc.T("mw.verFail")); return; }
            DoUpdate(ver);
        }

        void AfterLoad()
        {
            // Проверка всегда выполняется в фоне при каждом входе в приложение.
            // Она ничего сама не скачивает и не показывает модальные окна.
            CheckUpdates();
            bool isServiceRunning = (Core.ServiceState() == "running");
            if (isServiceRunning)
            {
                Core.Info(Loc.T("service.bypassActiveSvc"));
            }
            else if (Core.GetBool("autostart_run", false) && !string.IsNullOrEmpty(_currentStrategyFile) && Core.IsAdmin())
            {
                RunStrategy(_currentStrategyFile);
            }
            RefreshTop();
            if (Core.TgAutostartAppEnabled && Core.TgProxyInstalled() && !Core.TgProxyRunning())
            {
                string tgErr;
                if (Core.TgProxyStart(out tgErr)) Core.Good(Loc.T("tg.startedOk"));
                else Core.Fail(string.Format(Loc.T("tg.startErr"), tgErr));
            }
            // Автозапуск TG мог изменить состояние после первого RefreshTop.
            RefreshTop();
            if (!Core.IsAdmin())
                Core.Warn(Loc.T("mw.noAdminWarn"));
            StartBypassMonitor();

            // Фоновая проверка целостности: журнал предупредит о пропавших или
            // повреждённых файлах, а «Обновления» предложат восстановить их.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var z = Core.EngineIntegrityIssues();
                var t = Core.TgProxyIntegrityIssues();
                int n = (z == null ? 0 : z.Count) + (t == null ? 0 : t.Count);
                foreach (string issue in z) Core.Warn("Integrity zapret: " + issue);
                foreach (string issue in t) Core.Warn("Integrity TG: " + issue);
                if (n > 0)
                {
                    Core.Warn(string.Format(Loc.T("updates.integrity.found"), n));
                    UiPost(delegate { ShowToast(Loc.T("updates.integrity.startupToast"), Sev.Warn); });
                }
            });
        }

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
                // Во время бенчмарка стратегий или обновления winws убивается
                // намеренно — такие переходы не считаем падением обхода.
                if (Core.WinwsOperationActive()) return;
                bool now = Core.IsWinwsRunning();
                if (_wasRunning && !now)
                {
                    Notify(Loc.T("mw.bypassDown.title"), Loc.T("mw.bypassDown.body"));
                    if (Core.GetBool("watchdog_enabled", false))
                    {
                        Core.Warn("Watchdog: winws stopped unexpectedly, restarting in same mode...");
                        RestartInSameMode(_currentStrategyFile);
                    }
                }
                _wasRunning = now;
            };
            _bypassMonitor.Start();
        }

        // ---------- Системный трей ----------
        void BuildTray()
        {
            try
            {
                CleanupTray();
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
                _trayClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(System.Windows.Forms.SystemInformation.DoubleClickTime) };
                _trayClickTimer.Tick += (s, e) =>
                {
                    _trayClickTimer.Stop();
                    ToggleTrayWidget();
                };
                _tray.MouseClick += (s, e) =>
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    {
                        _trayClickTimer.Stop();
                        _trayClickTimer.Start();
                    }
                    else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    {
                        if (_trayWidget != null && _trayWidget.IsVisible) _trayWidget.Hide();
                    }
                };
                _tray.DoubleClick += (s, e) =>
                {
                    _trayClickTimer.Stop();
                    Dispatcher.Invoke((Action)ShowWindow);
                };
            }
            catch { }
        }

        DispatcherTimer _trayClickTimer;
        System.Windows.Forms.ToolStripMenuItem _trayStatus;
        TrayStatusWidget _trayWidget;
        bool _forceClose;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int RegisterWindowMessage(string message);
        static readonly int WM_TASKBARCREATED = RegisterWindowMessage("TaskbarCreated");
        const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor = new RECT();
            public RECT rcWork = new RECT();
            public int dwFlags = 0;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            try
            {
                var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));
                const int MONITOR_DEFAULTTONEAREST = 0x00000002;
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    var monitorInfo = new MONITORINFO();
                    if (GetMonitorInfo(monitor, monitorInfo))
                    {
                        var rcWorkArea = monitorInfo.rcWork;
                        var rcMonitorArea = monitorInfo.rcMonitor;
                        mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                        mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                        mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                        mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
                    }
                }
                // Сохраняем минимальный размер окна (MinWidth / MinHeight задаются в конструкторе)
                double minW = MinWidth > 0 ? MinWidth : 760;
                double minH = MinHeight > 0 ? MinHeight : 420;
                mmi.ptMinTrackSize.x = (int)(minW * _dpiScaleX);
                mmi.ptMinTrackSize.y = (int)(minH * _dpiScaleY);
                Marshal.StructureToPtr(mmi, lParam, true);
            }
            catch { }
        }

        static double _dpiScaleX = 1.0;
        static double _dpiScaleY = 1.0;
        static void EnsureDpiScale(IntPtr hwnd)
        {
            try
            {
                if (hwnd != IntPtr.Zero)
                {
                    var src = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
                    if (src != null && src.CompositionTarget != null)
                    {
                        _dpiScaleX = src.CompositionTarget.TransformToDevice.M11;
                        _dpiScaleY = src.CompositionTarget.TransformToDevice.M22;
                        return;
                    }
                }
                var main = Application.Current != null ? Application.Current.MainWindow : null;
                if (main != null)
                {
                    var src = PresentationSource.FromVisual(main);
                    if (src != null && src.CompositionTarget != null)
                    {
                        _dpiScaleX = src.CompositionTarget.TransformToDevice.M11;
                        _dpiScaleY = src.CompositionTarget.TransformToDevice.M22;
                        return;
                    }
                }
            }
            catch { }
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == App.WmShowWindow)
            {
                ShowWindow();
                handled = true;
            }
            else if (msg == WM_GETMINMAXINFO)
            {
                EnsureDpiScale(hwnd);
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            else if (msg == WM_TASKBARCREATED)
            {
                try
                {
                    if (_tray != null)
                    {
                        _tray.Visible = false;
                        _tray.Visible = true;
                    }
                    else
                    {
                        BuildTray();
                    }
                }
                catch { }
                handled = true;
            }
            return IntPtr.Zero;
        }

        void Add(System.Windows.Forms.ContextMenuStrip m, string text, Action act)
        {
            var it = new System.Windows.Forms.ToolStripMenuItem(text);
            it.Click += (s, e) => act();
            m.Items.Add(it);
        }

        void UpdateTray(bool bypass, bool tg)
        {
            if (_trayStatus != null)
            {
                if (bypass && tg) _trayStatus.Text = Loc.T("state.bypassTg");
                else if (bypass) _trayStatus.Text = Loc.T("state.bypass") + " · " + (CurrentStrategyName() ?? "");
                else if (tg) _trayStatus.Text = Loc.T("state.tg");
                else _trayStatus.Text = Loc.T("state.stopped");
            }
            if (_trayWidget != null) _trayWidget.Refresh();
        }

        void Notify(string title, string text)
        {
            if (!Core.GetBool("notifications", true)) return;
            try { if (_tray != null) { _tray.BalloonTipTitle = title; _tray.BalloonTipText = text; _tray.ShowBalloonTip(2500); } } catch { }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool BringWindowToTop(IntPtr hWnd);

        public void BringToFront()
        {
            try
            {
                var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    BringWindowToTop(handle);
                    SetForegroundWindow(handle);
                }
                Topmost = true;
                Topmost = false;
                Activate();
                Focus();
            }
            catch { }
        }

        void PauseActivePage()
        {
            try
            {
                if (!string.IsNullOrEmpty(_current) && _pages.ContainsKey(_current))
                    _pages[_current].OnHide();
                else if (_overview != null)
                    _overview.StopTimer();
            }
            catch { }
        }

        void ResumeActivePage()
        {
            try
            {
                if (!string.IsNullOrEmpty(_current) && _pages.ContainsKey(_current))
                    _pages[_current].OnShow();
                else if (_overview != null)
                    _overview.StartTimer();
            }
            catch { }
        }

        void ShowWindow()
        {
            ShowInTaskbar = true;
            Show(); WindowState = WindowState.Normal;
            ResumeActivePage();
            BringToFront();
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
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
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
                UiPost(delegate { RefreshTop(); });
            });
        }

        void ToggleTrayWidget()
        {
            if (_trayWidget == null) _trayWidget = new TrayStatusWidget(this);
            if (_trayWidget.IsVisible) _trayWidget.Hide();
            else _trayWidget.ShowAtCursor();
        }

        // Пишем геометрию не только при закрытии: выключение Windows,
        // установщик и аварийное завершение не дают окну получить Closing.
        // Дебаунс не превращает перетаскивание рамки в поток записей конфига.
        void ScheduleWindowStateSave()
        {
            if (!IsLoaded) return;
            if (_windowSaveTimer == null)
            {
                _windowSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                _windowSaveTimer.Tick += (s, e) =>
                {
                    _windowSaveTimer.Stop();
                    SaveWindowState();
                };
            }
            _windowSaveTimer.Stop();
            _windowSaveTimer.Start();
        }

        // Сохраняем пользовательский размер окна. При maximized/minimized берём
        // RestoreBounds — иначе потерялся бы обычный размер. Если RestoreBounds
        // пуст (скрытое окно), откатываемся на фактические габариты.
        void SaveWindowState()
        {
            try
            {
                Rect b = WindowState == WindowState.Normal
                    ? new Rect(Left, Top, ActualWidth, ActualHeight)
                    : RestoreBounds;
                if (b.IsEmpty || b.Width < MinWidth || b.Height < MinHeight)
                    b = new Rect(Left, Top, ActualWidth, ActualHeight);
                if (b.Width >= MinWidth && b.Height >= MinHeight)
                {
                    Core.SetInt("window_width", (int)Math.Round(b.Width));
                    Core.SetInt("window_height", (int)Math.Round(b.Height));
                }
                Core.SetBool("window_maximized", _maximized);
                Core.SaveConfig();
            }
            catch { }
        }

        void OnSessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            if (_windowSaveTimer != null) _windowSaveTimer.Stop();
            SaveWindowState();
        }

        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_windowSaveTimer != null) _windowSaveTimer.Stop();
            if (Application.Current != null) Application.Current.SessionEnding -= OnSessionEnding;
            SaveWindowState();
            // App.ForceShutdown ставится перед Application.Shutdown() (перезапуск
            // с правами администратора). Без него при tray_on_close = true закрытие
            // превращалось в сворачивание в трей, Shutdown() молча отменялся, и после
            // рестарта оставалось «призрачное» второе окно с иконкой.
            if (!_forceClose && !App.ForceShutdown && Core.GetBool("tray_on_close", true))
            {
                e.Cancel = true;
                PauseActivePage();
                Hide();
                Notify(string.Format(Loc.T("mw.tray.minTitle"), Core.AppName), Loc.T("mw.tray.minBody"));
                Core.TrimMemory();
                return;
            }
            try { if (_bypassMonitor != null) _bypassMonitor.Stop(); } catch { }
            foreach (var p in _pages.Values) { try { p.OnHide(); } catch { } }
            Loc.LanguageChanged -= OnUiChanged;
            Theme.ThemeChanged -= OnUiChanged;
            try { if (_trayWidget != null) { _trayWidget.Close(); _trayWidget = null; } } catch { }
            CleanupTray();
        }
    }
}
