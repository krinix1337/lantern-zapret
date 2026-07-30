using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ZapretStudio
{
    // Тема оформления: тёмная / светлая / AMOLED / северное сияние / Peter Griffin.
    enum ThemeMode { Dark, Light, Amoled, Aurora, Peter }

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
            if (Mode == ThemeMode.Aurora) return ThemeMode.Peter;
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

    // Material Design 3 icons (24x24 SVG path data). Контуры намеренно не
    // используются: MD3-иконки — заливочные, с единым визуальным весом.
    static class Icons
    {
        public const string Home = "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z";
        public const string Grid = "M3 3h8v8H3V3m10 0h8v8h-8V3M3 13h8v8H3v-8m10 0h8v8h-8v-8z";
        public const string Pulse = "M3.55 19.09 7 15.64l4 4L22.45 8.18l-1.41-1.41L11 16.81l-4-4-4.86 4.87zM14 4l2.29 2.29-4.88 4.88 1.41 1.41 4.88-4.88L20 10V4z";
        public const string Gear = "M3 17v2h6v-2H3M3 5v2h10V5H3m10 14v-2h8v-2h-8v-2h-2v6h2M7 9v2H3v2h4v2h2V9H7m14 4v-2H11v2h10M15 9h2V7h4V5h-4V3h-2v6z";
        public const string Server = "M3 3h18v6H3V3m2 2v2h14V5H5m-2 6h18v6H3v-6m2 2v2h14v-2H5m-2 6h18v2H3v-2z";
        public const string Filter = "M3 5h18l-7 8v5l-4 2v-7L3 5z";
        public const string List = "M3 13h2v-2H3v2m0 4h2v-2H3v2m0-8h2V7H3v2m4 4h14v-2H7v2m0 4h14v-2H7v2m0-8v2h14V7H7z";
        public const string Info = "M11 17h2v-6h-2v6m1-15a10 10 0 1 0 0 20 10 10 0 0 0 0-20m0 18a8 8 0 1 1 0-16 8 8 0 0 1 0 16m-1-11h2V7h-2v2z";
        public const string Play = "M8 5v14l11-7z";
        public const string Stop = "M6 6h12v12H6z";
        public const string Restart = "M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z";
        public const string Refresh = Restart;
        public const string Folder = "M10 4H2v16h20V6H12l-2-2m10 14H4V8h16v10z";
        public const string Github   = "M12 2 A10 10 0 0 0 9 21.5 C9 20 9 18.5 9 18 C6.5 18.5 6 16.5 6 16.5 C5.5 15 4.5 14.5 4.5 14.5 C3.5 14 4.5 14 4.5 14 C6 14 6.5 15.5 6.5 15.5 C7.5 17 9 16.5 9.5 16.5 C9.5 15.5 10 15 10.5 14.5 C7.5 14 5.5 13 5.5 9.5 C5.5 8 6 7 6.5 6.5 C6.5 6 6 5 6.5 3.5 C6.5 3.5 8 3.5 9.5 5 C10.5 4.5 13.5 4.5 14.5 5 C16 3.5 17.5 3.5 17.5 3.5 C18 5 17.5 6 17.5 6.5 C18 7 18.5 8 18.5 9.5 C18.5 13 16.5 14 13.5 14.5 C14 15 14.5 16 14.5 17.5 C14.5 18.5 14.5 20 14.5 21.5 A10 10 0 0 0 12 2";
        public const string Check = "m9 16.17-4.17-4.18L3.41 13.4 9 19 21 7l-1.41-1.41z";
        public const string Cross = "M18.3 5.71 16.89 4.29 12 9.17 7.11 4.29 5.7 5.71 10.59 10.59 5.7 15.48l1.41 1.41L12 12l4.89 4.89 1.41-1.41-4.89-4.89z";
        public const string Warn = "M1 21h22L12 2 1 21m12-3h-2v-2h2v2m0-4h-2v-4h2v4z";
        public const string Search = "M9.5 3a6.5 6.5 0 0 1 5.2 10.4L21 19.7 19.7 21l-6.3-6.3A6.5 6.5 0 1 1 9.5 3m0 2a4.5 4.5 0 1 0 0 9 4.5 4.5 0 0 0 0-9z";
        // Material Symbols Rounded SVG paths, imported from Google's public icon set.
        public const string Star = "M19.65 9.04l-4.84-.42-1.89-4.45c-.34-.81-1.5-.81-1.84 0L9.19 8.63l-4.83.41c-.88.07-1.24 1.17-.57 1.75l3.67 3.18-1.1 4.72c-.2.86.73 1.54 1.49 1.08l4.15-2.5 4.15 2.51c.76.46 1.69-.22 1.49-1.08l-1.1-4.73 3.67-3.18c.67-.58.32-1.68-.56-1.75zM12 15.4l-3.76 2.27 1-4.28-3.32-2.88 4.38-.38L12 6.1l1.71 4.04 4.38.38-3.32 2.88 1 4.28L12 15.4z";
        public const string StarFilled = "M12 17.27 18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z";
        public const string Copy = "M19 21H8c-1.1 0-2-.9-2-2V8c0-1.1.9-2 2-2h11c1.1 0 2 .9 2 2v11c0 1.1-.9 2-2 2M8 8v11h11V8H8M16 3H4c-1.1 0-2 .9-2 2v12h2V5h12V3z";
        public const string Save = "M17 3H5c-1.1 0-1.99.9-1.99 2L3 19c0 1.1.89 2 1.99 2H19c1.1 0 2-.9 2-2V7l-4-4m-5 16a3 3 0 1 1 0-6 3 3 0 0 1 0 6M6 8V5h9v3H6z";
        public const string External = "M14 3v2h3.59L7.76 14.83l1.41 1.41L19 6.41V10h2V3h-7M5 5h7V3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7H5V5z";
        public const string Shield = "M12 1 3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4m0 4.18 5 2.22v3.36c0 3.54-2.29 6.86-5 7.93-2.71-1.07-5-4.39-5-7.93V7.4l5-2.22z";
        public const string Dot = "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8z";
        public const string Down = "M7 10l5 5 5-5z";
        public const string Menu = "M3 18h18v-2H3v2m0-5h18v-2H3v2m0-7v2h18V6H3z";
        public const string MenuOpen = "M4 18h11c.55 0 1-.45 1-1s-.45-1-1-1H4c-.55 0-1 .45-1 1s.45 1 1 1m0-5h8c.55 0 1-.45 1-1s-.45-1-1-1H4c-.55 0-1 .45-1 1s.45 1 1 1M4 8h11c.55 0 1-.45 1-1s-.45-1-1-1H4c-.55 0-1 .45-1 1s.45 1 1 1m16.3 6.88L17.42 12l2.88-2.88c.39-.39.39-1.02 0-1.41s-1.02-.39-1.41 0l-3.59 3.59c-.39.39-.39 1.02 0 1.41l3.59 3.59c.39.39 1.02.39 1.41 0s.39-1.02 0-1.41z";
        public const string Sun = "M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6m0-7 1.25 2.75L16 5l-2.75 1.25L12 9l-1.25-2.75L8 5l2.75-.25L12 2m-7 8 2.75 1.25L8 14l-1.25-2.75L4 10l2.75-1.25L8 6l1.25 2.75L12 10l-2.75 1.25L8 14l-1.25-2.75L4 10m14-2 1.25 2.75L22 12l-2.75 1.25L18 16l-1.25-2.75L14 12l2.75-1.25L18 8z";
        public const string Moon = "M9.37 5.51A7 7 0 0 0 18.49 14 7 7 0 1 1 9.37 5.51z";
        public const string Globe = "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20m6.9 6h-2.95c-.32-1.25-.84-2.45-1.55-3.35A8.03 8.03 0 0 1 18.9 8M12 4.04c.83 1.2 1.48 2.5 1.89 3.96h-3.78A14.1 14.1 0 0 1 12 4.04M4.26 14a8.2 8.2 0 0 1 0-4h3.13a16.6 16.6 0 0 0 0 4H4.26m.84 2h2.95c.32 1.25.84 2.45 1.55 3.35A8.03 8.03 0 0 1 5.1 16m2.95-8H5.1a8.03 8.03 0 0 1 4.5-3.35A13.7 13.7 0 0 0 8.05 8m1.84 8h4.22A14.1 14.1 0 0 1 12 19.96 14.1 14.1 0 0 1 9.89 16m-1-2a14.5 14.5 0 0 1 0-4h6.22a14.5 14.5 0 0 1 0 4H8.89m5.51 5.35c.71-.9 1.23-2.1 1.55-3.35h2.95a8.03 8.03 0 0 1-4.5 3.35M16.61 14a16.6 16.6 0 0 0 0-4h3.13a8.2 8.2 0 0 1 0 4h-3.13z";
        public const string Game = "M7.97 16 5.5 18.35C4.9 18.92 3.87 18.5 3.87 17.67V9.5c0-3.04 2.46-5.5 5.5-5.5h5.26c3.04 0 5.5 2.46 5.5 5.5v8.17c0 .83-1.03 1.25-1.63.68L16.03 16h-2.05l-1.25 1.25c-.4.4-1.04.4-1.44 0L10.05 16H7.97m2.03-7H8v2H6v2h2v2h2v-2h2v-2h-2V9m5.5.5a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3m2.5 2a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3z";
        public const string Plug = "M7 2v6H5v2h2v3a5 5 0 0 0 4 4.9V22h2v-4.1a5 5 0 0 0 4-4.9v-3h2V8h-2V2h-2v6H9V2H7m2 8h6v3a3 3 0 0 1-6 0v-3z";
        public const string Download = "M19 9h-4V3H9v6H5l7 7 7-7M5 18v2h14v-2H5z";
        public const string Telegram = "M21 5 2 12l7 2 2 6 3-4 4 3 3-14M9.5 13.5 17.5 8l-6.3 7.1-.2 2.1-1-3.7z";
        public const string Link = "M3.9 12c0-1.71 1.39-3.1 3.1-3.1H10V7H7a5 5 0 0 0 0 10h3v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1m4.1 1h8v-2H8v2m9-6h-3v1.9h3a3.1 3.1 0 0 1 0 6.2h-3V17h3a5 5 0 0 0 0-10z";
        public const string Bolt = "M11 21h-1l1-7H7.5c-.88 0-.33-.75-.31-.78C8.48 10.94 10.42 7.54 13 3h1l-1 7h3.5c.4 0 .62.19.4.66C12.97 17.55 11 21 11 21z";
        public const string Lantern = "M9 21h6v-1H9v1m3-19a7 7 0 0 0-4 12.74V17h8v-2.26A7 7 0 0 0 12 2m2 11.73V15h-4v-1.27l-.48-.3A5 5 0 1 1 14.48 13l-.48.3z";
    }

    static class UI
    {
        public static System.Windows.Shapes.Path Icon(string data, double size, Brush stroke, double thickness)
        {
            return new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(data),
                Fill = stroke,
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
