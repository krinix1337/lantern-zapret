using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ZapretStudio
{
    // Тема оформления: тёмная / светлая / AMOLED.
    enum ThemeMode { Dark, Light, Amoled }

    // Централизованная дизайн-система. Кисти НЕ заморожены — при смене темы
    // мутируем их .Color, и все контролы, держащие ту же ссылку, перерисовываются.
    static class Theme
    {
        public static ThemeMode Mode = ThemeMode.Dark;

        public static Color BgDeep, BgBase, Surface, SurfaceAlt, SurfaceHi, Stroke, StrokeSoft;
        public static Color Text, TextMuted, TextFaint;
        public static Color AccentMain, AccentHi, AccentDim;
        public static Color Ok, OkDim, Warn, WarnDim, Err, ErrDim;

        public static readonly SolidColorBrush BrBgDeep     = new SolidColorBrush();
        public static readonly SolidColorBrush BrBgBase     = new SolidColorBrush();
        public static readonly SolidColorBrush BrSurface    = new SolidColorBrush();
        public static readonly SolidColorBrush BrSurfaceAlt = new SolidColorBrush();
        public static readonly SolidColorBrush BrSurfaceHi  = new SolidColorBrush();
        public static readonly SolidColorBrush BrStroke     = new SolidColorBrush();
        public static readonly SolidColorBrush BrStrokeSoft = new SolidColorBrush();
        public static readonly SolidColorBrush BrText       = new SolidColorBrush();
        public static readonly SolidColorBrush BrMuted      = new SolidColorBrush();
        public static readonly SolidColorBrush BrFaint      = new SolidColorBrush();
        public static readonly SolidColorBrush BrAccent     = new SolidColorBrush();
        public static readonly SolidColorBrush BrAccentHi   = new SolidColorBrush();
        public static readonly SolidColorBrush BrOk         = new SolidColorBrush();
        public static readonly SolidColorBrush BrWarn       = new SolidColorBrush();
        public static readonly SolidColorBrush BrErr        = new SolidColorBrush();
        public static readonly SolidColorBrush BrOnAccent   = new SolidColorBrush(Colors.White);

        public static event Action ThemeChanged;

        static Theme() { Apply(ThemeMode.Dark); }

        static void SetDarkPalette()
        {
            BgDeep     = Rgb(0x14, 0x15, 0x18);
            BgBase     = Rgb(0x18, 0x1A, 0x1F);
            Surface    = Rgb(0x1F, 0x22, 0x28);
            SurfaceAlt = Rgb(0x25, 0x29, 0x30);
            SurfaceHi  = Rgb(0x2D, 0x31, 0x3A);
            Stroke     = Rgb(0x33, 0x38, 0x42);
            StrokeSoft = Rgb(0x2A, 0x2E, 0x36);
            Text       = Rgb(0xEC, 0xEE, 0xF2);
            TextMuted  = Rgb(0x9A, 0xA0, 0xAB);
            TextFaint  = Rgb(0x6C, 0x72, 0x7D);
            AccentMain = Rgb(0x6C, 0x78, 0xF0);
            AccentHi   = Rgb(0x84, 0x8F, 0xFF);
            AccentDim  = Rgb(0x3A, 0x40, 0x7A);
            Ok  = Rgb(0x4F, 0xB0, 0x6A); OkDim  = Rgb(0x24, 0x3A, 0x2C);
            Warn = Rgb(0xE0, 0xA8, 0x45); WarnDim = Rgb(0x3C, 0x30, 0x18);
            Err = Rgb(0xDB, 0x5E, 0x5E); ErrDim = Rgb(0x3C, 0x22, 0x22);
        }

        static void SetLightPalette()
        {
            BgDeep     = Rgb(0xEC, 0xEE, 0xF3);
            BgBase     = Rgb(0xF5, 0xF6, 0xFA);
            Surface    = Rgb(0xFF, 0xFF, 0xFF);
            SurfaceAlt = Rgb(0xEF, 0xF1, 0xF6);
            SurfaceHi  = Rgb(0xE3, 0xE7, 0xEF);
            Stroke     = Rgb(0xD3, 0xD8, 0xE2);
            StrokeSoft = Rgb(0xE2, 0xE6, 0xEC);
            Text       = Rgb(0x1B, 0x1F, 0x27);
            TextMuted  = Rgb(0x59, 0x60, 0x6D);
            TextFaint  = Rgb(0x8A, 0x92, 0x9F);
            AccentMain = Rgb(0x53, 0x5F, 0xE0);
            AccentHi   = Rgb(0x43, 0x4F, 0xD6);
            AccentDim  = Rgb(0xC7, 0xCC, 0xF6);
            Ok  = Rgb(0x2F, 0x8F, 0x4E); OkDim  = Rgb(0xDD, 0xF0, 0xE2);
            Warn = Rgb(0xB0, 0x7C, 0x12); WarnDim = Rgb(0xF7, 0xEC, 0xD2);
            Err = Rgb(0xC6, 0x45, 0x45); ErrDim = Rgb(0xF7, 0xDE, 0xDE);
        }

        static void SetAmoledPalette()
        {
            BgDeep     = Rgb(0x00, 0x00, 0x00);
            BgBase     = Rgb(0x00, 0x00, 0x00);
            Surface    = Rgb(0x12, 0x12, 0x14);
            SurfaceAlt = Rgb(0x1A, 0x1A, 0x1E);
            SurfaceHi  = Rgb(0x24, 0x24, 0x2A);
            Stroke     = Rgb(0x2A, 0x2A, 0x32);
            StrokeSoft = Rgb(0x20, 0x20, 0x28);
            Text       = Rgb(0xEC, 0xEE, 0xF2);
            TextMuted  = Rgb(0x9A, 0xA0, 0xAB);
            TextFaint  = Rgb(0x6C, 0x72, 0x7D);
            AccentMain = Rgb(0x6C, 0x78, 0xF0);
            AccentHi   = Rgb(0x84, 0x8F, 0xFF);
            AccentDim  = Rgb(0x2A, 0x30, 0x60);
            Ok  = Rgb(0x4F, 0xB0, 0x6A); OkDim  = Rgb(0x14, 0x28, 0x1C);
            Warn = Rgb(0xE0, 0xA8, 0x45); WarnDim = Rgb(0x2C, 0x22, 0x10);
            Err = Rgb(0xDB, 0x5E, 0x5E); ErrDim = Rgb(0x2C, 0x18, 0x18);
        }

        public static void Apply(ThemeMode mode)
        {
            Mode = mode;
            if (mode == ThemeMode.Light) SetLightPalette();
            else if (mode == ThemeMode.Amoled) SetAmoledPalette();
            else SetDarkPalette();
            BrBgDeep.Color = BgDeep;   BrBgBase.Color = BgBase;
            BrSurface.Color = Surface; BrSurfaceAlt.Color = SurfaceAlt; BrSurfaceHi.Color = SurfaceHi;
            BrStroke.Color = Stroke;   BrStrokeSoft.Color = StrokeSoft;
            BrText.Color = Text;       BrMuted.Color = TextMuted; BrFaint.Color = TextFaint;
            BrAccent.Color = AccentMain; BrAccentHi.Color = AccentHi;
            BrOk.Color = Ok; BrWarn.Color = Warn; BrErr.Color = Err;
            BrOnAccent.Color = Colors.White;
            InstallScrollBarStyle();
            if (ThemeChanged != null) ThemeChanged();
        }

        // Тёмная/светлая полоса прокрутки в стиле приложения. Переустанавливается при
        // каждой смене темы (цвета «зашиваются» в XAML-шаблон как текущий снимок палитры).
        // Через XamlReader, т.к. Track не реализует IAddChild и его нельзя собрать
        // из FrameworkElementFactory в чистом коде.
        public static void InstallScrollBarStyle()
        {
            var appRes = (System.Windows.Application.Current != null) ? System.Windows.Application.Current.Resources : null;
            if (appRes == null) return;
            try
            {
                string trackBg = Hex(BgDeep);
                string thumb   = Hex(SurfaceHi);
                string thumbHv = Hex(Blend(SurfaceHi, TextMuted, 0.35));

                string xaml =
"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
"<Style x:Key='{x:Type ScrollBar}' TargetType='ScrollBar'>" +
"<Setter Property='Background' Value='" + trackBg + "'/>" +
"<Setter Property='Width' Value='11'/>" +
"<Setter Property='Template'><Setter.Value>" +
"<ControlTemplate TargetType='ScrollBar'>" +
"<Grid Background='{TemplateBinding Background}'>" +
"<Track Name='PART_Track' IsDirectionReversed='True'>" +
"<Track.Thumb><Thumb><Thumb.Template>" +
"<ControlTemplate TargetType='Thumb'>" +
"<Border x:Name='tb' CornerRadius='4' Margin='2' Background='" + thumb + "'/>" +
"<ControlTemplate.Triggers>" +
"<Trigger Property='IsMouseOver' Value='True'>" +
"<Setter TargetName='tb' Property='Background' Value='" + thumbHv + "'/></Trigger>" +
"</ControlTemplate.Triggers></ControlTemplate>" +
"</Thumb.Template></Thumb></Track.Thumb>" +
"<Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False' Background='Transparent' BorderThickness='0'/></Track.IncreaseRepeatButton>" +
"<Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False' Background='Transparent' BorderThickness='0'/></Track.DecreaseRepeatButton>" +
"</Track></Grid>" +
"<ControlTemplate.Triggers>" +
"<Trigger Property='Orientation' Value='Horizontal'>" +
"<Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='11'/></Trigger>" +
"</ControlTemplate.Triggers>" +
"</ControlTemplate>" +
"</Setter.Value></Setter></Style>" +
"</ResourceDictionary>";

                var rd = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(xaml);
                appRes[typeof(System.Windows.Controls.Primitives.ScrollBar)] =
                    rd[typeof(System.Windows.Controls.Primitives.ScrollBar)];
            }
            catch { /* прокрутка останется системной — не критично */ }
        }

        static string Hex(Color c)
        {
            return "#" + c.A.ToString("X2") + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
        }

        static Color Blend(Color a, Color b, double t)
        {
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        public static ThemeMode NextMode()
        {
            if (Mode == ThemeMode.Dark) return ThemeMode.Amoled;
            if (Mode == ThemeMode.Amoled) return ThemeMode.Light;
            return ThemeMode.Dark;
        }

        public static void ToggleMode()
        {
            Apply(NextMode());
        }

        public static readonly FontFamily UiFont  = new FontFamily("Segoe UI, Inter, sans-serif");
        public static readonly FontFamily MonoFont = new FontFamily("JetBrains Mono, Cascadia Mono, Consolas, monospace");

        public const double FsDisplay = 30;
        public const double FsH1 = 22;
        public const double FsH2 = 16;
        public const double FsBody = 13;
        public const double FsSmall = 12;
        public const double FsTiny = 11;

        public static readonly CornerRadius R6  = new CornerRadius(6);
        public static readonly CornerRadius R8  = new CornerRadius(8);
        public static readonly CornerRadius R10 = new CornerRadius(10);
        public static readonly CornerRadius R12 = new CornerRadius(12);
        public static readonly CornerRadius R14 = new CornerRadius(14);
        public static readonly CornerRadius Rpill = new CornerRadius(999);

        // Анимации: только пользовательская настройка «Уменьшить анимацию».
        public static bool AnimationsEnabled
        {
            get { return !Core.GetBool("reduce_motion", false); }
        }

        static Color Rgb(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }
        public static SolidColorBrush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }
        public static SolidColorBrush Alpha(Color c, byte a) { var b = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); b.Freeze(); return b; }
    }

    // Векторные иконки (24x24 path data, штриховые, минималистичные).
    static class Icons
    {
        public const string Home     = "M4 11 L12 4 L20 11 M6 10 V19 H18 V10";
        public const string Grid     = "M4 4 H10 V10 H4 Z M14 4 H20 V10 H14 Z M4 14 H10 V20 H4 Z M14 14 H20 V20 H14 Z";
        public const string Pulse    = "M3 12 H7 L9 6 L13 18 L15 12 H21";
        public const string Gear     = "M4 21 V14 M4 10 V3 M12 21 V12 M12 8 V3 M20 21 V16 M20 12 V3 M1 14 H7 M9 8 H15 M17 16 H23";
        public const string Server   = "M4 5 H20 V10 H4 Z M4 14 H20 V19 H4 Z M7 7.5 H7.01 M7 16.5 H7.01";
        public const string Filter   = "M4 5 H20 L14 12 V19 L10 21 V12 Z";
        public const string List     = "M8 6 H20 M8 12 H20 M8 18 H20 M4 6 H4.01 M4 12 H4.01 M4 18 H4.01";
        public const string Info     = "M12 3 A9 9 0 1 0 12 21 A9 9 0 1 0 12 3 M12 8 H12.01 M11 12 H12 V16 H13";
        public const string Play     = "M7 4 V20 L19 12 Z";
        public const string Stop     = "M6 6 H18 V18 H6 Z";
        public const string Restart  = "M4 12 A8 8 0 1 0 6 6 M6 6 V10 M6 6 H10";
        public const string Refresh  = "M20 12 A8 8 0 1 1 18 6 M18 6 V2 M18 6 H14";
        public const string Folder   = "M4 6 H10 L12 8 H20 V18 H4 Z";
        public const string Github   = "M12 2 A10 10 0 0 0 9 21.5 C9 20 9 18.5 9 18 C6.5 18.5 6 16.5 6 16.5 C5.5 15 4.5 14.5 4.5 14.5 C3.5 14 4.5 14 4.5 14 C6 14 6.5 15.5 6.5 15.5 C7.5 17 9 16.5 9.5 16.5 C9.5 15.5 10 15 10.5 14.5 C7.5 14 5.5 13 5.5 9.5 C5.5 8 6 7 6.5 6.5 C6.5 6 6 5 6.5 3.5 C6.5 3.5 8 3.5 9.5 5 C10.5 4.5 13.5 4.5 14.5 5 C16 3.5 17.5 3.5 17.5 3.5 C18 5 17.5 6 17.5 6.5 C18 7 18.5 8 18.5 9.5 C18.5 13 16.5 14 13.5 14.5 C14 15 14.5 16 14.5 17.5 C14.5 18.5 14.5 20 14.5 21.5 A10 10 0 0 0 12 2";
        public const string Check    = "M5 12 L10 17 L19 7";
        public const string Cross    = "M6 6 L18 18 M18 6 L6 18";
        public const string Warn     = "M12 3 L22 20 H2 Z M12 9 V14 M12 17 H12.01";
        public const string Search   = "M11 4 A7 7 0 1 0 11 18 A7 7 0 1 0 11 4 M16 16 L21 21";
        public const string Star     = "M12 3 L14.5 9 L21 9.5 L16 14 L17.5 20.5 L12 17 L6.5 20.5 L8 14 L3 9.5 L9.5 9 Z";
        public const string Copy     = "M8 8 H18 V20 H8 Z M6 16 H4 V4 H14 V6";
        public const string Save     = "M5 4 H16 L20 8 V20 H5 Z M8 4 V9 H15 M8 15 H16";
        public const string External = "M14 4 H20 V10 M20 4 L11 13 M18 14 V19 H5 V6 H10";
        public const string Shield   = "M12 3 L20 6 V11 C20 16 16 20 12 21 C8 20 4 16 4 11 V6 Z";
        public const string Dot      = "M12 8 A4 4 0 1 0 12 16 A4 4 0 1 0 12 8";
        public const string Down     = "M6 9 L12 15 L18 9";
        public const string Menu     = "M5 7 H19 M5 12 H19 M5 17 H19";
        public const string Sun      = "M12 7 A5 5 0 1 0 12 17 A5 5 0 1 0 12 7 M12 1 V3 M12 21 V23 M4.2 4.2 L5.6 5.6 M18.4 18.4 L19.8 19.8 M1 12 H3 M21 12 H23 M4.2 19.8 L5.6 18.4 M18.4 5.6 L19.8 4.2";
        public const string Moon     = "M20 14 A9 9 0 1 1 10 4 A7 7 0 0 0 20 14 Z";
        public const string Globe    = "M12 3 A9 9 0 1 0 12 21 A9 9 0 1 0 12 3 M3 12 H21 M12 3 C15 6 15 18 12 21 C9 18 9 6 12 3";
        public const string Game     = "M8 12 H12 M10 10 V14 M16 11 H16.01 M18 13 H18.01 M7 7 H17 A4 4 0 0 1 21 11 L20 17 A3 3 0 0 1 15 18 L14 16 H10 L9 18 A3 3 0 0 1 4 17 L3 11 A4 4 0 0 1 7 7 Z";
        public const string Plug     = "M9 3 V8 M15 3 V8 M7 8 H17 V12 A5 5 0 0 1 7 12 Z M12 17 V21";
        public const string Download = "M12 3 V15 M7 10 L12 15 L17 10 M5 19 H19";
        public const string Telegram = "M21 5 L2 12 L9 14 L11 20 L14 16 L18 19 Z M9 14 L18 7";
        public const string Link     = "M9 12 H15 M10 8 H7 A4 4 0 0 0 7 16 H10 M14 8 H17 A4 4 0 0 1 17 16 H14";
        public const string Bolt     = "M13 2 L4 14 H11 L10 22 L20 9 H13 Z";
        public const string Lantern  = "M9 4 A3 2 0 0 1 15 4 M7 6 H17 M8 6 V17 A2 2 0 0 0 10 19 H14 A2 2 0 0 0 16 17 V6 M6 19 H18 M12 9 A2 3 0 0 0 12 15 A2 3 0 0 0 12 9";
    }

    static class UI
    {
        public static System.Windows.Shapes.Path Icon(string data, double size, Brush stroke, double thickness)
        {
            return new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Stroke = stroke,
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = size, Height = size,
                SnapsToDevicePixels = false
            };
        }
        public static System.Windows.Shapes.Path Icon(string data, double size, Brush stroke)
        { return Icon(data, size, stroke, 1.7); }

        public static TextBlock T(string text, double size, Brush fg, FontWeight? weight = null)
        {
            var t = new TextBlock
            {
                Text = text, FontSize = size, Foreground = fg,
                FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.None
            };
            if (weight.HasValue) t.FontWeight = weight.Value;
            return t;
        }
        public static TextBlock Mono(string text, double size, Brush fg)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = fg,
                FontFamily = Theme.MonoFont, TextWrapping = TextWrapping.Wrap };
        }

        public static Border Card(UIElement child, Thickness? pad = null, CornerRadius? radius = null, Brush bg = null)
        {
            return new Border
            {
                Background = bg ?? Theme.BrSurface,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = radius ?? Theme.R12,
                Padding = pad ?? new Thickness(18),
                Child = child
            };
        }

    }

    public enum Sev { Neutral, Ok, Warn, Err, Info, Progress }

    static class UI2
    {
        public static Brush SevBrush(Sev s)
        {
            switch (s)
            {
                case Sev.Ok: return Theme.BrOk;
                case Sev.Warn: return Theme.BrWarn;
                case Sev.Err: return Theme.BrErr;
                case Sev.Info: return Theme.BrAccent;
                case Sev.Progress: return Theme.BrAccent;
                default: return Theme.BrMuted;
            }
        }
        public static Color SevColor(Sev s)
        {
            switch (s)
            {
                case Sev.Ok: return Theme.Ok;
                case Sev.Warn: return Theme.Warn;
                case Sev.Err: return Theme.Err;
                case Sev.Info: return Theme.AccentMain;
                case Sev.Progress: return Theme.AccentMain;
                default: return Theme.TextMuted;
            }
        }
        public static string SevIcon(Sev s)
        {
            switch (s)
            {
                case Sev.Ok: return Icons.Check;
                case Sev.Warn: return Icons.Warn;
                case Sev.Err: return Icons.Cross;
                default: return Icons.Dot;
            }
        }
    }

    static class Ctl
    {
        // kind: 0=primary,1=ghost,2=danger,3=subtle
        public static Button Button(string text, string iconData, int kind)
        {
            Brush bg, fg, brd;
            switch (kind)
            {
                case 0: bg = Theme.BrAccent; fg = Theme.BrOnAccent; brd = Theme.BrAccent; break;
                case 2: bg = Theme.Alpha(Theme.Err, 30); fg = Theme.BrErr; brd = Theme.Alpha(Theme.Err, 110); break;
                case 3: bg = Theme.BrSurfaceAlt; fg = Theme.BrText; brd = Theme.BrStroke; break;
                default: bg = Theme.Alpha(Theme.Text, 8); fg = Theme.BrText; brd = Theme.BrStroke; break;
            }

            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            if (iconData != null)
            {
                var ic = UI.Icon(iconData, 16, fg, 1.8);
                ic.VerticalAlignment = VerticalAlignment.Center;
                ic.Margin = new Thickness(0, 0, text != null ? 8 : 0, 0);
                sp.Children.Add(ic);
            }
            if (text != null)
                sp.Children.Add(new TextBlock { Text = text, Foreground = fg, FontSize = Theme.FsBody,
                    FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

            var border = new Border
            {
                Background = bg, BorderBrush = brd, BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10, Padding = new Thickness(14, 9, 14, 9), Child = sp
            };

            var b = new Button { Content = border, Cursor = System.Windows.Input.Cursors.Hand, Focusable = true };
            StripChrome(b);
            b.Tag = new object[] { border, bg, kind };
            b.MouseEnter += (s, e) => { border.Background = Hover(bg, kind); };
            b.MouseLeave += (s, e) => { border.Background = bg; };
            AutomationSetName(b, text ?? Loc.T("common.button"));
            return b;
        }

        static Brush Hover(Brush baseBg, int kind)
        {
            switch (kind)
            {
                case 0: return Theme.BrAccentHi;
                case 2: return Theme.Alpha(Theme.Err, 55);
                case 3: return Theme.BrSurfaceHi;
                default: return Theme.Alpha(Theme.Text, 20);
            }
        }

        public static void StripChrome(Button b)
        {
            b.Background = Brushes.Transparent;
            b.BorderThickness = new Thickness(0);
            b.Padding = new Thickness(0);
            b.Template = ButtonTemplate();
            b.FocusVisualStyle = FocusStyle();
        }

        static System.Windows.Controls.ControlTemplate ButtonTemplate()
        {
            var t = new System.Windows.Controls.ControlTemplate(typeof(Button));
            var f = new FrameworkElementFactory(typeof(ContentPresenter));
            f.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            f.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            t.VisualTree = f;
            return t;
        }

        public static Style FocusStyle()
        {
            var st = new Style();
            var ct = new System.Windows.Controls.ControlTemplate();
            var b = new FrameworkElementFactory(typeof(Border));
            // ВАЖНО: в шаблон нельзя класть мутируемые кисти Theme.Br* — WPF замораживает
            // ресурсы шаблона, и последующая смена темы падает с "read-only state".
            // Берём замороженную копию текущего акцентного цвета.
            b.SetValue(Border.BorderBrushProperty, Theme.Frozen(Theme.AccentHi));
            b.SetValue(Border.BorderThicknessProperty, new Thickness(2));
            b.SetValue(Border.CornerRadiusProperty, Theme.R10);
            b.SetValue(Border.MarginProperty, new Thickness(-3));
            b.SetValue(Border.SnapsToDevicePixelsProperty, true);
            ct.VisualTree = b;
            st.Setters.Add(new Setter(Control.TemplateProperty, ct));
            return st;
        }

        public static void AutomationSetName(DependencyObject el, string name)
        {
            System.Windows.Automation.AutomationProperties.SetName(el, name);
        }

        // CheckBox в стиле темы (по умолчанию WPF рисует системный белый квадрат).
        public static CheckBox Check(string autoName)
        {
            var cb = new CheckBox { Foreground = Theme.BrText, Template = CheckTemplate() };
            if (!string.IsNullOrEmpty(autoName)) AutomationSetName(cb, autoName);
            return cb;
        }

        static System.Windows.Controls.ControlTemplate CheckTemplate()
        {
            var t = new System.Windows.Controls.ControlTemplate(typeof(CheckBox));
            var box = new FrameworkElementFactory(typeof(Border), "box");
            box.SetValue(Border.WidthProperty, 18.0);
            box.SetValue(Border.HeightProperty, 18.0);
            box.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            box.SetValue(Border.BorderThicknessProperty, new Thickness(1.6));
            box.SetValue(Border.BorderBrushProperty, Theme.Frozen(Theme.TextFaint));
            box.SetValue(Border.BackgroundProperty, Theme.Frozen(Theme.SurfaceAlt));
            box.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var check = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "tick");
            check.SetValue(System.Windows.Shapes.Path.DataProperty,
                Geometry.Parse("M4 9 L7.5 12.5 L14 5.5"));
            check.SetValue(System.Windows.Shapes.Path.StrokeProperty, Theme.Frozen(Colors.White));
            check.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty, 2.0);
            check.SetValue(System.Windows.Shapes.Path.StrokeStartLineCapProperty, PenLineCap.Round);
            check.SetValue(System.Windows.Shapes.Path.StrokeEndLineCapProperty, PenLineCap.Round);
            check.SetValue(System.Windows.Shapes.Path.StrokeLineJoinProperty, PenLineJoin.Round);
            check.SetValue(System.Windows.Shapes.Path.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            check.SetValue(System.Windows.Shapes.Path.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetValue(System.Windows.Shapes.Path.OpacityProperty, 0.0);
            box.AppendChild(check);

            t.VisualTree = box;

            var onChecked = new Trigger { Property = CheckBox.IsCheckedProperty, Value = true };
            onChecked.Setters.Add(new Setter(Border.BackgroundProperty, Theme.Frozen(Theme.AccentMain), "box"));
            onChecked.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Frozen(Theme.AccentMain), "box"));
            onChecked.Setters.Add(new Setter(System.Windows.Shapes.Path.OpacityProperty, 1.0, "tick"));
            t.Triggers.Add(onChecked);

            var onHover = new Trigger { Property = CheckBox.IsMouseOverProperty, Value = true };
            onHover.Setters.Add(new Setter(Border.BorderBrushProperty, Theme.Frozen(Theme.AccentHi), "box"));
            t.Triggers.Add(onHover);

            return t;
        }
    }

    static class Pill
    {
        public static Border Make(Sev sev, string text)
        {
            var color = UI2.SevColor(sev);
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (sev == Sev.Progress)
            {
                var ring = new System.Windows.Shapes.Ellipse
                {
                    Width = 9, Height = 9, Stroke = Theme.Frozen(color), StrokeThickness = 2,
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
                };
                sp.Children.Add(ring);
            }
            else
            {
                var dot = new System.Windows.Shapes.Ellipse
                {
                    Width = 8, Height = 8, Fill = Theme.Frozen(color),
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
                };
                sp.Children.Add(dot);
            }
            sp.Children.Add(new TextBlock { Text = text, Foreground = Theme.Frozen(color),
                FontSize = Theme.FsSmall, FontWeight = FontWeights.SemiBold, FontFamily = Theme.UiFont,
                VerticalAlignment = VerticalAlignment.Center });

            var bd = new Border
            {
                Background = Theme.Alpha(color, 28), BorderBrush = Theme.Alpha(color, 90),
                BorderThickness = new Thickness(1), CornerRadius = Theme.R8,
                Padding = new Thickness(10, 4, 12, 4), Child = sp,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Ctl.AutomationSetName(bd, text);
            return bd;
        }
    }

    // Тумблер с плавной анимацией ручки через TranslateTransform.
    // Скруглённый прямоугольник (не овал): дорожка со скруглением R8, ручка — квадрат со скруглением.
    class Toggle : System.Windows.Controls.Primitives.ToggleButton
    {
        Border _track;
        Border _knob;
        TranslateTransform _tx;
        const double TrackW = 46, TrackH = 26, KnobD = 20, Inset = 3;
        readonly double _travel = TrackW - KnobD - Inset * 2;

        public Toggle(string accessibleName)
        {
            Width = TrackW; Height = TrackH;
            Cursor = System.Windows.Input.Cursors.Hand;
            Focusable = true;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;
            SnapsToDevicePixels = true;

            _tx = new TranslateTransform(0, 0);
            _knob = new Border
            {
                Width = KnobD, Height = KnobD,
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Inset, 0, 0, 0),
                RenderTransform = _tx,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.35, Direction = 270 }
            };
            // Дорожка занимает весь контрол целиком (иначе схлопывается в овал вокруг ручки).
            _track = new Border
            {
                Width = TrackW, Height = TrackH,
                CornerRadius = new CornerRadius(8),
                Background = Theme.BrSurfaceHi,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1),
                Child = _knob
            };

            Content = _track;
            Template = MakeTemplate();
            FocusVisualStyle = Ctl.FocusStyle();
            Checked += (s, e) => Render(true);
            Unchecked += (s, e) => Render(true);
            Ctl.AutomationSetName(this, accessibleName);
            Loaded += (s, e) => Render(false);
        }

        System.Windows.Controls.ControlTemplate MakeTemplate()
        {
            var t = new System.Windows.Controls.ControlTemplate(typeof(Toggle));
            var f = new FrameworkElementFactory(typeof(ContentPresenter));
            f.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            f.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Stretch);
            t.VisualTree = f;
            return t;
        }

        void Render(bool animate)
        {
            bool on = IsChecked == true;
            _track.Background = on ? Theme.BrAccent : Theme.BrSurfaceHi;
            _track.BorderBrush = on ? Theme.BrAccent : Theme.BrStroke;
            double target = on ? _travel : 0;
            if (animate && Theme.AnimationsEnabled)
            {
                var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(160))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                _tx.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            else
            {
                _tx.BeginAnimation(TranslateTransform.XProperty, null);
                _tx.X = target;
            }
        }
    }
}
