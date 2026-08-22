using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ZapretStudio
{
    // Тема оформления: тёмная / светлая / AMOLED / северное сияние / закат / Peter Griffin.
    enum ThemeMode { Dark, Light, Amoled, Aurora, Sunset, Peter }

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

        // Тёплая глубокая тема с зелёным акцентом. Контраст подобран так, чтобы
        // журнал и статусы оставались читаемыми даже на неярких мониторах.
        static void SetAuroraPalette()
        {
            BgDeep     = Rgb(0x0B, 0x16, 0x16);
            BgBase     = Rgb(0x0F, 0x20, 0x20);
            Surface    = Rgb(0x15, 0x2A, 0x2A);
            SurfaceAlt = Rgb(0x1A, 0x34, 0x33);
            SurfaceHi  = Rgb(0x24, 0x43, 0x40);
            Stroke     = Rgb(0x30, 0x51, 0x4E);
            StrokeSoft = Rgb(0x22, 0x3B, 0x39);
            Text       = Rgb(0xEF, 0xF7, 0xF3);
            TextMuted  = Rgb(0xA7, 0xBF, 0xB7);
            TextFaint  = Rgb(0x72, 0x91, 0x89);
            AccentMain = Rgb(0x4D, 0xC5, 0x96);
            AccentHi   = Rgb(0x7A, 0xE1, 0xB8);
            AccentDim  = Rgb(0x1D, 0x5C, 0x49);
            Ok  = Rgb(0x62, 0xD3, 0x8A); OkDim  = Rgb(0x16, 0x3A, 0x29);
            Warn = Rgb(0xF0, 0xB8, 0x52); WarnDim = Rgb(0x43, 0x31, 0x12);
            Err = Rgb(0xF0, 0x76, 0x76); ErrDim = Rgb(0x43, 0x20, 0x22);
        }

        // Тёплая вечерняя тема: глубокий сумеречный фон и закатный кораллово-персиковый неон.
        static void SetSunsetPalette()
        {
            BgDeep     = Rgb(0x15, 0x12, 0x1B);
            BgBase     = Rgb(0x1B, 0x16, 0x22);
            Surface    = Rgb(0x24, 0x1E, 0x2D);
            SurfaceAlt = Rgb(0x2C, 0x25, 0x37);
            SurfaceHi  = Rgb(0x38, 0x2F, 0x45);
            Stroke     = Rgb(0x4A, 0x3C, 0x5B);
            StrokeSoft = Rgb(0x33, 0x2A, 0x3E);
            Text       = Rgb(0xFF, 0xF3, 0xEB);
            TextMuted  = Rgb(0xBF, 0xAA, 0xBF);
            TextFaint  = Rgb(0x86, 0x73, 0x89);
            AccentMain = Rgb(0xFF, 0x6B, 0x6B);
            AccentHi   = Rgb(0xFF, 0x8E, 0x72);
            AccentDim  = Rgb(0x61, 0x26, 0x2B);
            Ok  = Rgb(0x4A, 0xDE, 0x80); OkDim  = Rgb(0x1E, 0x3B, 0x27);
            Warn = Rgb(0xFB, 0xBF, 0x24); WarnDim = Rgb(0x45, 0x33, 0x15);
            Err = Rgb(0xF4, 0x3F, 0x5E); ErrDim = Rgb(0x45, 0x1D, 0x24);
        }

        // Мягкая семейная палитра: небесно-голубой фон, зелёные акценты и
        // тёплая жёлтая подсветка — под фоновую иллюстрацию Питера Гриффина.
        static void SetPeterPalette()
        {
            BgDeep     = Rgb(0xD9, 0xEC, 0xF3);
            BgBase     = Rgb(0xEE, 0xF7, 0xFA);
            Surface    = Rgb(0xFA, 0xFD, 0xFE);
            SurfaceAlt = Rgb(0xE5, 0xF1, 0xF5);
            SurfaceHi  = Rgb(0xCF, 0xE3, 0xEA);
            Stroke     = Rgb(0xAE, 0xCD, 0xD7);
            StrokeSoft = Rgb(0xC9, 0xDF, 0xE6);
            Text       = Rgb(0x1E, 0x35, 0x3B);
            TextMuted  = Rgb(0x50, 0x70, 0x78);
            TextFaint  = Rgb(0x7A, 0x99, 0xA1);
            AccentMain = Rgb(0x4D, 0x9B, 0x62);
            AccentHi   = Rgb(0x60, 0xB8, 0x77);
            AccentDim  = Rgb(0xC5, 0xE5, 0xCC);
            Ok  = Rgb(0x37, 0x96, 0x57); OkDim  = Rgb(0xD5, 0xEF, 0xDC);
            Warn = Rgb(0xB7, 0x7B, 0x18); WarnDim = Rgb(0xFB, 0xED, 0xCB);
            Err = Rgb(0xC1, 0x55, 0x4D); ErrDim = Rgb(0xF8, 0xDF, 0xDC);
        }

        public static void Apply(ThemeMode mode)
        {
            Mode = mode;
            if (mode == ThemeMode.Light) SetLightPalette();
            else if (mode == ThemeMode.Amoled) SetAmoledPalette();
            else if (mode == ThemeMode.Aurora) SetAuroraPalette();
            else if (mode == ThemeMode.Sunset) SetSunsetPalette();
            else if (mode == ThemeMode.Peter) SetPeterPalette();
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
            if (Mode == ThemeMode.Light) return ThemeMode.Aurora;
            if (Mode == ThemeMode.Aurora) return ThemeMode.Sunset;
            if (Mode == ThemeMode.Sunset) return ThemeMode.Peter;
            return ThemeMode.Dark;
        }

        public static void ToggleMode()
        {
            Apply(NextMode());
        }

        // До первого скачивания доступны системные fallback-шрифты. После успешной
        // проверки SHA-256 Core.ConfigureUiFonts подменяет эти семейства на локальные
        // Google Sans / Google Sans Code из папки utils\\fonts.
        public static FontFamily UiFont = new FontFamily("Google Sans, Google Sans Text, Product Sans, Segoe UI");
        public static FontFamily MonoFont = new FontFamily("Google Sans Code, Cascadia Mono, Consolas");

        public static bool ConfigureDownloadedFonts(string fontDirectory)
        {
            try
            {
                if (string.IsNullOrEmpty(fontDirectory) || !System.IO.Directory.Exists(fontDirectory)) return false;
                string dir = System.IO.Path.GetFullPath(fontDirectory);
                if (!dir.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())) dir += System.IO.Path.DirectorySeparatorChar;
                var uri = new Uri(dir, UriKind.Absolute);
                UiFont = new FontFamily(uri, "./#Google Sans");
                MonoFont = new FontFamily(uri, "./#Google Sans Code");
                return true;
            }
            catch
            {
                return false;
            }
        }

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

    public enum IconAnimType
    {
        None,
        ScaleBounce,
        Rotate360,
        Rotate90,
        Pulse,
        Wiggle,
        Float
    }

    // Официальные иконки Lucide Icons (24x24 SVG) — чистый, ультрасовременный контурный дизайн
    static class Icons
    {
        // F: префикс для заливочных элементов, стандартные строки — для контурных (stroke)
        public const string Home = "M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M9 22V12h6v10";
        public const string Grid = "M3 3h7v7H3z M14 3h7v7h-7z M14 14h7v7h-7z M3 14h7v7H3z";
        public const string Pulse = "M22 12h-4l-3 9L9 3l-3 9H2";
        public const string Gear = "M12.22 2h-.44a2 2 0 0 0-2 2v.18a2 2 0 0 1-1 1.73l-.43.25a2 2 0 0 1-2 0l-.15-.08a2 2 0 0 0-2.73.73l-.22.38a2 2 0 0 0 .73 2.73l.15.1a2 2 0 0 1 1 1.72v.51a2 2 0 0 1-1 1.74l-.15.09a2 2 0 0 0-.73 2.73l.22.38a2 2 0 0 0 2.73.73l.15-.08a2 2 0 0 1 2 0l.43.25a2 2 0 0 1 1 1.73V20a2 2 0 0 0 2 2h.44a2 2 0 0 0 2-2v-.18a2 2 0 0 1 1-1.73l.43-.25a2 2 0 0 1 2 0l.15.08a2 2 0 0 0 2.73-.73l.22-.39a2 2 0 0 0-.73-2.73l-.15-.08a2 2 0 0 1-1-1.74v-.5a2 2 0 0 1 1-1.74l.15-.09a2 2 0 0 0 .73-2.73l-.22-.38a2 2 0 0 0-2.73-.73l-.15.08a2 2 0 0 1-2 0l-.43-.25a2 2 0 0 1-1-1.73V4a2 2 0 0 0-2-2z M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6";
        public const string Server = "M2 4a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V4zm0 12a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-4z M6 6h.01 M6 18h.01";
        public const string Filter = "M22 3H2l8 9.46V19l4 2v-8.54L22 3z";
        public const string List = "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z M14 2v6h6 M16 13H8 M16 17H8 M10 9H8";
        public const string Info = "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z M12 16v-4 M12 8h.01";
        public const string Play = "F:M5 3l14 9-14 9V3z";
        public const string Pause = "F:M6 4h4v16H6z M14 4h4v16h-4z";
        public const string Stop = "F:M6 6h12v12H6z";
        public const string SkipNext = "F:M5 4l10 8-10 8V4z M19 5v14";
        public const string SkipPrev = "F:M19 20L9 12l10-8v16z M5 19V5";
        public const string Music = "M9 18V5l12-2v13 M9 18a3 3 0 1 1-6 0 3 3 0 0 1 6 0z M21 16a3 3 0 1 1-6 0 3 3 0 0 1 6 0z";
        public const string Shuffle = "M16 3h5v5 M4 20L21 3 M21 16v5h-5 M15 15l6 6 M4 4l5 5";
        public const string VolumeUp = "M11 5L6 9H2v6h4l5 4V5z M19.07 4.93a10 10 0 0 1 0 14.14 M15.54 8.46a5 5 0 0 1 0 7.07";
        public const string VolumeDown = "M11 5L6 9H2v6h4l5 4V5z M15.54 8.46a5 5 0 0 1 0 7.07";
        public const string VolumeOff = "M11 5L6 9H2v6h4l5 4V5z M23 9l-6 6 M17 9l6 6";
        public const string Restart = "M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8 M21 3v5h-5";
        public const string Refresh = Restart;
        public const string Folder = "M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z";
        public const string Github   = "F:M12 2 A10 10 0 0 0 9 21.5 C9 20 9 18.5 9 18 C6.5 18.5 6 16.5 6 16.5 C5.5 15 4.5 14.5 4.5 14.5 C3.5 14 4.5 14 4.5 14 C6 14 6.5 15.5 6.5 15.5 C7.5 17 9 16.5 9.5 16.5 C9.5 15.5 10 15 10.5 14.5 C7.5 14 5.5 13 5.5 9.5 C5.5 8 6 7 6.5 6.5 C6.5 6 6 5 6.5 3.5 C6.5 3.5 8 3.5 9.5 5 C10.5 4.5 13.5 4.5 14.5 5 C16 3.5 17.5 3.5 17.5 3.5 C18 5 17.5 6 17.5 6.5 C18 7 18.5 8 18.5 9.5 C18.5 13 16.5 14 13.5 14.5 C14 15 14.5 16 14.5 17.5 C14.5 18.5 14.5 20 14.5 21.5 A10 10 0 0 0 12 2";
        public const string Check = "M20 6L9 17l-5-5";
        public const string Cross = "M18 6L6 18 M6 6l12 12";
        public const string Warn = "M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z M12 9v4 M12 17h.01";
        public const string Search = "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z M21 21l-4.35-4.35";
        public const string Star = "M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z";
        public const string StarFilled = "F:M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z";
        public const string Copy = "M8 8H4a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-4 M16 4H8a2 2 0 0 0-2 2v8a2 2 0 0 0 2 2h8a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2z";
        public const string Save = "M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z M17 21v-8H7v8 M7 3v5h8";
        public const string External = "M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6 M15 3h6v6 M10 14L21 3";
        public const string Shield = "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z";
        public const string Dot = "F:M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z";
        public const string Down = "M6 9l6 6 6-6";
        public const string Menu = "M3 12h18 M3 6h18 M3 18h18";
        public const string MenuOpen = "M3 12h12 M3 6h18 M3 18h18 M19 9l3 3-3 3";
        public const string Sun = "M12 1v2 M12 21v2 M4.22 4.22l1.42 1.42 M18.36 18.36l1.42 1.42 M1 12h2 M21 12h2 M4.22 19.78l1.42-1.42 M18.36 5.64l1.42-1.42 M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10z";
        public const string Moon = "M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z";
        public const string Globe = "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z M2 12h20 M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z";
        public const string Game = "M6 11h4 M8 9v4 M15 12h.01 M18 10h.01 M17.32 5H6.68a4 4 0 0 0-3.978 3.59c-.006.052-.01.101-.017.152C2.604 9.416 2 14.456 2 16a3 3 0 0 0 3 3c1 0 1.5-.5 2-1l1.414-1.414A2 2 0 0 1 9.828 16h4.344a2 2 0 0 1 1.414.586L17 18c.5.5 1 1 2 1a3 3 0 0 0 3-3c0-1.545-.604-6.584-.685-7.258-.007-.05-.011-.1-.017-.151A4 4 0 0 0 17.32 5z";
        public const string Plug = "M12 22v-5 M9 8V2 M15 8V2 M18 8v5a4 4 0 0 1-4 4h-4a4 4 0 0 1-4-4V8z";
        public const string Download = "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3";
        public const string Telegram = "M22 2L11 13 M22 2l-7 20-4-9-9-4 20-7z";
        public const string Link = "M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71 M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71";
        public const string Bolt = "M13 2L3 14h9l-1 8 10-12h-9l1-8z";
        public const string Lantern = "M12 2a5 5 0 0 0-5 5v3a5 5 0 0 0 10 0V7a5 5 0 0 0-5-5z M9 18h6 M10 22h4 M12 7v5";
    }

    static class UI
    {
        public static System.Windows.Shapes.Path Icon(string data, double size, Brush stroke, double thickness = 1.8)
        {
            if (string.IsNullOrEmpty(data)) return new System.Windows.Shapes.Path();

            bool isFill = data.StartsWith("F:");
            string pathData = isFill ? data.Substring(2) : data;

            var p = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(pathData),
                Stretch = Stretch.Uniform,
                Width = size,
                Height = size,
                SnapsToDevicePixels = true
            };

            if (isFill)
            {
                p.Fill = stroke;
                p.Stroke = null;
            }
            else
            {
                p.Fill = Brushes.Transparent;
                p.Stroke = stroke;
                p.StrokeThickness = thickness;
                p.StrokeStartLineCap = PenLineCap.Round;
                p.StrokeEndLineCap = PenLineCap.Round;
                p.StrokeLineJoin = PenLineJoin.Round;
            }
            return p;
        }

        public static System.Windows.Shapes.Path Icon(string data, double size, Brush stroke)
        { return Icon(data, size, stroke, 1.8); }

        public static void AttachIconHoverAnimation(FrameworkElement trigger, FrameworkElement icon, IconAnimType type)
        {
            if (trigger == null || icon == null || type == IconAnimType.None) return;

            var transformGroup = new TransformGroup();
            var scale = new ScaleTransform(1, 1);
            var rotate = new RotateTransform(0);
            var translate = new TranslateTransform(0, 0);
            transformGroup.Children.Add(scale);
            transformGroup.Children.Add(rotate);
            transformGroup.Children.Add(translate);

            icon.RenderTransform = transformGroup;
            icon.RenderTransformOrigin = new Point(0.5, 0.5);

            trigger.MouseEnter += (s, e) =>
            {
                if (!Theme.AnimationsEnabled) return;
                switch (type)
                {
                    case IconAnimType.ScaleBounce:
                    {
                        var anim = new DoubleAnimation(1.0, 1.22, TimeSpan.FromMilliseconds(160))
                        {
                            AutoReverse = true,
                            EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.7 }
                        };
                        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                        break;
                    }
                    case IconAnimType.Rotate360:
                    {
                        var anim = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(420))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
                        break;
                    }
                    case IconAnimType.Rotate90:
                    {
                        var anim = new DoubleAnimation(0, 90, TimeSpan.FromMilliseconds(240))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
                        break;
                    }
                    case IconAnimType.Pulse:
                    {
                        var anim = new DoubleAnimation(1.0, 1.28, TimeSpan.FromMilliseconds(180))
                        {
                            AutoReverse = true,
                            EasingFunction = new ElasticEase { EasingMode = EasingMode.EaseOut, Oscillations = 1, Springiness = 4 }
                        };
                        scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                        scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                        break;
                    }
                    case IconAnimType.Wiggle:
                    {
                        var anim = new DoubleAnimation(-14, 14, TimeSpan.FromMilliseconds(85))
                        {
                            AutoReverse = true,
                            RepeatBehavior = new RepeatBehavior(2),
                            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                        };
                        rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
                        break;
                    }
                    case IconAnimType.Float:
                    {
                        var anim = new DoubleAnimation(0, -3, TimeSpan.FromMilliseconds(160))
                        {
                            AutoReverse = true,
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        translate.BeginAnimation(TranslateTransform.YProperty, anim);
                        break;
                    }
                }
            };

            trigger.MouseLeave += (s, e) =>
            {
                if (!Theme.AnimationsEnabled) return;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)));
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)));
                rotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(140)));
                translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(120)));
            };
        }

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
            var border = new Border
            {
                Background = bg, BorderBrush = brd, BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10, Padding = new Thickness(14, 9, 14, 9), Child = sp
            };

            var b = new Button { Content = border, Cursor = System.Windows.Input.Cursors.Hand, Focusable = true };

            if (iconData != null)
            {
                var ic = UI.Icon(iconData, 16, fg, 1.8);
                ic.VerticalAlignment = VerticalAlignment.Center;
                ic.Margin = new Thickness(0, 0, text != null ? 8 : 0, 0);
                sp.Children.Add(ic);
                var animType = (iconData == Icons.Restart || iconData == Icons.Refresh) ? IconAnimType.Rotate360 :
                               (iconData == Icons.Pulse) ? IconAnimType.Pulse :
                               (iconData == Icons.Gear) ? IconAnimType.Rotate90 :
                               (iconData == Icons.Music) ? IconAnimType.Wiggle : IconAnimType.ScaleBounce;
                UI.AttachIconHoverAnimation(b, ic, animType);
            }
            if (text != null)
                sp.Children.Add(new TextBlock { Text = text, Foreground = fg, FontSize = Theme.FsBody,
                    FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });

            StripChrome(b);
            b.Tag = new object[] { border, bg, kind };
            b.MouseEnter += (s, e) => { border.Background = Hover(bg, kind); };
            b.MouseLeave += (s, e) => { border.Background = bg; };
            AutomationSetName(b, text ?? Loc.T("common.button"));
            return b;
        }

        public static void SetButtonText(Button b, string text)
        {
            try
            {
                var border = b.Content as Border;
                if (border != null)
                {
                    var sp = border.Child as StackPanel;
                    if (sp != null)
                    {
                        foreach (var child in sp.Children)
                        {
                            var tb = child as TextBlock;
                            if (tb != null) { tb.Text = text; return; }
                        }
                    }
                }
            }
            catch { }
        }

        public static void SetButton(Button b, string text, string iconData, int kind)
        {
            try
            {
                Brush bg, fg, brd;
                switch (kind)
                {
                    case 0: bg = Theme.BrAccent; fg = Theme.BrOnAccent; brd = Theme.BrAccent; break;
                    case 2: bg = Theme.Alpha(Theme.Err, 30); fg = Theme.BrErr; brd = Theme.Alpha(Theme.Err, 110); break;
                    case 3: bg = Theme.BrSurfaceAlt; fg = Theme.BrText; brd = Theme.BrStroke; break;
                    default: bg = Theme.Alpha(Theme.Text, 8); fg = Theme.BrText; brd = Theme.BrStroke; break;
                }

                var border = b.Content as Border;
                if (border != null)
                {
                    border.Background = bg;
                    border.BorderBrush = brd;
                    var sp = border.Child as StackPanel;
                    if (sp != null)
                    {
                        sp.Children.Clear();
                        if (iconData != null)
                        {
                            var ic = UI.Icon(iconData, 16, fg, 1.8);
                            ic.VerticalAlignment = VerticalAlignment.Center;
                            ic.Margin = new Thickness(0, 0, text != null ? 8 : 0, 0);
                            sp.Children.Add(ic);
                            var animType = (iconData == Icons.Restart || iconData == Icons.Refresh) ? IconAnimType.Rotate360 :
                                           (iconData == Icons.Pulse) ? IconAnimType.Pulse :
                                           (iconData == Icons.Gear) ? IconAnimType.Rotate90 :
                                           (iconData == Icons.Music) ? IconAnimType.Wiggle : IconAnimType.ScaleBounce;
                            UI.AttachIconHoverAnimation(b, ic, animType);
                        }
                        if (text != null)
                        {
                            sp.Children.Add(new TextBlock { Text = text, Foreground = fg, FontSize = Theme.FsBody,
                                FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
                        }
                    }
                    b.Tag = new object[] { border, bg, kind };
                }
                AutomationSetName(b, text ?? Loc.T("common.button"));
            }
            catch { }
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
            AddMotion(b);
        }

        // Единая микроанимация для всех кнопок. Работает через RenderTransform,
        // поэтому не запускает перерасчёт разметки и остаётся лёгкой на слабых ПК.
        // При «Уменьшить анимацию» состояние меняется мгновенно.
        public static void AddMotion(System.Windows.Controls.Primitives.ButtonBase control)
        {
            if (control == null || control.RenderTransform is ScaleTransform) return;

            var scale = new ScaleTransform(1, 1);
            control.RenderTransform = scale;
            control.RenderTransformOrigin = new Point(0.5, 0.5);

            Action<double, int> setScale = (target, duration) =>
            {
                if (!Theme.AnimationsEnabled)
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    scale.ScaleX = target;
                    scale.ScaleY = target;
                    return;
                }

                var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(duration))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            };

            control.MouseEnter += (s, e) => { if (control.IsEnabled) setScale(1.012, 110); };
            control.MouseLeave += (s, e) => setScale(1, 120);
            control.PreviewMouseLeftButtonDown += (s, e) => { if (control.IsEnabled) setScale(0.972, 70); };
            control.PreviewMouseLeftButtonUp += (s, e) => setScale(control.IsMouseOver ? 1.012 : 1, 130);
            control.LostMouseCapture += (s, e) => setScale(control.IsMouseOver ? 1.012 : 1, 120);
            control.IsEnabledChanged += (s, e) => { if (!control.IsEnabled) setScale(1, 0); };
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
            AddMotion(cb);
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

        public static string GetText(Border pill)
        {
            try { return ((TextBlock)((StackPanel)pill.Child).Children[1]).Text; }
            catch { return ""; }
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
            Ctl.AddMotion(this);
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
