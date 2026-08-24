using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ZapretStudio
{
    // Компактная карточка, которая открывается левой кнопкой по иконке в трее.
    // Это отдельное маленькое окно, а не системное меню: так оно полностью
    // наследует палитру приложения и выглядит одинаково в каждой теме.
    sealed class TrayStatusWidget : Window
    {
        readonly MainWindow _owner;
        readonly TextBlock _zapretState;
        readonly TextBlock _tgState;
        readonly TextBlock _strategy;
        readonly ContentControl _zapretAction;
        readonly ContentControl _tgAction;
        readonly SolidColorBrush _zapretDot = new SolidColorBrush();
        readonly SolidColorBrush _tgDot = new SolidColorBrush();
        readonly SolidColorBrush _zapretTint = new SolidColorBrush();
        readonly SolidColorBrush _tgTint = new SolidColorBrush();

        // Кнопки создаются один раз в конструкторе; Refresh обновляет только
        // подпись/иконку/активность и цель действия. Раньше на каждый Refresh
        // строился новый Button (пересоздание визуала и обработчиков).
        System.Windows.Controls.Button _zapretBtn, _tgBtn;
        Action _zapretAction2, _tgAction2;

        public TrayStatusWidget(MainWindow owner)
        {
            _owner = owner;
            // Текстовые кнопки в статусах выше прежних иконок. Запас по высоте
            // исключает обрезание нижней карточки на любом системном масштабе.
            Width = 344; Height = 306;
            MinWidth = Width; MaxWidth = Width;
            MinHeight = Height; MaxHeight = Height;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = true;
            Topmost = true;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            UseLayoutRounding = true;

            var frame = new Border
            {
                Background = Theme.BrBgBase, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), CornerRadius = Theme.R14,
                Padding = new Thickness(16),
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 22, ShadowDepth = 7, Opacity = 0.28, Direction = 270 }
            };
            var root = new StackPanel();
            frame.Child = root;

            var header = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var logo = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(11), Background = Theme.BrAccent };
            logo.Child = UI.Icon(Icons.Lantern, 19, Theme.BrOnAccent, 1.8);
            header.Children.Add(logo);
            var heading = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            heading.Children.Add(new TextBlock { Text = Core.AppName, Foreground = Theme.BrText, FontFamily = Theme.UiFont,
                FontSize = Theme.FsBody, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = Loc.T("tray.widget.caption"), Foreground = Theme.BrFaint,
                FontFamily = Theme.UiFont, FontSize = Theme.FsTiny, Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(heading, 1); header.Children.Add(heading);
            // Только индикатор LIVE прямоугольный: как компактная метка в интерфейсе,
            // а не овальная капсула. У самой карточки остаются мягкие углы.
            var live = new Border { Background = Theme.BrSurfaceAlt, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Center };
            var liveSp = new StackPanel { Orientation = Orientation.Horizontal };
                        liveSp.Children.Add(new Border { Width = 6, Height = 6, CornerRadius = Theme.Rpill, Background = Theme.BrAccent, VerticalAlignment = VerticalAlignment.Center });
            liveSp.Children.Add(new TextBlock { Text = Loc.T("tray.widget.live"), Foreground = Theme.BrAccent, FontFamily = Theme.UiFont, FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(5, 0, 0, 0) });
            live.Child = liveSp;
            Grid.SetColumn(live, 2); header.Children.Add(live);
            root.Children.Add(header);

            _zapretState = AddServiceRow(root, Icons.Shield, "zapret", _zapretDot, _zapretTint, out _zapretAction);
            _tgState = AddServiceRow(root, Icons.Telegram, "TG Proxy", _tgDot, _tgTint, out _tgAction);
            _zapretBtn = MakeActionButton(() => { if (_zapretAction2 != null) _zapretAction2(); });
            _tgBtn = MakeActionButton(() => { if (_tgAction2 != null) _tgAction2(); });
            _zapretAction.Content = _zapretBtn;
            _tgAction.Content = _tgBtn;

            var strategyCard = new Border { Background = Theme.BrSurfaceAlt, BorderBrush = Theme.BrStrokeSoft, BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10, Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 10, 0, 0) };
            var strategyStack = new StackPanel();
            strategyStack.Children.Add(new TextBlock { Text = Loc.T("tray.widget.strategy").ToUpperInvariant(), Foreground = Theme.BrFaint,
                FontFamily = Theme.UiFont, FontSize = 10, FontWeight = FontWeights.SemiBold });
            _strategy = new TextBlock { Foreground = Theme.BrText, FontFamily = Theme.UiFont, FontSize = Theme.FsSmall,
                FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) };
            strategyStack.Children.Add(_strategy);
            strategyCard.Child = strategyStack;
            root.Children.Add(strategyCard);

            Content = frame;
            Deactivated += (s, e) => Hide();
            Theme.ThemeChanged += OnThemeChanged;
            Closed += (s, e) => Theme.ThemeChanged -= OnThemeChanged;
            Refresh();
        }

        TextBlock AddServiceRow(Panel parent, string icon, string title, SolidColorBrush dot, SolidColorBrush tint, out ContentControl action)
        {
            var card = new Border { Background = Theme.BrSurface, BorderBrush = Theme.BrStrokeSoft, BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10, Padding = new Thickness(12, 10, 12, 10), Margin = new Thickness(0, 0, 0, 8) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var iconBox = new Border { Width = 28, Height = 28, Background = Theme.BrSurfaceAlt, CornerRadius = Theme.R8 };
            iconBox.Child = UI.Icon(icon, 16, Theme.BrMuted, 1.7);
            g.Children.Add(iconBox);
            var text = new StackPanel { Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = title, Foreground = Theme.BrText, FontFamily = Theme.UiFont, FontSize = Theme.FsSmall,
                FontWeight = FontWeights.SemiBold });
            var state = new TextBlock { FontFamily = Theme.UiFont, FontSize = Theme.FsTiny, FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 0, 0) };
            var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            status.Children.Add(new Border { Width = 7, Height = 7, CornerRadius = Theme.Rpill, Background = dot, VerticalAlignment = VerticalAlignment.Center });
            status.Children.Add(state);
            text.Children.Add(status);
            Grid.SetColumn(text, 1); g.Children.Add(text);
            action = new ContentControl { Width = 118, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            Grid.SetColumn(action, 2); g.Children.Add(action);
            card.Child = g; parent.Children.Add(card);
            return state;
        }

        void OnThemeChanged() { Refresh(); }

        public void Refresh()
        {
            bool zapretOn = _owner.IsActive2();
            SetState(_zapretState, _zapretDot, _zapretTint, zapretOn ? Theme.Ok : Theme.TextFaint,
                zapretOn ? Loc.T("state.running") : Loc.T("state.stopped"));
            _zapretAction2 = delegate { _owner.ToggleRun(); };
            UpdateActionButton(_zapretBtn, zapretOn, true, zapretOn ? Loc.T("common.stop") : Loc.T("common.start"));

            bool tgInstalled = Core.TgProxyInstalled();
            bool tgOn = tgInstalled && Core.TgProxyRunning();
            SetState(_tgState, _tgDot, _tgTint, tgOn ? Theme.Ok : (tgInstalled ? Theme.TextFaint : Theme.Warn),
                !tgInstalled ? Loc.T("settings.tg.notInstalled") : (tgOn ? Loc.T("state.running") : Loc.T("state.stopped")));
            _tgAction2 = delegate { _owner.ToggleTgProxyFromTray(); };
            UpdateActionButton(_tgBtn, tgOn, tgInstalled, tgOn ? Loc.T("common.stop") : Loc.T("common.start"));

            string name = _owner.CurrentStrategyName();
            _strategy.Text = string.IsNullOrEmpty(name) ? Loc.T("tray.widget.strategyNone") : name;
        }

        static System.Windows.Controls.Button MakeActionButton(Action action)
        {
            var b = Ctl.Button("", null, 0);
            b.Click += (s, e) => action();
            return b;
        }

        static void UpdateActionButton(System.Windows.Controls.Button button, bool running, bool enabled, string text)
        {
            if (button == null) return;
            Ctl.SetButton(button, text, running ? Icons.Stop : Icons.Play, running ? 2 : 0);
            button.IsEnabled = enabled;
        }

        static void SetState(TextBlock label, SolidColorBrush dot, SolidColorBrush tint, Color color, string text)
        {
            dot.Color = color;
            tint.Color = color;
            label.Foreground = tint;
            label.Text = text;
        }

        public void ShowAtCursor()
        {
            Refresh();
            var cursor = System.Windows.Forms.Cursor.Position;
            var source = PresentationSource.FromVisual(_owner);
            Matrix fromDevice = source != null && source.CompositionTarget != null
                ? source.CompositionTarget.TransformFromDevice : Matrix.Identity;
            Point p = fromDevice.Transform(new Point(cursor.X, cursor.Y));
            var area = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
            Point workTopLeft = fromDevice.Transform(new Point(area.Left, area.Top));
            Point workBottomRight = fromDevice.Transform(new Point(area.Right, area.Bottom));
            Left = Math.Max(workTopLeft.X + 8, Math.Min(p.X - Width + 18, workBottomRight.X - Width - 8));
            Top = Math.Max(workTopLeft.Y + 8, Math.Min(p.Y - Height - 14, workBottomRight.Y - Height - 8));
            if (!IsVisible) Show();
            Activate();
        }
    }
}
