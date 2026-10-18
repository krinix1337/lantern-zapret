using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZapretStudio
{
    // Базовый класс экрана: заголовок + прокручиваемое тело.
    abstract class Page : UserControl
    {
        protected StackPanel Body;
        protected SmoothScrollViewer ScrollHost;
        protected ContentControl HeaderActionSlot;
        public double ScrollOffset
        {
            get { return ScrollHost != null ? ScrollHost.VerticalOffset : 0; }
            set { if (ScrollHost != null) ScrollHost.JumpTo(value); }
        }
        public abstract string Title { get; }
        public abstract string Subtitle { get; }
        public virtual void OnShow() { }
        public virtual void OnHide() { }

        protected Page()
        {
            Background = Brushes.Transparent;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var headGrid = new Grid { Margin = new Thickness(28, 24, 28, 8) };
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var head = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            head.Children.Add(UI.T(Title, Theme.FsH1, Theme.BrText, FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(Subtitle))
                head.Children.Add(new TextBlock { Text = Subtitle, Foreground = Theme.BrMuted,
                    FontSize = Theme.FsBody, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(head, 0);
            headGrid.Children.Add(head);

            HeaderActionSlot = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(HeaderActionSlot, 1);
            headGrid.Children.Add(HeaderActionSlot);

            headGrid.PreviewMouseWheel += (s, e) =>
            {
                if (ScrollHost != null && ScrollHost.ScrollableHeight > 0)
                {
                    ScrollHost.ScrollByDelta(e.Delta);
                    e.Handled = true;
                }
            };

            Grid.SetRow(headGrid, 0);
            root.Children.Add(headGrid);

            Body = new StackPanel { Margin = new Thickness(28, 12, 28, 28) };
            ScrollHost = new SmoothScrollViewer
            {
                Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0),
                Background = Brushes.Transparent
            };
            Grid.SetRow(ScrollHost, 1);
            root.Children.Add(ScrollHost);
            Content = root;
        }

        // ---- строительные блоки ----
        protected static TextBlock SectionLabel(string t)
        {
            return new TextBlock { Text = t.ToUpperInvariant(), Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, Theme.Dense(18), 0, Theme.Dense(8)) };
        }

        // Карточка-строка: слева заголовок+описание, справа контрол.
        protected static Border Row(string title, string desc, UIElement right)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(UI.T(title, Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(desc))
                left.Children.Add(new TextBlock { Text = desc, Foreground = Theme.BrMuted,
                    FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            if (right != null)
            {
                var rc = new ContentControl { Content = right, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0), Focusable = false };
                Grid.SetColumn(rc, 1);
                g.Children.Add(rc);
            }
            return UI.Card(g, new Thickness(16, 14, 16, 14));
        }

        protected static Border Group(params UIElement[] rows)
        {
            var sp = new StackPanel();
            for (int i = 0; i < rows.Length; i++)
            {
                sp.Children.Add(rows[i]);
                if (i < rows.Length - 1) sp.Children.Add(new Border { Height = Theme.Dense(10) });
            }
            return new Border { Child = sp };
        }

        // Карточка-заметка: иконка слева + переносящийся текст. В отличие от простого
        // горизонтального StackPanel, текст здесь получает ограниченную ширину и корректно
        // переносится (в StackPanel он уходил в бесконечность и обрезался).
        protected static Border NoteCard(string icon, Brush iconBrush, string text, Sev tint)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var ic = UI.Icon(icon, 18, iconBrush, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Top;
            ic.Margin = new Thickness(0, 1, 0, 0);
            Grid.SetColumn(ic, 0);
            g.Children.Add(ic);
            var t = new TextBlock
            {
                Text = text, Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(11, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(t, 1);
            g.Children.Add(t);

            var card = UI.Card(g, new Thickness(16, 12, 16, 12), Theme.R10, Theme.BrSurface);
            if (tint != Sev.Neutral)
            {
                var sc = UI2.SevColor(tint);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(90, sc.R, sc.G, sc.B));
                card.Background = new SolidColorBrush(Color.FromArgb(14, sc.R, sc.G, sc.B));
            }
            else
            {
                card.BorderBrush = Theme.BrStroke;
            }
            return card;
        }

        // Плашка-объяснение с действием внутри: текст слева, кнопки/статус справа.
        // Прежний вид «карточка с текстом, а под ней одинокая кнопка» читался как
        // два несвязанных блока (замечание по разделу «Настройки»).
        protected static Border ActionCard(string icon, Brush iconBrush, string text, Sev tint, params UIElement[] actions)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var ic = UI.Icon(icon, 18, iconBrush, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(ic, 0);
            g.Children.Add(ic);

            var t = new TextBlock
            {
                Text = text, Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(11, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(t, 1);
            g.Children.Add(t);

            var host = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (actions != null)
                foreach (var a in actions)
                {
                    if (a == null) continue;
                    var fe = a as FrameworkElement;
                    if (fe != null && host.Children.Count > 0) fe.Margin = new Thickness(10, 0, 0, 0);
                    host.Children.Add(a);
                }
            Grid.SetColumn(host, 2);
            g.Children.Add(host);

            var card = UI.Card(g, new Thickness(16, 12, 16, 12), Theme.R10, Theme.BrSurface);
            if (tint != Sev.Neutral)
            {
                var sc = UI2.SevColor(tint);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(90, sc.R, sc.G, sc.B));
                card.Background = new SolidColorBrush(Color.FromArgb(14, sc.R, sc.G, sc.B));
            }
            else card.BorderBrush = Theme.BrStroke;
            return card;
        }

        internal static readonly System.Collections.Generic.List<ComboBox> ActiveCombos = new System.Collections.Generic.List<ComboBox>();

        // ComboBox в стиле темы (системный по умолчанию рисует белый выпадающий список).
        protected static ComboBox Combo(double width)
        {
            var cb = new ComboBox
            {
                Width = width, HorizontalAlignment = HorizontalAlignment.Left, Height = 34,
                FontSize = Theme.FsBody, FontFamily = Theme.UiFont,
                Foreground = Theme.BrText, Background = Theme.BrSurfaceAlt,
                BorderBrush = Theme.BrStroke,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            cb.DropDownOpened += (s, e) => { if (!ActiveCombos.Contains(cb)) ActiveCombos.Add(cb); AnimateDropdown(cb); };
            cb.DropDownClosed += (s, e) => { ActiveCombos.Remove(cb); };
            cb.Unloaded += (s, e) => { ActiveCombos.Remove(cb); };
            cb.FocusVisualStyle = null;
            cb.PreviewKeyDown += (s, e) =>
            {
                if (!cb.IsDropDownOpen)
                {
                    if (e.Key == Key.Enter || e.Key == Key.Space || e.Key == Key.F4 ||
                        (e.Key == Key.Down && (Keyboard.Modifiers & ModifierKeys.Alt) != 0))
                    {
                        cb.IsDropDownOpen = true;
                        e.Handled = true;
                    }
                }
                else
                {
                    if (e.Key == Key.Escape)
                    {
                        cb.IsDropDownOpen = false;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Enter)
                    {
                        // При нажатии Enter фиксируем подсвеченный стрелками пункт
                        for (int i = 0; i < cb.Items.Count; i++)
                        {
                            var cbi = cb.ItemContainerGenerator.ContainerFromIndex(i) as ComboBoxItem;
                            if (cbi != null && cbi.IsHighlighted)
                            {
                                cb.SelectedIndex = i;
                                break;
                            }
                        }
                        cb.IsDropDownOpen = false;
                        e.Handled = true;
                    }
                    else if (e.Key == Key.Tab)
                    {
                        // При нажатии Tab фиксируем подсвеченный пункт и штатно переходим
                        // к следующему элементу формы (без необходимости жать Tab дважды)
                        for (int i = 0; i < cb.Items.Count; i++)
                        {
                            var cbi = cb.ItemContainerGenerator.ContainerFromIndex(i) as ComboBoxItem;
                            if (cbi != null && cbi.IsHighlighted)
                            {
                                cb.SelectedIndex = i;
                                break;
                            }
                        }
                        cb.IsDropDownOpen = false;
                    }
                }
            };
            cb.MouseEnter += (s, e) => { if (!cb.IsKeyboardFocusWithin) Ctl.SetGlow(ComboSurface(cb), true, false); };
            cb.MouseLeave += (s, e) => { if (!cb.IsKeyboardFocusWithin) Ctl.SetGlow(ComboSurface(cb), false, false); };
            cb.GotKeyboardFocus += (s, e) =>
            {
                cb.BorderBrush = Theme.BrAccentHi;
                Ctl.SetGlow(ComboSurface(cb), true, true);
                cb.BringIntoView();
            };
            cb.LostKeyboardFocus += (s, e) =>
            {
                cb.BorderBrush = Theme.BrStroke;
                Ctl.SetGlow(ComboSurface(cb), cb.IsMouseOver, false);
            };
            cb.Resources = ComboResources();
            cb.Template = ComboTemplate();
            return cb;
        }

        // Заливка «коробки» ComboBox живёт в шаблоне внутреннего ToggleButton.
        static Border ComboSurface(ComboBox cb)
        {
            try
            {
                var tg = cb.Template.FindName("PART_Toggle", cb) as System.Windows.Controls.Primitives.ToggleButton;
                return tg != null && tg.Template != null ? tg.Template.FindName("tb", tg) as Border : null;
            }
            catch { return null; }
        }

        // Системная PopupAnimation.Fade зависит от настроек Windows («затухание меню»)
        // и у многих выключена — список просто выскакивает. Анимируем бордюр попапа сами.
        static void AnimateDropdown(ComboBox cb)
        {
            if (!Theme.AnimationsEnabled) return;
            try
            {
                var pop = cb.Template.FindName("PART_Popup", cb) as System.Windows.Controls.Primitives.Popup;
                if (pop == null) return;
                UiAnim.Popup(pop.Child as FrameworkElement);
            }
            catch { }
        }

        // ВАЖНО: в шаблоны/стили нельзя класть мутируемые Theme.Br* — WPF замораживает
        // ресурсы шаблона при «запечатывании», и следующая смена темы падает с
        // "read-only state". Берём замороженные снимки текущей палитры (Theme.Frozen).
        // Не кэшируем: интерфейс полностью пересобирается при смене темы, снимки свежие.
        static ResourceDictionary ComboResources()
        {
            var rd = new ResourceDictionary();
            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Frozen(Theme.Text)));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, (Brush)Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.FontFamilyProperty, Theme.UiFont));
            itemStyle.Setters.Add(new Setter(Control.FontSizeProperty, Theme.FsBody));
            var itmpl = new ControlTemplate(typeof(ComboBoxItem));
            var ibd = new FrameworkElementFactory(typeof(Border));
            ibd.Name = "ib";
            ibd.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            ibd.SetValue(Border.PaddingProperty, new Thickness(10, 7, 10, 7));
            ibd.SetValue(Border.CornerRadiusProperty, Theme.R8);
            ibd.SetValue(Border.MarginProperty, new Thickness(4, 2, 4, 2));
            var icp = new FrameworkElementFactory(typeof(ContentPresenter));
            icp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            ibd.AppendChild(icp);
            itmpl.VisualTree = ibd;
            var hov = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
            hov.Setters.Add(new Setter(Border.BackgroundProperty, Theme.Frozen(Theme.SurfaceHi), "ib"));
            itmpl.Triggers.Add(hov);
            var selT = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
            selT.Setters.Add(new Setter(Border.BackgroundProperty, Theme.Alpha(Theme.AccentMain, 55), "ib"));
            itmpl.Triggers.Add(selT);
            itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itmpl));
            rd[typeof(ComboBoxItem)] = itemStyle;
            return rd;
        }

        static ControlTemplate ComboTemplate()
        {
            var t = new ControlTemplate(typeof(ComboBox));

            var root = new FrameworkElementFactory(typeof(Grid));

            // Кнопка-«коробка» открывает список.
            var toggle = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton));
            toggle.Name = "PART_Toggle";
            toggle.SetValue(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
                new System.Windows.Data.Binding("IsDropDownOpen") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent, Mode = System.Windows.Data.BindingMode.TwoWay });
            toggle.SetValue(FrameworkElement.FocusableProperty, false);
            toggle.SetValue(System.Windows.Controls.Primitives.ButtonBase.ClickModeProperty, ClickMode.Press);
            toggle.SetValue(Control.TemplateProperty, ComboToggleTemplate());
            root.AppendChild(toggle);

            // Отображение выбранного значения.
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.ContentProperty, new System.Windows.Data.Binding("SelectionBoxItem") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            content.SetValue(ContentPresenter.MarginProperty, new Thickness(12, 0, 34, 0));
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            content.SetValue(FrameworkElement.IsHitTestVisibleProperty, false);
            root.AppendChild(content);

            // Выпадающий список.
            var popup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
            popup.Name = "PART_Popup";
            popup.SetValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
                new System.Windows.Data.Binding("IsDropDownOpen") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty, System.Windows.Controls.Primitives.PlacementMode.Bottom);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PopupAnimationProperty, System.Windows.Controls.Primitives.PopupAnimation.None);
            popup.SetValue(System.Windows.Controls.Primitives.Popup.FocusableProperty, false);

            var popBorder = new FrameworkElementFactory(typeof(Border));
            popBorder.SetValue(Border.BackgroundProperty, Theme.Frozen(Theme.Surface));
            popBorder.SetValue(Border.BorderBrushProperty, Theme.Frozen(Theme.Stroke));
            popBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popBorder.SetValue(Border.CornerRadiusProperty, Theme.R10);
            popBorder.SetValue(Border.MarginProperty, new Thickness(0, 4, 0, 4));
            popBorder.SetValue(Border.PaddingProperty, new Thickness(0, 4, 0, 4));
            popBorder.SetValue(FrameworkElement.MinWidthProperty,
                new System.Windows.Data.Binding("ActualWidth") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
            popBorder.SetValue(Border.EffectProperty, new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 14, ShadowDepth = 3, Opacity = 0.35, Direction = 270 });

            var scroll = new FrameworkElementFactory(typeof(ScrollViewer));
            scroll.Name = "DropDownScrollViewer";
            scroll.SetValue(ScrollViewer.CanContentScrollProperty, false);
            scroll.SetValue(ScrollViewer.MaxHeightProperty, 260.0);
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            scroll.SetValue(ScrollViewer.PanningModeProperty, PanningMode.VerticalOnly);
            scroll.SetValue(FrameworkElement.FocusableProperty, false);
            scroll.AddHandler(UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler((s, e) =>
            {
                SmoothWheel.Scroll(s as ScrollViewer, e.Delta, 36);
                e.Handled = true;
            }));
            var items = new FrameworkElementFactory(typeof(ItemsPresenter));
            scroll.AppendChild(items);
            popBorder.AppendChild(scroll);
            popup.AppendChild(popBorder);
            root.AppendChild(popup);

            t.VisualTree = root;
            return t;
        }

        static ControlTemplate ComboToggleTemplate()
        {
            var t = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
            var bd = new FrameworkElementFactory(typeof(Border));
            bd.Name = "tb";
            bd.SetValue(Border.BackgroundProperty, Theme.Frozen(Theme.SurfaceAlt));
            bd.SetValue(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ComboBox), 1) });
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            bd.SetValue(Border.CornerRadiusProperty, Theme.R10);
            // Стрелка — сосед заливки, а не её ребёнок: неон не должен её размывать.
            var grid = new FrameworkElementFactory(typeof(Grid));
            var arrow = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));
            arrow.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M6 9 L12 15 L18 9"));
            arrow.SetValue(System.Windows.Shapes.Shape.StrokeProperty, Theme.Frozen(Theme.TextMuted));
            arrow.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.7);
            arrow.SetValue(System.Windows.Shapes.Shape.StretchProperty, Stretch.Uniform);
            arrow.SetValue(FrameworkElement.WidthProperty, 14.0);
            arrow.SetValue(FrameworkElement.HeightProperty, 14.0);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0));
            grid.AppendChild(arrow);
            var root = new FrameworkElementFactory(typeof(Grid));
            root.AppendChild(bd);
            root.AppendChild(grid);
            t.VisualTree = root;
            var hov = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Frozen(Theme.AccentMain), "tb"));
            t.Triggers.Add(hov);
            var chk = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
            chk.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Frozen(Theme.AccentMain), "tb"));
            t.Triggers.Add(chk);
            return t;
        }

        static UIElement space10() { return new Border { Height = 10 }; }
    }

    // Плавная инерционная прокрутка на частоте обновления монитора (CompositionTarget.Rendering)
    class SmoothScrollViewer : ScrollViewer
    {
        const double WheelStep = 64;
        double _targetOffset;
        double _lastApplied;
        bool _isAnimating;
        double _pendingScroll = -1;
        readonly System.Diagnostics.Stopwatch _clock = new System.Diagnostics.Stopwatch();
        double _lastMs;

        public SmoothScrollViewer()
        {
            Background = Brushes.Transparent;
            PreviewMouseWheel += OnPreviewMouseWheel;
            ScrollChanged += (s, e) =>
            {
                if (_pendingScroll >= 0 && ScrollableHeight > 0)
                {
                    double target = _pendingScroll;
                    _pendingScroll = -1;
                    _targetOffset = target;
                    _lastApplied = target;
                    ScrollToVerticalOffset(Math.Min(target, ScrollableHeight));
                }
                else if (!_isAnimating)
                {
                    _targetOffset = VerticalOffset;
                }
            };
            LayoutUpdated += (s, e) =>
            {
                if (_pendingScroll >= 0 && ScrollableHeight > 0)
                {
                    double target = _pendingScroll;
                    _pendingScroll = -1;
                    _targetOffset = target;
                    _lastApplied = target;
                    ScrollToVerticalOffset(Math.Min(target, ScrollableHeight));
                }
            };
            Unloaded += (s, e) => StopAnimation();
        }

        public static void CloseAnyOpenDropdown(DependencyObject root)
        {
            try
            {
                for (int i = Page.ActiveCombos.Count - 1; i >= 0; i--)
                {
                    var cb = Page.ActiveCombos[i];
                    if (cb != null && cb.IsDropDownOpen)
                    {
                        cb.IsDropDownOpen = false;
                    }
                }
                Page.ActiveCombos.Clear();

                // Все ComboBox приложения создаются через Page.Combo и попадают
                // в ActiveCombos, поэтому полный обход визуального дерева на
                // каждое колесо не нужен — он и давал лаги при прокрутке.
                if (root != null)
                {
                    var win = (root as MainWindow) ?? (Window.GetWindow(root) as MainWindow);
                    if (win != null) win.CloseFlavorPopup();
                }
            }
            catch { }
        }

        public void ScrollByDelta(int deltaRaw)
        {
            CloseAnyOpenDropdown(this);
            if (ScrollableHeight <= 0) return;
            double delta = (deltaRaw / 120.0) * WheelStep;
            double cur = _isAnimating ? _targetOffset : VerticalOffset;
            _targetOffset = Math.Max(0, Math.Min(ScrollableHeight, cur - delta));

            if (!Theme.AnimationsEnabled)
            {
                ScrollToVerticalOffset(_targetOffset);
                return;
            }

            if (!_isAnimating)
            {
                _isAnimating = true;
                _lastApplied = VerticalOffset;
                _clock.Reset();
                _clock.Start();
                _lastMs = 0;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        public void JumpTo(double offset)
        {
            StopAnimation();
            _targetOffset = offset;
            _lastApplied = offset;
            if (ScrollableHeight > 0)
            {
                _pendingScroll = -1;
                ScrollToVerticalOffset(Math.Min(offset, ScrollableHeight));
            }
            else
            {
                _pendingScroll = offset;
            }
        }

        void StopAnimation()
        {
            if (_isAnimating)
            {
                _isAnimating = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        public static bool IsInsideDropdown(DependencyObject d, object source)
        {
            // Закрытый ComboBox не должен перехватывать колесо: иначе страница
            // «залипает», когда курсор проходит над списком при прокрутке.
            var cbSource = source as ComboBox;
            if (cbSource != null) return cbSource.IsDropDownOpen;
            while (d != null)
            {
                var cb = d as ComboBox;
                if (cb != null) return cb.IsDropDownOpen;
                if (d is System.Windows.Controls.Primitives.Popup) return true;
                string typeName = d.GetType().Name;
                if (typeName == "PopupRoot") return true;
                var sv = d as ScrollViewer;
                if (sv != null && sv.Name == "DropDownScrollViewer") return true;
                DependencyObject parent = null;
                try { parent = VisualTreeHelper.GetParent(d); } catch { }
                if (parent == null && d is FrameworkElement)
                {
                    try { parent = ((FrameworkElement)d).Parent; } catch { }
                }
                d = parent;
            }
            return false;
        }

        void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsInsideDropdown(e.OriginalSource as DependencyObject, e.Source)) return;
            if (IsInsideNestedScroller(e.OriginalSource as DependencyObject)) return;
            if (ScrollableHeight <= 0) return;
            e.Handled = true;
            ScrollByDelta(e.Delta);
        }

        // Если событие колеса пришло изнутри вложенного ScrollViewer (не нашего),
        // отдаём его тому ScrollViewer — иначе чексуммы и другие вложенные списки
        // не прокручиваются.
        bool IsInsideNestedScroller(DependencyObject d)
        {
            while (d != null)
            {
                if (d == this) return false;
                var sv = d as ScrollViewer;
                if (sv != null && sv.ScrollableHeight > 0) return true;
                DependencyObject parent = null;
                try { parent = VisualTreeHelper.GetParent(d); } catch { }
                if (parent == null && d is FrameworkElement)
                {
                    try { parent = ((FrameworkElement)d).Parent; } catch { }
                }
                d = parent;
            }
            return false;
        }

        void OnRendering(object sender, EventArgs e)
        {
            // Смещение, не сделанное нами (перетаскивание ползунка скроллбара,
            // клавиатура): прекращаем анимацию и отдаём управление пользователю.
            if (Math.Abs(VerticalOffset - _lastApplied) > 1.5)
            {
                StopAnimation();
                _targetOffset = VerticalOffset;
                return;
            }

            double cur = VerticalOffset;
            double diff = _targetOffset - cur;
            if (Math.Abs(diff) < 0.4 || ScrollableHeight <= 0)
            {
                ScrollToVerticalOffset(_targetOffset);
                StopAnimation();
                return;
            }

            // Экспоненциальное приближение к цели: доля пройденного пути зависит
            // от реально прошедшего времени, а не от числа кадров (60/120/144 Гц
            // дают одинаковое ощущение), и тормозит к концу — без рывков.
            // Время берём с дробной точностью: на 120/144 Гц кадр короче миллисекунды
            // не целой, и округление до мс давало рваный шаг.
            double now = _clock.Elapsed.TotalMilliseconds;
            double dt = now - _lastMs;
            _lastMs = now;
            if (dt <= 0) dt = 16.7;
            if (dt > 100) dt = 100;
            double k = 1.0 - Math.Exp(-dt / SmoothWheel.Tau);
            double next = cur + diff * k;
            _lastApplied = next;
            ScrollToVerticalOffset(next);
        }
    }

    // Плавная прокрутка колесом для обычных ScrollViewer: вложенные списки,
    // выпадающий список ComboBox, всплывающие панели. Там, где SmoothScrollViewer
    // не используется, поведение колеса должно совпадать с ним.
    static class SmoothWheel
    {
        public const double Tau = 110; // мс: за это время проходится ~63% остатка пути

        class Run
        {
            public double Target;
            public double Applied;
            public double LastMs;
            public EventHandler Handler;
            public RoutedEventHandler UnloadedHandler;
            public readonly System.Diagnostics.Stopwatch Clock = new System.Diagnostics.Stopwatch();
        }

        static readonly System.Collections.Generic.Dictionary<ScrollViewer, Run> Map =
            new System.Collections.Generic.Dictionary<ScrollViewer, Run>();

        public static void Scroll(ScrollViewer sv, int deltaRaw, double step)
        {
            if (sv == null || sv.ScrollableHeight <= 0) return;
            double delta = (deltaRaw / 120.0) * step;

            if (!Theme.AnimationsEnabled)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - delta);
                return;
            }

            Run r;
            if (!Map.TryGetValue(sv, out r))
            {
                r = new Run();
                r.Target = sv.VerticalOffset;
                r.Applied = sv.VerticalOffset;
                r.Handler = delegate { Tick(sv); };
                // Выпавший из дерева список не должен оставаться в статическом
                // словаре, если Rendering больше не тикает (окно свёрнуто в трей).
                r.UnloadedHandler = delegate { Stop(sv, r); };
                sv.Unloaded += r.UnloadedHandler;
                Map[sv] = r;
                r.Clock.Start();
                CompositionTarget.Rendering += r.Handler;
            }
            r.LastMs = r.Clock.Elapsed.TotalMilliseconds;
            r.Target = Math.Max(0, Math.Min(sv.ScrollableHeight, r.Target - delta));
        }

        static void Tick(ScrollViewer sv)
        {
            Run r;
            if (!Map.TryGetValue(sv, out r)) return;

            if (Math.Abs(sv.VerticalOffset - r.Applied) > 1.5)
            {
                Stop(sv, r);
                return;
            }

            double cur = sv.VerticalOffset;
            double diff = r.Target - cur;
            if (Math.Abs(diff) < 0.4 || sv.ScrollableHeight <= 0)
            {
                sv.ScrollToVerticalOffset(r.Target);
                Stop(sv, r);
                return;
            }

            double now = r.Clock.Elapsed.TotalMilliseconds;
            double dt = now - r.LastMs;
            r.LastMs = now;
            if (dt <= 0) dt = 16.7;
            if (dt > 100) dt = 100;

            double k = 1.0 - Math.Exp(-dt / Tau);
            r.Applied = cur + diff * k;
            sv.ScrollToVerticalOffset(r.Applied);
        }

        static void Stop(ScrollViewer sv, Run r)
        {
            Map.Remove(sv);
            if (r.Handler != null) CompositionTarget.Rendering -= r.Handler;
            if (r.UnloadedHandler != null)
            {
                try { sv.Unloaded -= r.UnloadedHandler; } catch { }
                r.UnloadedHandler = null;
            }
            try { r.Clock.Stop(); } catch { }
        }
    }

    // Появление контента с анимацией (учитывает reduce_motion через Theme.AnimationsEnabled).
    // Важно: часы анимации запускаем ПОСЛЕ того, как элемент вставлен в дерево и
    // разложен (UpdateLayout). Вёрстка страницы/вкладки занимает десятки миллисекунд,
    // и если стартовать до неё, первые кадры теряются и переход выглядит мгновенным.
    static class UiAnim
    {
        static bool On { get { return Theme.AnimationsEnabled; } }

        public static void Appear(UIElement el, double fromY, int fadeMs, int slideMs)
        {
            if (el == null) return;
            if (!On) { Reset(el); return; }

            el.Opacity = 0;
            var tt = new TranslateTransform(0, fromY);
            el.RenderTransform = tt;
            Layout(el);

            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeMs))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            el.BeginAnimation(UIElement.OpacityProperty, fade);
            if (fromY != 0)
            {
                var slide = new System.Windows.Media.Animation.DoubleAnimation(fromY, 0, TimeSpan.FromMilliseconds(slideMs))
                {
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                };
                tt.BeginAnimation(TranslateTransform.YProperty, slide);
            }
        }

        // Выпадающие списки и панели: короткое проявление со сдвигом сверху.
        public static void Popup(FrameworkElement el)
        {
            Appear(el, -5, 150, 190);
        }

        public static void Reset(UIElement el)
        {
            if (el == null) return;
            el.BeginAnimation(UIElement.OpacityProperty, null);
            el.Opacity = 1;
            el.RenderTransform = null;
        }

        static void Layout(UIElement el)
        {
            var fe = el as FrameworkElement;
            try { if (fe != null) fe.UpdateLayout(); } catch { }
        }
    }

    // Ряд кнопок: если все влезают в строку, первая растягивается на остаток,
    // остальные идут по своей ширине справа. Если не влезают — каждая кнопка
    // занимает отдельную строку во всю ширину. Раньше это была Grid со
    // Star-колонкой, и на узком окне (1000 px) первая кнопка сжималась ниже
    // своей подписи, а рамка обрезала текст («Скачать прок»).
    class ActionRow : Panel
    {
        public double Gap = 10;

        bool _stack;
        double[] _nat = new double[0];

        double NatSum()
        {
            double s = 0;
            for (int i = 0; i < _nat.Length; i++) s += _nat[i];
            return s + Gap * Math.Max(0, _nat.Length - 1);
        }

        double NatMax()
        {
            double m = 0;
            for (int i = 0; i < _nat.Length; i++) m = Math.Max(m, _nat[i]);
            return m;
        }

        double FirstWidth(double avail)
        {
            if (_nat.Length == 0) return 0;
            double rest = NatSum() - _nat[0];
            return Math.Max(_nat[0], avail - rest);
        }

        protected override Size MeasureOverride(Size avail)
        {
            int n = InternalChildren.Count;
            if (n == 0) return new Size(0, 0);
            if (_nat.Length != n) _nat = new double[n];

            var inf = new Size(double.PositiveInfinity, double.PositiveInfinity);
            double h = 0;
            for (int i = 0; i < n; i++)
            {
                var ch = InternalChildren[i];
                ch.Measure(inf);
                _nat[i] = ch.DesiredSize.Width;
                h = Math.Max(h, ch.DesiredSize.Height);
            }

            double need = NatSum();
            double w = avail.Width;
            _stack = !double.IsInfinity(w) && need > w + 0.5;

            if (!_stack)
            {
                double first = double.IsInfinity(w) ? _nat[0] : FirstWidth(w);
                InternalChildren[0].Measure(new Size(first, avail.Height));
                for (int i = 1; i < n; i++) InternalChildren[i].Measure(new Size(_nat[i], avail.Height));
                return new Size(double.IsInfinity(w) ? need : w, h);
            }

            double total = 0;
            for (int i = 0; i < n; i++)
            {
                var ch = InternalChildren[i];
                ch.Measure(new Size(w, double.PositiveInfinity));
                total += ch.DesiredSize.Height;
            }
            return new Size(Math.Max(w, NatMax()), total + Gap * (n - 1));
        }

        protected override Size ArrangeOverride(Size fin)
        {
            int n = InternalChildren.Count;
            if (n == 0) return fin;

            if (!_stack)
            {
                double x = 0;
                for (int i = 0; i < n; i++)
                {
                    double cw = i == 0 ? FirstWidth(fin.Width) : _nat[i];
                    InternalChildren[i].Arrange(new Rect(x, 0, cw, fin.Height));
                    x += cw + Gap;
                }
                return fin;
            }

            double y = 0;
            for (int i = 0; i < n; i++)
            {
                double ch = InternalChildren[i].DesiredSize.Height;
                InternalChildren[i].Arrange(new Rect(0, y, fin.Width, ch));
                y += ch + Gap;
            }
            return fin;
        }
    }

    // Сетка равных ячеек с переносом: колонок столько, сколько влезает при
    // минимальной ширине ячейки MinCell. Заменяет Grid с фиксированным числом
    // Star-колонок там, где на узком окне карточки сжимались и обрезали текст.
    class CellRow : Panel
    {
        public double MinCell = 160;
        public double Gap = 12;
        // Для карточек «3 + 1» уродливо, и там ряды выравнивают в 2 + 2. У ряда
        // кнопок такое удваивает ширину (кнопки по 400+ px), поэтому для него
        // правило отключают.
        public bool EvenRows = true;
        // >0 — жёстко задать число колонок и НЕ переносить ряд, даже если ячейка
        // выходит уже содержимого. Для рядов кнопок: кнопка всегда в одной линии,
        // а подпись при нехватке места переносится внутри кнопки.
        public int ForceCols;
        // Ниже этой ширины ячейки ForceCols уступает: при абсурдно узком окне
        // лучше перенести кнопки, чем превратить подпись в столбик букв.
        public double MinForceCell = 96;
        // Число колонок, по которым считается ШИРИНА ячейки. Нужно, когда в одном
        // блоке несколько рядов кнопок: ряд из трёх кнопок делит ширину на три и
        // оказывается на треть шире ряда из четырёх — кнопки выглядят разбросанными
        // по размеру. Ряды одного набора получают общий UniformCols (Ctl.ButtonRows),
        // поэтому ячейки во всех рядах равны, а короткий ряд просто не доходит до
        // правого края на одну колонку.
        public int UniformCols;
        // Элементы остаются КОМПАКТНЫМИ (по ширине содержимого), но распределены
        // по всей ширине ряда с равными зазорами: первый прижат к левому краю,
        // последний к правому. Нужен такой режим для полос табов: равные колонки
        // растягивают пилюлю на весь остаток строки, и иконка с подписью
        // разъезжаются, а сгруппированная по содержимому полоса центрировалась и
        // у её краёв оставалось пустое поле. Не влезает — ряд откатывается к
        // равным колонкам (ForceCols), и подпись переносится внутри элемента.
        public bool Justify;

        int _cols = 1;
        double[] _nat;          // естественная ширина элементов без ограничения
        // Подписи кнопок ряда по тем же индексам, что и Children (null — не кнопка).
        // Нужны, чтобы померить естественную ширину: с включённым TextWrapping и
        // MaxWidth TextBlock отдаёт ширину ПЕРЕНЕСЁННОЙ строки, и ряд считал
        // элемент уже, чем нужно.
        public System.Collections.Generic.List<TextBlock> Labels;

        // Один замер на весь проход разметки: естественные ширины нужны и в
        // Measure, и в Arrange, а решение между ними обязано совпасть.
        void MeasureNatural()
        {
            int n = InternalChildren.Count;
            if (_nat == null || _nat.Length != n) _nat = new double[n];
            for (int i = 0; i < n; i++)
            {
                var ch = InternalChildren[i];
                TextBlock lab = (Labels != null && i < Labels.Count) ? Labels[i] : null;
                TextWrapping prevWrap = TextWrapping.NoWrap;
                double prevMax = double.PositiveInfinity;
                bool restored = false;
                if (lab != null)
                {
                    prevWrap = lab.TextWrapping;
                    prevMax = lab.MaxWidth;
                    lab.TextWrapping = TextWrapping.NoWrap;
                    lab.MaxWidth = double.PositiveInfinity;
                    restored = true;
                }
                ch.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                // +1 на округление метрик: иначе последняя буква влезает не всегда.
                _nat[i] = ch.DesiredSize.Width + 1;
                if (restored)
                {
                    lab.TextWrapping = prevWrap;
                    lab.MaxWidth = prevMax;
                }
            }
        }

        // Хватает ли ширины на компактный ряд (Gap здесь — минимальный зазор).
        bool NaturalFits(double avail)
        {
            if (!Justify) return false;
            if (double.IsInfinity(avail) || avail <= 0) return false;
            if (_nat == null || _nat.Length != InternalChildren.Count) return false;
            double total = Gap * (InternalChildren.Count - 1);
            for (int i = 0; i < _nat.Length; i++) total += _nat[i];
            return total <= avail + 0.5;
        }

        int ColsFor(double avail)
        {
            int n = InternalChildren.Count;
            if (n <= 1) return 1;
            if (ForceCols > 0)
            {
                int c = ForceCols > n ? n : ForceCols;
                if (!double.IsInfinity(avail) && avail > 0)
                {
                    int fit = (int)Math.Floor((avail + Gap) / (MinForceCell + Gap));
                    if (fit < 1) fit = 1;
                    if (fit < c) c = fit;
                }
                return c;
            }
            if (double.IsInfinity(avail) || avail <= 0) return n;
            int cols = (int)Math.Floor((avail + Gap) / (MinCell + Gap));
            if (cols < 1) cols = 1;
            if (cols > n) cols = n;
            // Четыре карточки в три колонки дают неровное «3 + 1»:
            // два ряда по два выглядят аккуратнее.
            if (EvenRows && cols > 1 && n % cols != 0 && n % 2 == 0 && cols > n / 2) cols = n / 2;
            return cols;
        }

        double CellWidth(double avail)
        {
            if (double.IsInfinity(avail) || avail <= 0) return MinCell;
            int denom = _cols;
            // Групповая ширина действует, только пока ряд ещё в одну линию. Если он
            // всё же перенёсся на узком окне, делить на колонки длинного ряда —
            // значит оставить половину строки пустой.
            if (UniformCols > denom && (ForceCols <= 0 || _cols >= ForceCols)) denom = UniformCols;
            return Math.Max(1, (avail - Gap * (denom - 1)) / denom);
        }

        protected override Size MeasureOverride(Size avail)
        {
            int n = InternalChildren.Count;
            if (n == 0) return new Size(0, 0);

            if (Justify)
            {
                MeasureNatural();
                if (NaturalFits(avail.Width))
                {
                    double h = 0, natW = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var ch = InternalChildren[i];
                        ch.Measure(new Size(_nat[i], double.PositiveInfinity));
                        h = Math.Max(h, ch.DesiredSize.Height);
                        natW += _nat[i] + (i > 0 ? Gap : 0);
                    }
                    return new Size(double.IsInfinity(avail.Width) ? natW : avail.Width, h);
                }
            }

            _cols = ColsFor(avail.Width);
            double cw = CellWidth(avail.Width);

            double y = 0, rowH = 0;
            for (int i = 0; i < n; i++)
            {
                var ch = InternalChildren[i];
                ch.Measure(new Size(cw, double.PositiveInfinity));
                rowH = Math.Max(rowH, ch.DesiredSize.Height);
                bool last = i == n - 1;
                if ((i + 1) % _cols == 0 || last)
                {
                    y += rowH + (last ? 0 : Gap);
                    rowH = 0;
                }
            }
            double w = double.IsInfinity(avail.Width) ? cw * _cols + Gap * (_cols - 1) : avail.Width;
            return new Size(w, y);
        }

        protected override Size ArrangeOverride(Size fin)
        {
            int n = InternalChildren.Count;
            if (n == 0) return fin;
            // Ширины детей берутся из Measure: вызвать Measure здесь — значит попасть
            // в цикл measure→arrange→measure (приложение висело ровно на этом).
            if (NaturalFits(fin.Width))
            {
                double hh = 0;
                double total = 0;
                for (int i = 0; i < n; i++)
                {
                    var ch = InternalChildren[i];
                    hh = Math.Max(hh, ch.DesiredSize.Height);
                    total += _nat[i];
                }
                // Зазор делится на (n-1) ровно: крайний правый элемент заканчивается
                // в пиксель от правого края ряда, пустого поля у краёв нет.
                double gap = n > 1 ? (fin.Width - total) / (n - 1) : 0;
                double x = 0;
                for (int i = 0; i < n; i++)
                {
                    var ch = InternalChildren[i];
                    ch.Arrange(new Rect(x, 0, _nat[i], hh));
                    x += _nat[i] + gap;
                }
                return fin;
            }

            _cols = ColsFor(fin.Width);
            double cw = CellWidth(fin.Width);

            double y = 0, rowH = 0;
            for (int i = 0; i < n; i++)
            {
                var ch = InternalChildren[i];
                rowH = Math.Max(rowH, ch.DesiredSize.Height);
                if ((i + 1) % _cols == 0 || i == n - 1)
                {
                    int start = i - (i % _cols);
                    for (int j = start; j <= i; j++)
                        InternalChildren[j].Arrange(new Rect((j - start) * (cw + Gap), y, cw, rowH));
                    y += rowH + Gap;
                    rowH = 0;
                }
            }
            return fin;
        }
    }

    // Дорожка индикатора загрузки: заполнение задаётся долей 0..1, а не
    // фиксированной шириной, поэтому полоса тянется на всю ширину карточки и
    // остаётся корректной при изменении размера окна. Fraction помечен
    // AffectsArrange — его можно анимировать, как обычную ширину.
    sealed class ProgressTrack : Panel
    {
        public static readonly DependencyProperty FractionProperty = DependencyProperty.Register(
            "Fraction", typeof(double), typeof(ProgressTrack),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsArrange));

        public double Fraction
        {
            get { return (double)GetValue(FractionProperty); }
            set { SetValue(FractionProperty, value); }
        }

        // На 0 % пустая дорожка не показывает, что загрузка началась: оставляем
        // короткий видимый кусочек.
        public double MinFill = 12;

        protected override Size MeasureOverride(Size avail)
        {
            double w = double.IsInfinity(avail.Width) ? 240 : avail.Width;
            double h = double.IsInfinity(avail.Height) ? 6 : avail.Height;
            var cell = new Size(w, h);
            for (int i = 0; i < InternalChildren.Count; i++)
                InternalChildren[i].Measure(cell);
            return new Size(w, h);
        }

        protected override Size ArrangeOverride(Size fin)
        {
            double f = Fraction;
            if (double.IsNaN(f) || f < 0) f = 0;
            if (f > 1) f = 1;
            double w = Math.Max(Math.Min(MinFill, fin.Width), fin.Width * f);
            for (int i = 0; i < InternalChildren.Count; i++)
                InternalChildren[i].Arrange(new Rect(0, 0, w, fin.Height));
            return fin;
        }
    }
}
