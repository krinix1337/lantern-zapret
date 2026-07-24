using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZapretStudio
{
    // Базовый класс экрана: заголовок + прокручиваемое тело.
    abstract class Page : UserControl
    {
        protected StackPanel Body;
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

            var head = new StackPanel { Margin = new Thickness(28, 24, 28, 8) };
            head.Children.Add(UI.T(Title, Theme.FsH1, Theme.BrText, FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(Subtitle))
                head.Children.Add(new TextBlock { Text = Subtitle, Foreground = Theme.BrMuted,
                    FontSize = Theme.FsBody, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap });
            Grid.SetRow(head, 0);
            root.Children.Add(head);

            Body = new StackPanel { Margin = new Thickness(28, 12, 28, 28) };
            var sv = new ScrollViewer
            {
                Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0)
            };
            Grid.SetRow(sv, 1);
            root.Children.Add(sv);
            Content = root;
        }

        // ---- строительные блоки ----
        protected static TextBlock SectionLabel(string t)
        {
            return new TextBlock { Text = t.ToUpperInvariant(), Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny, FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(2, 18, 0, 8) };
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
                    Margin = new Thickness(16, 0, 0, 0) };
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
                if (i < rows.Length - 1) sp.Children.Add(new Border { Height = 10 });
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

            Color c = tint == Sev.Ok ? Theme.Ok : tint == Sev.Warn ? Theme.Warn
                    : tint == Sev.Err ? Theme.Err : Theme.AccentMain;
            Brush bg = tint == Sev.Neutral ? Theme.BrSurface : Theme.Alpha(c, 16);
            var card = UI.Card(g, new Thickness(16, 12, 16, 12), Theme.R10, bg);
            if (tint != Sev.Neutral) card.BorderBrush = Theme.Alpha(c, 70);
            return card;
        }

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
            cb.Resources = ComboResources();
            cb.Template = ComboTemplate();
            return cb;
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
            var hov = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsMouseOverProperty, Value = true };
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
            popup.SetValue(System.Windows.Controls.Primitives.Popup.PopupAnimationProperty, System.Windows.Controls.Primitives.PopupAnimation.Fade);
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
            scroll.SetValue(ScrollViewer.MaxHeightProperty, 280.0);
            scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
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
            bd.SetValue(Border.BorderBrushProperty, Theme.Frozen(Theme.Stroke));
            bd.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            bd.SetValue(Border.CornerRadiusProperty, Theme.R10);
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
            bd.AppendChild(grid);
            t.VisualTree = bd;
            var hov = new Trigger { Property = System.Windows.Controls.Primitives.ButtonBase.IsMouseOverProperty, Value = true };
            hov.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Frozen(Theme.AccentMain), "tb"));
            t.Triggers.Add(hov);
            return t;
        }

        static UIElement space10() { return new Border { Height = 10 }; }
    }
}
