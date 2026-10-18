using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace ZapretStudio
{
    // Тема оформления: тёмная / AMOLED / светлая / закат / System24.
    // Порядок членов = порядок в списке настроек и в цикле кнопки темы в шапке.
    // Имена в gui-config.ini берутся из ToString() в нижний регистр (Theme.Name),
    // поэтому удалять тему можно только вместе с миграцией чужих конфигов (App.ApplySavedTheme).
    enum ThemeMode { Dark, Amoled, Light, Sunset, System24 }

    // Централизованная дизайн-система. Кисти НЕ заморожены — при смене темы
    // мутируем их .Color, и все контролы, держащие ту же ссылку, перерисовываются.
    static class Theme
    {
        public static ThemeMode Mode = ThemeMode.Dark;

        // Единый реестр живых тем. Раньше соответствие «тема ↔ строка конфига» было
        // продублировано в App, MainWindow и View.Settings тремя тернарниками, и
        // удаление темы означало правку всех трёх мест.
        public static readonly ThemeMode[] Modes = { ThemeMode.Dark, ThemeMode.Amoled, ThemeMode.Light, ThemeMode.Sunset, ThemeMode.System24 };

        public static string Name(ThemeMode mode) { return mode.ToString().ToLowerInvariant(); }
        public static string CurrentTheme { get { return Name(Mode); } }
        public static string LocKey(ThemeMode mode) { return "settings.theme." + Name(mode); }
        public static string Label(ThemeMode mode) { return Loc.T(LocKey(mode)); }
        public static int IndexOf(ThemeMode mode) { return System.Array.IndexOf(Modes, mode); }

        // Строка конфига → тема. false, если имя неживое: удалённая тема или мусор.
        public static bool TryParse(string raw, out ThemeMode mode)
        {
            mode = ThemeMode.Dark;
            if (string.IsNullOrEmpty(raw)) return false;
            string s = raw.Trim().ToLowerInvariant();
            for (int i = 0; i < Modes.Length; i++)
                if (string.Equals(Name(Modes[i]), s, StringComparison.Ordinal)) { mode = Modes[i]; return true; }
            return false;
        }

        // Темы, удалённые из сборки, но способные остаться в чужих gui-config.ini
        // («aurora» — северное сияние, «peter» — Питер Грифин, «midnight» — полуночная).
        // Куда их менять: светлая тема уходила в светлую, тёмные — в тёмную, чтобы
        // ради косметики человеку не переворачивали оформление с ног на голову.
        static bool IsRemovedName(string s)
        {
            return s == "aurora" || s == "midnight"
                || s == "peter" || s == "petergriffin" || s == "peter_griffin" || s == "griffin";
        }

        public static bool IsRemovedTheme(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            ThemeMode m;
            if (TryParse(raw, out m)) return false;
            return IsRemovedName(raw.Trim().ToLowerInvariant());
        }

        // Возвращает тему-заменителя либо ThemeMode.Dark, если имя неизвестное
        // (App перепроверяет неизвестные по теме Windows).
        public static ThemeMode RemovedThemeFallback(string raw)
        {
            string s = (raw ?? "").Trim().ToLowerInvariant();
            bool lightFamily = s == "peter" || s == "petergriffin" || s == "peter_griffin" || s == "griffin";
            return lightFamily ? ThemeMode.Light : ThemeMode.Dark;
        }

        public static Color BgDeep, BgBase, Surface, SurfaceAlt, SurfaceHi, Stroke, StrokeSoft;
        public static Color Text, TextMuted, TextFaint;
        public static Color AccentMain, AccentHi, AccentDim;
        public static Color Ok, OkDim, Warn, WarnDim, Err, ErrDim, Info, InfoDim;

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
        public static readonly SolidColorBrush BrInfo       = new SolidColorBrush();
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
            Info = Rgb(0x4A, 0x9E, 0xFF); InfoDim = Rgb(0x18, 0x2E, 0x48);
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
            Info = Rgb(0x25, 0x63, 0xEB); InfoDim = Rgb(0xDB, 0xEA, 0xFE);
        }

        // Настоящий AMOLED, а не «тёмная, но чернее». Правила:
        // 1) большое поле (фон окна и холст контента) — выключенные пиксели #000000;
        //    шапка и сайдбар на 5% серого, иначе bands сливаются в пятно.
        // 2) карточки почти чёрные: объём даёт рамка, а не серая заливка — серая
        //    заливка на OLED светится и убивает смысл темы.
        // 3) все серые тона нейтральные, без синего подтона (на чёрном синева читается как грязь).
        // 4) текст не #FFFFFF: чистый белый на выключенном чёрном даёт гало и быстро утомляет.
        // 5) статусы чуть ярче, чем в Dark — вокруг них нет светлой подложки-карточки.
        static void SetAmoledPalette()
        {
            BgDeep     = Rgb(0x00, 0x00, 0x00);
            BgBase     = Rgb(0x05, 0x05, 0x07);
            Surface    = Rgb(0x0B, 0x0B, 0x0E);
            SurfaceAlt = Rgb(0x12, 0x12, 0x16);
            SurfaceHi  = Rgb(0x1C, 0x1C, 0x22);
            Stroke     = Rgb(0x27, 0x27, 0x30);
            StrokeSoft = Rgb(0x19, 0x19, 0x20);
            Text       = Rgb(0xE6, 0xE6, 0xE9);
            TextMuted  = Rgb(0x9A, 0xA0, 0xAB);
            TextFaint  = Rgb(0x6E, 0x73, 0x7B);
            AccentMain = Rgb(0x6C, 0x78, 0xF0);
            AccentHi   = Rgb(0x8B, 0x96, 0xFF);
            AccentDim  = Rgb(0x1E, 0x23, 0x40);
            Ok  = Rgb(0x59, 0xC1, 0x76); OkDim  = Rgb(0x0C, 0x20, 0x14);
            Warn = Rgb(0xE9, 0xB2, 0x5C); WarnDim = Rgb(0x2A, 0x1F, 0x0C);
            Err = Rgb(0xEF, 0x6B, 0x6B); ErrDim = Rgb(0x2A, 0x12, 0x14);
            Info = Rgb(0x5A, 0xA8, 0xFF); InfoDim = Rgb(0x0C, 0x1E, 0x33);
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
            Info = Rgb(0x56, 0x9C, 0xD6); InfoDim = Rgb(0x26, 0x2A, 0x38);
        }




        public const string FlavorDefault = "default";
        public const string FlavorMocha = "mocha";
        public const string FlavorTokyoNight = "tokyo_night";
        public const string FlavorNord = "nord";
        public const string FlavorRosePine = "rose_pine";
        public const string FlavorEverforest = "everforest";
        public const string FlavorVencord = "vencord";

        public static readonly string[] System24Flavors = new string[]
        {
            FlavorDefault,
            FlavorMocha,
            FlavorTokyoNight,
            FlavorNord,
            FlavorRosePine,
            FlavorEverforest,
            FlavorVencord
        };

        static string _system24Flavor;
        public static string System24Flavor
        {
            get
            {
                if (_system24Flavor == null)
                {
                    try { _system24Flavor = Core.Get("system24_flavor", FlavorDefault); }
                    catch { _system24Flavor = FlavorDefault; }
                    if (string.IsNullOrEmpty(_system24Flavor) || !IsValidFlavor(_system24Flavor))
                    {
                        _system24Flavor = FlavorDefault;
                        try { Core.Set("system24_flavor", FlavorDefault); Core.SaveConfig(); } catch { }
                    }
                }
                return _system24Flavor;
            }
            set
            {
                if (string.IsNullOrEmpty(value) || !IsValidFlavor(value))
                    _system24Flavor = FlavorDefault;
                else
                    _system24Flavor = value;
            }
        }

        static bool IsValidFlavor(string f)
        {
            if (string.IsNullOrEmpty(f)) return false;
            for (int i = 0; i < System24Flavors.Length; i++)
            {
                if (string.Equals(System24Flavors[i], f, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static Color GetFlavorAccent(string flavor)
        {
            if (flavor == FlavorMocha) return Rgb(0xCB, 0xA6, 0xF7);
            if (flavor == FlavorTokyoNight) return Rgb(0x7A, 0xA2, 0xF7);
            if (flavor == FlavorNord) return Rgb(0x88, 0xC0, 0xD0);
            if (flavor == FlavorRosePine) return Rgb(0xEB, 0xBC, 0xBA);
            if (flavor == FlavorEverforest) return Rgb(0xA7, 0xC0, 0x80);
            if (flavor == FlavorVencord) return Rgb(0x58, 0x65, 0xF2);
            return Rgb(0x9F, 0x7B, 0xC1); // FlavorDefault
        }

        public static string GetFlavorName(string flavor)
        {
            if (string.IsNullOrEmpty(flavor)) return Loc.T("system24.flavor.default");
            return Loc.T("system24.flavor." + flavor);
        }

        public static void ApplySystem24Flavor(string flavor)
        {
            System24Flavor = flavor;
            try
            {
                Core.Set("system24_flavor", System24Flavor);
                Core.SaveConfig();
            }
            catch { }

            if (Mode == ThemeMode.System24)
            {
                Apply(ThemeMode.System24);
            }
        }

        // Аутентичный TUI-стиль BetterDiscord System24 (refact0r/system24) с поддержкой подтем (flavors):
        // 1. default: матовый чистый уголь (#121212 / #161616 / #1C1C1C) + лаванда (#9F7BC1)
        // 2. mocha: Catppuccin Mocha (#11111B / #181825 / #1E1E2E) + лиловый Mauve (#CBA6F7)
        // 3. tokyo_night: Tokyo Night (#16161E / #1A1B26 / #24283B) + неон Cyan/Blue (#7AA2F7)
        // 4. nord: Nord Polar Night (#242933 / #2E3440 / #3B4252) + Frost (#88C0D0)
        // 5. rose_pine: Rosé Pine (#14121E / #191724 / #1F1D2E) + Rose (#EBBCBA)
        // 6. everforest: Everforest (#1E2326 / #232A2E / #272E33) + Forest (#A7C080)
        // 7. vencord: Charcoal + Discord Blurple (#5865F2)
        static void SetSystem24Palette(string flavor = null)
        {
            Info = Rgb(0x7D, 0xA1, 0xFF); InfoDim = Rgb(0x1B, 0x25, 0x3D);
            string f = flavor ?? System24Flavor;
            if (f == FlavorMocha)
            {
                BgDeep     = Rgb(0x11, 0x11, 0x1B);
                BgBase     = Rgb(0x18, 0x18, 0x25);
                Surface    = Rgb(0x1E, 0x1E, 0x2E);
                SurfaceAlt = Rgb(0x24, 0x27, 0x3A);
                SurfaceHi  = Rgb(0x31, 0x32, 0x44);
                Stroke     = Rgb(0x45, 0x47, 0x5A);
                StrokeSoft = Rgb(0x31, 0x32, 0x44);
                Text       = Rgb(0xCD, 0xD6, 0xF4);
                TextMuted  = Rgb(0xA6, 0xAD, 0xC8);
                TextFaint  = Rgb(0x6C, 0x70, 0x86);
                AccentMain = Rgb(0xCB, 0xA6, 0xF7);
                AccentHi   = Rgb(0xF5, 0xC2, 0xE7);
                AccentDim  = Rgb(0x3E, 0x2D, 0x52);
                Ok         = Rgb(0xA6, 0xE3, 0xA1); OkDim  = Rgb(0x1B, 0x3B, 0x26);
                Warn       = Rgb(0xF9, 0xE2, 0xAF); WarnDim = Rgb(0x42, 0x39, 0x1D);
                Err        = Rgb(0xF3, 0x8B, 0xA8); ErrDim = Rgb(0x42, 0x1B, 0x24);
            }
            else if (f == FlavorTokyoNight)
            {
                BgDeep     = Rgb(0x16, 0x16, 0x1E);
                BgBase     = Rgb(0x1A, 0x1B, 0x26);
                Surface    = Rgb(0x24, 0x28, 0x3B);
                SurfaceAlt = Rgb(0x2A, 0x2E, 0x45);
                SurfaceHi  = Rgb(0x34, 0x3B, 0x58);
                Stroke     = Rgb(0x41, 0x48, 0x68);
                StrokeSoft = Rgb(0x2F, 0x35, 0x4F);
                Text       = Rgb(0xC0, 0xCA, 0xF5);
                TextMuted  = Rgb(0x9A, 0xA5, 0xCE);
                TextFaint  = Rgb(0x56, 0x5F, 0x89);
                AccentMain = Rgb(0x7A, 0xA2, 0xF7);
                AccentHi   = Rgb(0x7D, 0xCF, 0xFF);
                AccentDim  = Rgb(0x25, 0x34, 0x59);
                Ok         = Rgb(0x9E, 0xCE, 0x6A); OkDim  = Rgb(0x23, 0x3B, 0x1E);
                Warn       = Rgb(0xE0, 0xAF, 0x68); WarnDim = Rgb(0x40, 0x33, 0x1C);
                Err        = Rgb(0xF7, 0x76, 0x8E); ErrDim = Rgb(0x45, 0x1E, 0x28);
            }
            else if (f == FlavorNord)
            {
                BgDeep     = Rgb(0x24, 0x29, 0x33);
                BgBase     = Rgb(0x2E, 0x34, 0x40);
                Surface    = Rgb(0x3B, 0x42, 0x52);
                SurfaceAlt = Rgb(0x43, 0x4C, 0x5E);
                SurfaceHi  = Rgb(0x4C, 0x56, 0x6A);
                Stroke     = Rgb(0x4C, 0x56, 0x6A);
                StrokeSoft = Rgb(0x3B, 0x42, 0x52);
                Text       = Rgb(0xEC, 0xEF, 0xF4);
                TextMuted  = Rgb(0xD8, 0xDE, 0xE9);
                TextFaint  = Rgb(0x7B, 0x88, 0xA1);
                AccentMain = Rgb(0x88, 0xC0, 0xD0);
                AccentHi   = Rgb(0x8F, 0xBC, 0xBB);
                AccentDim  = Rgb(0x27, 0x42, 0x4C);
                Ok         = Rgb(0xA3, 0xBE, 0x8C); OkDim  = Rgb(0x27, 0x38, 0x22);
                Warn       = Rgb(0xEB, 0xCB, 0x8B); WarnDim = Rgb(0x45, 0x3B, 0x23);
                Err        = Rgb(0xBF, 0x61, 0x6A); ErrDim = Rgb(0x3E, 0x1F, 0x23);
            }
            else if (f == FlavorRosePine)
            {
                BgDeep     = Rgb(0x14, 0x12, 0x1E);
                BgBase     = Rgb(0x19, 0x17, 0x24);
                Surface    = Rgb(0x1F, 0x1D, 0x2E);
                SurfaceAlt = Rgb(0x26, 0x23, 0x3A);
                SurfaceHi  = Rgb(0x31, 0x2E, 0x48);
                Stroke     = Rgb(0x40, 0x3D, 0x52);
                StrokeSoft = Rgb(0x26, 0x23, 0x3A);
                Text       = Rgb(0xE0, 0xDE, 0xF4);
                TextMuted  = Rgb(0x90, 0x8C, 0xAA);
                TextFaint  = Rgb(0x6E, 0x6A, 0x86);
                AccentMain = Rgb(0xEB, 0xBC, 0xBA);
                AccentHi   = Rgb(0xF6, 0xC1, 0x77);
                AccentDim  = Rgb(0x45, 0x2E, 0x36);
                Ok         = Rgb(0x9C, 0xCF, 0xD8); OkDim  = Rgb(0x1F, 0x38, 0x3D);
                Warn       = Rgb(0xF6, 0xC1, 0x77); WarnDim = Rgb(0x42, 0x33, 0x1C);
                Err        = Rgb(0xEB, 0x6F, 0x92); ErrDim = Rgb(0x42, 0x1B, 0x29);
            }
            else if (f == FlavorEverforest)
            {
                BgDeep     = Rgb(0x1E, 0x23, 0x26);
                BgBase     = Rgb(0x23, 0x2A, 0x2E);
                Surface    = Rgb(0x27, 0x2E, 0x33);
                SurfaceAlt = Rgb(0x2D, 0x35, 0x3B);
                SurfaceHi  = Rgb(0x37, 0x41, 0x45);
                Stroke     = Rgb(0x47, 0x52, 0x58);
                StrokeSoft = Rgb(0x32, 0x3C, 0x41);
                Text       = Rgb(0xD3, 0xC6, 0xAA);
                TextMuted  = Rgb(0x9D, 0xA9, 0xA0);
                TextFaint  = Rgb(0x70, 0x7C, 0x74);
                AccentMain = Rgb(0xA7, 0xC0, 0x80);
                AccentHi   = Rgb(0x83, 0xC0, 0x92);
                AccentDim  = Rgb(0x2C, 0x3E, 0x26);
                Ok         = Rgb(0xA7, 0xC0, 0x80); OkDim  = Rgb(0x27, 0x36, 0x20);
                Warn       = Rgb(0xDB, 0xBC, 0x7F); WarnDim = Rgb(0x3D, 0x34, 0x1E);
                Err        = Rgb(0xE6, 0x7E, 0x80); ErrDim = Rgb(0x40, 0x20, 0x22);
            }
            else if (f == FlavorVencord)
            {
                BgDeep     = Rgb(0x12, 0x12, 0x12);
                BgBase     = Rgb(0x16, 0x16, 0x16);
                Surface    = Rgb(0x1C, 0x1C, 0x1C);
                SurfaceAlt = Rgb(0x23, 0x23, 0x23);
                SurfaceHi  = Rgb(0x2D, 0x2D, 0x2D);
                Stroke     = Rgb(0x32, 0x32, 0x32);
                StrokeSoft = Rgb(0x24, 0x24, 0x24);
                Text       = Rgb(0xF0, 0xF0, 0xF2);
                TextMuted  = Rgb(0x9A, 0x9A, 0xA0);
                TextFaint  = Rgb(0x60, 0x60, 0x68);
                AccentMain = Rgb(0x58, 0x65, 0xF2);
                AccentHi   = Rgb(0x79, 0x83, 0xF5);
                AccentDim  = Rgb(0x26, 0x2B, 0x57);
                Ok         = Rgb(0x57, 0xF2, 0x87); OkDim  = Rgb(0x18, 0x3A, 0x24);
                Warn       = Rgb(0xFE, 0xE7, 0x5C); WarnDim = Rgb(0x3E, 0x38, 0x16);
                Err        = Rgb(0xED, 0x42, 0x45); ErrDim = Rgb(0x3E, 0x18, 0x1A);
            }
            else // FlavorDefault
            {
                BgDeep     = Rgb(0x12, 0x12, 0x12);
                BgBase     = Rgb(0x16, 0x16, 0x16);
                Surface    = Rgb(0x1C, 0x1C, 0x1C);
                SurfaceAlt = Rgb(0x23, 0x23, 0x23);
                SurfaceHi  = Rgb(0x2D, 0x2D, 0x2D);
                Stroke     = Rgb(0x32, 0x32, 0x32);
                StrokeSoft = Rgb(0x24, 0x24, 0x24);
                Text       = Rgb(0xF0, 0xF0, 0xF2);
                TextMuted  = Rgb(0x9A, 0x9A, 0xA0);
                TextFaint  = Rgb(0x60, 0x60, 0x68);
                AccentMain = Rgb(0x9F, 0x7B, 0xC1);
                AccentHi   = Rgb(0xB8, 0x96, 0xE4);
                AccentDim  = Rgb(0x3D, 0x2B, 0x52);
                Ok         = Rgb(0x57, 0xF2, 0x87); OkDim  = Rgb(0x18, 0x3A, 0x24);
                Warn       = Rgb(0xFE, 0xE7, 0x5C); WarnDim = Rgb(0x3E, 0x38, 0x16);
                Err        = Rgb(0xED, 0x42, 0x45); ErrDim = Rgb(0x3E, 0x18, 0x1A);
            }
        }


        public static void Apply(ThemeMode mode)
        {
            Mode = mode;
            if (mode == ThemeMode.Light) SetLightPalette();
            else if (mode == ThemeMode.Amoled) SetAmoledPalette();
            else if (mode == ThemeMode.Sunset) SetSunsetPalette();

            else if (mode == ThemeMode.System24) SetSystem24Palette();
            else SetDarkPalette();

            // Пресеты акцента применяются поверх палитры темы. В System24 нет —
            // там акцент задают подтемы-флейворы (SetSystem24Palette/Flavor*).
            if (mode != ThemeMode.System24)
            {
                var ap = FindAccent(Core.Get("accent", null));
                if (ap != null)
                {
                    AccentMain = ap.Main;
                    AccentHi = ap.Hi;
                    // Тёмная вариация для шиммера карточек — смесь с фоном темы,
                    // как в палитрах (автоматически подходит и светлым темам).
                    AccentDim = Blend(ap.Main, BgBase, 0.6);
                }
            }

            UiFont = (mode == ThemeMode.System24) ? System24Font : (_downloadedUiFont ?? DefaultUiFont);

            BrBgDeep.Color = BgDeep;   BrBgBase.Color = BgBase;
            BrSurface.Color = Surface; BrSurfaceAlt.Color = SurfaceAlt; BrSurfaceHi.Color = SurfaceHi;
            BrStroke.Color = Stroke;   BrStrokeSoft.Color = StrokeSoft;
            BrText.Color = Text;       BrMuted.Color = TextMuted; BrFaint.Color = TextFaint;
            BrAccent.Color = AccentMain; BrAccentHi.Color = AccentHi;
            BrOk.Color = Ok; BrWarn.Color = Warn; BrErr.Color = Err; BrInfo.Color = Info;
            BrOnAccent.Color = (mode == ThemeMode.System24) ? BgDeep : Colors.White;
            InstallScrollBarStyle();
            if (ThemeChanged != null) ThemeChanged();
        }

        // ---- Кастомизация: акцентные пресеты и плотность интерфейса ----

        // Пресеты акцента поверх палитры любой темы. Пусто или "default" в
        // конфиге — родной акцент темы. В System24 не применяются: там акцент
        // задают подтемы-флейворы.
        public sealed class AccentPreset
        {
            public readonly string Key;
            public readonly Color Main, Hi;
            public AccentPreset(string key, Color main, Color hi) { Key = key; Main = main; Hi = hi; }
        }

        public static readonly AccentPreset[] Accents =
        {
            new AccentPreset("blue",  Rgb(0x5B, 0x8D, 0xF6), Rgb(0x8F, 0xB4, 0xFF)),
            new AccentPreset("green", Rgb(0x35, 0xC7, 0x7A), Rgb(0x63, 0xE0, 0x9E)),
            new AccentPreset("amber", Rgb(0xF2, 0xA9, 0x3B), Rgb(0xFF, 0xC4, 0x66)),
            new AccentPreset("pink",  Rgb(0xE8, 0x5A, 0xA7), Rgb(0xF7, 0x8B, 0xC5)),
            new AccentPreset("red",   Rgb(0xEC, 0x4E, 0x4E), Rgb(0xFF, 0x7A, 0x7A))
        };

        public static AccentPreset FindAccent(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string k = key.Trim();
            if (string.Equals(k, "default", StringComparison.OrdinalIgnoreCase)) return null;
            foreach (var a in Accents)
                if (string.Equals(a.Key, k, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        // Незнакомый акцент (правленый конфиг или убранный в новой версии пресет)
        // заменяем на «как в теме» и разово пишем в файл — без тихой подстановки
        // на каждом запуске.
        public static void NormalizeSavedAccent()
        {
            string raw = Core.Get("accent", null);
            if (string.IsNullOrEmpty(raw)) return;
            if (string.Equals(raw.Trim(), "default", StringComparison.OrdinalIgnoreCase)) return;
            if (FindAccent(raw) != null) return;
            Core.Set("accent", "default");
            Core.SaveConfig();
            Core.Info(Loc.T("accent.migrated"));
        }

        // Плотность: «Компактный интерфейс» (настройки → Интерфейс) множит
        // отступы карточек, кнопок, плашек и вертикальные зазоры — для длинных
        // списков (стратегии). Шрифты и строки Журнала не масштабируются.
        public static double Density { get { return Core.GetBool("compact_ui", false) ? 0.72 : 1.0; } }

        public static Thickness Dense(Thickness t)
        {
            double d = Density;
            if (d == 1.0) return t;
            return new Thickness(t.Left * d, t.Top * d, t.Right * d, t.Bottom * d);
        }

        public static double Dense(double v)
        {
            double d = Density;
            return d == 1.0 ? v : v * d;
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
"<Setter Property='Width' Value='Auto'/><Setter Property='Height' Value='11'/>" +
// Для горизонтальной ориентации направление трека инвертировать нельзя.
"<Setter TargetName='PART_Track' Property='Track.IsDirectionReversed' Value='False'/>" +
"</Trigger>" +
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

        public static Color Blend(Color a, Color b, double t)
        {
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        // Цикл кнопки темы в шапке: идёт по реестру Modes, поэтому добавление или
        // удаление темы не требует правки метода.
        public static ThemeMode NextMode()
        {
            int i = IndexOf(Mode);
            if (i < 0) return Modes[0];
            return Modes[(i + 1) % Modes.Length];
        }

        public static void ToggleMode()
        {
            Apply(NextMode());
        }

        // До первого скачивания доступны системные fallback-шрифты. После успешной
        // проверки SHA-256 Core.ConfigureUiFonts подменяет семейство интерфейса на
        // выбранное пользователем (по умолчанию Google Sans), а моно — на Google
        // Sans Code из папки utils\fonts.
        public static readonly FontFamily DefaultUiFont = new FontFamily("Google Sans, Google Sans Text, Product Sans, Segoe UI");
        public static readonly FontFamily System24Font = new FontFamily("DM Mono, Cascadia Mono, Cascadia Code, Consolas, Courier New");
        static FontFamily _downloadedUiFont = null;
        public static FontFamily UiFont = DefaultUiFont;
        public static FontFamily MonoFont = new FontFamily("Google Sans Code, Cascadia Mono, Consolas");

        public static bool ConfigureDownloadedFonts(string fontDirectory, string uiFamilyName)
        {
            try
            {
                if (string.IsNullOrEmpty(fontDirectory) || !System.IO.Directory.Exists(fontDirectory)) return false;
                string dir = System.IO.Path.GetFullPath(fontDirectory);
                if (!dir.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString())) dir += System.IO.Path.DirectorySeparatorChar;
                var uri = new Uri(dir, UriKind.Absolute);
                // uiFamilyName — имя семейства из манифеста Core.FontFamilies (Title);
                // файлы семейства WPF находит среди всех ttf папки по этому фрагменту.
                _downloadedUiFont = new FontFamily(uri, "./#" + uiFamilyName);
                MonoFont = new FontFamily(uri, "./#Google Sans Code");
                if (Mode != ThemeMode.System24) UiFont = _downloadedUiFont;
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

        public static CornerRadius R6   { get { return Mode == ThemeMode.System24 ? new CornerRadius(0) : new CornerRadius(6); } }
        public static CornerRadius R8   { get { return Mode == ThemeMode.System24 ? new CornerRadius(0) : new CornerRadius(8); } }
        public static CornerRadius R10  { get { return Mode == ThemeMode.System24 ? new CornerRadius(0) : new CornerRadius(10); } }
        public static CornerRadius R12  { get { return Mode == ThemeMode.System24 ? new CornerRadius(0) : new CornerRadius(12); } }
        public static CornerRadius R14  { get { return Mode == ThemeMode.System24 ? new CornerRadius(0) : new CornerRadius(14); } }
        public static CornerRadius Rpill{ get { return Mode == ThemeMode.System24 ? new CornerRadius(0) : new CornerRadius(999); } }

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
        public const string Pause = "M10 4v16 M14 4v16";
        public const string Stop = "M12 22a10 10 0 1 0 0-20 10 10 0 0 0 0 20z M9 9h6v6H9z";
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
        public const string Minimize = "M5 12h14";
        public const string Maximize = "M4 4h16v16H4z";
        public const string Restore = "M4 4h11v11H4z M8 8h12v12H8z";
        public const string Lantern = "M12 2a5 5 0 0 0-5 5v3a5 5 0 0 0 10 0V7a5 5 0 0 0-5-5z M9 18h6 M10 22h4 M12 7v5";
        public const string Terminal = "M4 17l6-6-6-6 M12 19h8";
        public const string Trash = "M3 6h18 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2 M10 11v6 M14 11v6";
        public const string Tool = "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z";
        public const string Alert = Warn;
        public const string Discord = "M18 6h0a14.5 14.5 0 0 0-4-1.3 10.7 10.7 0 0 0-.5 1.1 13.5 13.5 0 0 0-4 0 10.7 10.7 0 0 0-.5-1.1A14.5 14.5 0 0 0 5 6a15.8 15.8 0 0 0-2 10.7 14.8 14.8 0 0 0 4.5 2.3 11 11 0 0 0 1-1.6 9.6 9.6 0 0 1-1.6-.8l.4-.3c3.1 1.5 6.5 1.5 9.6 0l.4.3a9.6 9.6 0 0 1-1.6.8 11 11 0 0 0 1 1.6 14.8 14.8 0 0 0 4.5-2.3A15.8 15.8 0 0 0 19 6z M8.5 14c-.8 0-1.5-.7-1.5-1.5s.7-1.5 1.5-1.5 1.5.7 1.5 1.5-.7 1.5-1.5 1.5zm7 0c-.8 0-1.5-.7-1.5-1.5s.7-1.5 1.5-1.5 1.5.7 1.5 1.5-.7 1.5-1.5 1.5z";
        public const string YouTube = "M22.54 6.42a2.78 2.78 0 0 0-1.94-2C18.88 4 12 4 12 4s-6.88 0-8.6.46a2.78 2.78 0 0 0-1.94 2A29 29 0 0 0 1 11.75a29 29 0 0 0 .46 5.33 2.78 2.78 0 0 0 1.94 2c1.72.46 8.6.46 8.6.46s6.88 0 8.6-.46a2.78 2.78 0 0 0 1.94-2 29 29 0 0 0 .46-5.25 29 29 0 0 0-.46-5.33z M9.75 15.02l5.75-3.27-5.75-3.27v6.54z";
        public const string Palette = "M12 2C6.5 2 2 6.5 2 12s4.5 10 10 10c.926 0 1.648-.746 1.648-1.688 0-.437-.18-.835-.437-1.125-.29-.289-.438-.652-.438-1.125a1.64 1.64 0 0 1 1.668-1.668h1.996c3.051 0 5.555-2.503 5.555-5.554C21.965 6.012 17.461 2 12 2z M6 12h.01 M8 7h.01 M13 6h.01 M17 10h.01";
    }

    static class UI
    {
        public static Geometry ParseGeometry(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            string pathData = data.StartsWith("F:") ? data.Substring(2) : data;
            try { return Geometry.Parse(pathData); } catch { return null; }
        }

        public static void UpdateIcon(System.Windows.Shapes.Path p, string data, Brush stroke = null, double thickness = 1.8)
        {
            if (p == null || string.IsNullOrEmpty(data)) return;
            bool isFill = data.StartsWith("F:");
            string pathData = isFill ? data.Substring(2) : data;
            try
            {
                p.Data = Geometry.Parse(pathData);
                if (stroke != null)
                {
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
                }
                else
                {
                    if (isFill)
                    {
                        if (p.Fill == null || p.Fill == Brushes.Transparent)
                        {
                            p.Fill = p.Stroke ?? Theme.BrText;
                            p.Stroke = null;
                        }
                    }
                    else
                    {
                        if (p.Stroke == null)
                        {
                            p.Stroke = p.Fill ?? Theme.BrText;
                            p.Fill = Brushes.Transparent;
                            p.StrokeThickness = thickness;
                            p.StrokeStartLineCap = PenLineCap.Round;
                            p.StrokeEndLineCap = PenLineCap.Round;
                            p.StrokeLineJoin = PenLineJoin.Round;
                        }
                    }
                }
            }
            catch { }
        }

        public static System.Windows.Shapes.Path Icon(string data, double size, Brush stroke, double thickness = 1.8)
        {
            if (string.IsNullOrEmpty(data)) return new System.Windows.Shapes.Path();

            bool isFill = data.StartsWith("F:");
            string pathData = isFill ? data.Substring(2) : data;

            var p = new System.Windows.Shapes.Path
            {
                Data = ParseGeometry(pathData),
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
                if (Theme.Mode == ThemeMode.System24 && type != IconAnimType.Rotate360) return;
                switch (type)
                {
                    case IconAnimType.ScaleBounce:
                    {
                        var anim = new DoubleAnimation(1.0, 1.15, TimeSpan.FromMilliseconds(150))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
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

        // Подсветка карточки под курсором: круглое пятно света в фоне и такая же
        // круглая дуга на рамке. Радиусы задаются в долях bounding box, поэтому без
        // поправки на отношение сторон пятно превращается в эллипс на всю ширину
        // карточки — визуально это выглядело как «светится прямоугольная рамка».
        public static void AttachSpotlight(Border b, Func<Brush> getDefaultBrush = null, Func<Color> getOuterColor = null)
        {
            if (b == null) return;
            RadialGradientBrush spotBrush = null;
            RadialGradientBrush bgSpotBrush = null;
            Brush origBg = null;

            // Радиус пятна на рамке и в фоне (по вертикали, в долях высоты).
            const double RingR = 0.30, BgR = 0.46;

            Action<double, double> setRadii = delegate (double w, double h)
            {
                if (spotBrush == null || bgSpotBrush == null) return;
                double aspect = w > 0 && h > 0 ? h / w : 1.0;
                spotBrush.RadiusY = RingR;
                spotBrush.RadiusX = RingR * aspect;
                bgSpotBrush.RadiusY = BgR;
                bgSpotBrush.RadiusX = BgR * aspect;
            };

            b.MouseEnter += (s, e) =>
            {
                if (!Theme.AnimationsEnabled || Theme.Mode == ThemeMode.System24) return;
                origBg = b.Background;
                if (spotBrush == null)
                {
                    spotBrush = new RadialGradientBrush
                    {
                        MappingMode = BrushMappingMode.RelativeToBoundingBox,
                        RadiusX = 0.45,
                        RadiusY = 0.45
                    };
                }
                if (bgSpotBrush == null)
                {
                    bgSpotBrush = new RadialGradientBrush
                    {
                        MappingMode = BrushMappingMode.RelativeToBoundingBox,
                        RadiusX = 0.55,
                        RadiusY = 0.55
                    };
                }
                Color outer = getOuterColor != null ? getOuterColor() : Theme.Stroke;
                spotBrush.GradientStops.Clear();
                spotBrush.GradientStops.Add(new GradientStop(Color.FromArgb(235, Theme.AccentHi.R, Theme.AccentHi.G, Theme.AccentHi.B), 0.0));
                spotBrush.GradientStops.Add(new GradientStop(Color.FromArgb(120, Theme.AccentMain.R, Theme.AccentMain.G, Theme.AccentMain.B), 0.55));
                spotBrush.GradientStops.Add(new GradientStop(outer, 1.0));

                Color bgCenter = Theme.Blend(Theme.SurfaceHi, Theme.AccentHi, 0.16);
                Color bgEdge = Theme.SurfaceHi;
                bgSpotBrush.GradientStops.Clear();
                bgSpotBrush.GradientStops.Add(new GradientStop(bgCenter, 0.0));
                bgSpotBrush.GradientStops.Add(new GradientStop(bgEdge, 0.85));

                var pos = e.GetPosition(b);
                var relX = b.ActualWidth > 0 ? pos.X / b.ActualWidth : 0.5;
                var relY = b.ActualHeight > 0 ? pos.Y / b.ActualHeight : 0.5;
                var relPos = new Point(relX, relY);
                setRadii(b.ActualWidth, b.ActualHeight);
                spotBrush.Center = relPos;
                spotBrush.GradientOrigin = relPos;
                bgSpotBrush.Center = relPos;
                b.BorderBrush = spotBrush;
                b.Background = bgSpotBrush;
            };
            b.MouseMove += (s, e) =>
            {
                if (!Theme.AnimationsEnabled || Theme.Mode == ThemeMode.System24) return;
                var pos = e.GetPosition(b);
                var relX = b.ActualWidth > 0 ? pos.X / b.ActualWidth : 0.5;
                var relY = b.ActualHeight > 0 ? pos.Y / b.ActualHeight : 0.5;
                var relPos = new Point(relX, relY);
                // Размер карточки мог измениться после наведения (перенос подписи,
                // прокрутка) — круг нужно держать кругом.
                setRadii(b.ActualWidth, b.ActualHeight);
                if (spotBrush != null && b.BorderBrush == spotBrush)
                {
                    spotBrush.Center = relPos;
                    spotBrush.GradientOrigin = relPos;
                }
                if (bgSpotBrush != null && b.Background == bgSpotBrush)
                {
                    bgSpotBrush.Center = relPos;
                    bgSpotBrush.GradientOrigin = relPos;
                }
            };
            b.MouseLeave += (s, e) =>
            {
                if (Theme.Mode == ThemeMode.System24) return;
                b.BorderBrush = getDefaultBrush != null ? getDefaultBrush() : Theme.BrStroke;
                b.Background = origBg ?? Theme.BrSurface;
            };
        }

        public static Border Card(UIElement child, Thickness? pad = null, CornerRadius? radius = null, Brush bg = null)
        {
            var b = new Border
            {
                Background = bg ?? Theme.BrSurface,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = radius ?? Theme.R12,
                Padding = Theme.Dense(pad ?? new Thickness(18)),
                Child = child
            };
            if (Theme.Mode == ThemeMode.System24)
            {
                b.MouseEnter += (s, e) =>
                {
                    if (Theme.Mode != ThemeMode.System24) return;
                    if (Theme.AnimationsEnabled)
                    {
                        var anim = new ColorAnimation(Theme.Stroke, Theme.AccentMain, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        var brush = new SolidColorBrush(Theme.Stroke);
                        b.BorderBrush = brush;
                        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                    }
                    else b.BorderBrush = Theme.BrAccent;
                };
                b.MouseLeave += (s, e) =>
                {
                    if (Theme.Mode != ThemeMode.System24) return;
                    if (Theme.AnimationsEnabled)
                    {
                        var anim = new ColorAnimation(Theme.AccentMain, Theme.Stroke, TimeSpan.FromMilliseconds(200))
                        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                        var brush = new SolidColorBrush(Theme.AccentMain);
                        b.BorderBrush = brush;
                        brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                    }
                    else b.BorderBrush = Theme.BrStroke;
                };
            }
            else
            {
                AttachSpotlight(b, () => Theme.BrStroke, null);
            }
            return b;
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
                case Sev.Info: return Theme.BrInfo;
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
                case Sev.Info: return Theme.Info;
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
        // Единый фон для всех — различия только в цвете текста/иконки и рамки.
        public static Button Button(string text, string iconData, int kind)
        {
            Brush bg = Theme.Alpha(Theme.Text, 8);
            Brush fg, brd;
            switch (kind)
            {
                case 0:  fg = Theme.BrAccent; brd = Theme.BrStroke; break;
                case 2:  fg = Theme.BrErr;    brd = Theme.BrStroke; break;
                default: fg = Theme.BrText;   brd = Theme.BrStroke; break;
            }

            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center };
            var border = new Border
            {
                Background = bg, BorderBrush = brd, BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10, Padding = Theme.Dense(new Thickness(14, 9, 14, 9)), Child = sp
            };
            Border contentHost;
            var contentRoot = SplitSurface(border, out contentHost);

            var b = new Button { Content = contentRoot, Cursor = System.Windows.Input.Cursors.Hand, Focusable = true };

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
            AttachHoverGlow(b, border);
            b.Tag = new object[] { border, bg, kind, contentHost };
            b.MouseEnter += (s, e) =>
            {
                var tag = b.Tag as object[];
                if (tag != null)
                {
                    var curBd = tag[0] as Border;
                    var curBg = tag[1] as Brush;
                    int curKind = (int)tag[2];
                    if (curBd != null)
                    {
                        curBd.Background = Hover(curBg, curKind);
                        if (Theme.Mode == ThemeMode.System24)
                        {
                            Color targetC = (curKind == 2) ? Theme.Err : Theme.AccentMain;
                            if (Theme.AnimationsEnabled)
                            {
                                var anim = new ColorAnimation(Theme.Stroke, targetC, TimeSpan.FromMilliseconds(200))
                                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                                var brush = new SolidColorBrush(Theme.Stroke);
                                curBd.BorderBrush = brush;
                                brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                            }
                            else curBd.BorderBrush = (curKind == 2) ? Theme.BrErr : Theme.BrAccent;
                        }
                    }
                }
                else
                {
                    border.Background = Hover(bg, kind);
                    if (Theme.Mode == ThemeMode.System24) border.BorderBrush = (kind == 2) ? Theme.BrErr : Theme.BrAccent;
                }
            };
            b.MouseLeave += (s, e) =>
            {
                var tag = b.Tag as object[];
                if (tag != null)
                {
                    var curBd = tag[0] as Border;
                    var curBg = tag[1] as Brush;
                    if (curBd != null)
                    {
                        curBd.Background = curBg;
                        if (Theme.Mode == ThemeMode.System24)
                        {
                            int curKind = (int)tag[2];
                            if (curKind == 0) curBd.BorderBrush = Theme.BrAccent;
                            else if (Theme.AnimationsEnabled)
                            {
                                Color startC = (curKind == 2) ? Theme.Err : Theme.AccentMain;
                                var anim = new ColorAnimation(startC, Theme.Stroke, TimeSpan.FromMilliseconds(200))
                                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                                var brush = new SolidColorBrush(startC);
                                curBd.BorderBrush = brush;
                                brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
                            }
                            else curBd.BorderBrush = Theme.BrStroke;
                        }
                    }
                }
                else
                {
                    border.Background = bg;
                    if (Theme.Mode == ThemeMode.System24) border.BorderBrush = (kind == 0) ? Theme.BrAccent : Theme.BrStroke;
                }
            };
            b.GotKeyboardFocus += (s, e) =>
            {
                var tag = b.Tag as object[];
                var curBd = (tag != null ? tag[0] as Border : null) ?? border;
                SetFocusRing(curBd, true);
                b.BringIntoView();
            };
            b.LostKeyboardFocus += (s, e) =>
            {
                var tag = b.Tag as object[];
                var curBd = (tag != null ? tag[0] as Border : null) ?? border;
                int curKind = tag != null ? (int)tag[2] : kind;
                SetFocusRing(curBd, false);
                if (curBd != null)
                {
                    curBd.BorderBrush = (curKind == 0) ? Theme.BrAccent : Theme.BrStroke;
                    if (b.IsMouseOver) SetGlow(curBd, true, false);
                }
            };
            AutomationSetName(b, text ?? Loc.T("common.button"));
            return b;
        }

        // Кольцо фокуса с клавиатуры НЕ должно влиять на разметку. Раньше ставили
        // BorderThickness 2: содержимое кнопки смещалось на 1px, а у Toggle это ещё
        // и ломало ход ручки, посчитанный от рамки толщиной 1 — переключатель
        // «уезжал не туда на пару пикселей», пока не потерял фокус.
        public static void SetFocusRing(Border bd, bool on)
        {
            if (bd == null) return;
            bd.BorderThickness = new Thickness(1);
            if (on)
            {
                bd.BorderBrush = Theme.BrAccentHi;
                SetGlow(bd, true, true);
            }
            else bd.Effect = null;
        }

        // Акцентный неон. Эффект вешается только на заливку БЕЗ содержимого:
        // DropShadowEffect растрирует всё поддерево, и текст с иконками внутри
        // мутнел. Заливка и контент разделены через SplitSurface().
        public static void SetGlow(Border bd, bool on, bool focused)
        {
            if (bd == null) return;
            if (!on || !Theme.AnimationsEnabled)
            {
                bd.Effect = null;
                return;
            }
            var old = bd.Effect as DropShadowEffect;
            if (old != null && Math.Abs(old.Opacity - (focused ? 0.85 : 0.5)) < 0.01) return;
            bd.Effect = new DropShadowEffect
            {
                Color = Theme.AccentHi,
                BlurRadius = focused ? 10 : 9,
                ShadowDepth = 0,
                Opacity = focused ? 0.85 : 0.5
            };
        }

        public static void AttachHoverGlow(Button b, Border bd)
        {
            if (b == null || bd == null) return;
            b.MouseEnter += (s, e) => { if (!b.IsKeyboardFocusWithin) SetGlow(bd, true, false); };
            b.MouseLeave += (s, e) => { if (!b.IsKeyboardFocusWithin) SetGlow(bd, false, false); };
            b.LostKeyboardFocus += (s, e) => { if (b.IsMouseOver) SetGlow(bd, true, false); };
        }

        public static void AttachGlow(Button b, Border bd)
        {
            if (b == null || bd == null) return;
            AttachHoverGlow(b, bd);
            b.GotKeyboardFocus += (s, e) => SetGlow(bd, true, true);
        }

        // Разделяет кнопку на заливку (surface) и содержимое (contentHost).
        // Тень живёт на заливке без детей, поэтому подпись и иконки остаются
        // чёткими. Отступы и толщина рамки переносятся на слой содержимого,
        // чтобы разметка не сдвинулась ни на пиксель.
        public static Grid SplitSurface(Border surface)
        {
            Border unused;
            return SplitSurface(surface, out unused);
        }

        public static Grid SplitSurface(Border surface, out Border contentHost)
        {
            contentHost = null;
            if (surface == null) return null;
            var child = surface.Child;
            var pad = surface.Padding;
            var bt = surface.BorderThickness;
            // Заливка должна заполнить весь узел. Если оставить ей Left/Right
            // от исходной пилюли, она сжимается до толщины рамки: пропадает фон,
            // а кликабельной остаётся тонкая полоска у края.
            var ha = surface.HorizontalAlignment;
            var va = surface.VerticalAlignment;
            surface.Child = null;
            surface.Padding = new Thickness(0);
            surface.HorizontalAlignment = HorizontalAlignment.Stretch;
            surface.VerticalAlignment = VerticalAlignment.Stretch;

            contentHost = new Border
            {
                Padding = pad,
                BorderThickness = bt,
                BorderBrush = Brushes.Transparent,
                Child = child
            };

            var root = new Grid
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = ha,
                VerticalAlignment = va
            };
            root.Children.Add(surface);
            root.Children.Add(contentHost);
            return root;
        }

        public static Border Surface(Button b)
        {
            var tag = b != null ? b.Tag as object[] : null;
            var s = tag != null && tag.Length > 0 ? tag[0] as Border : null;
            return s ?? (b != null ? b.Content as Border : null);
        }

        public static Border ContentHost(Button b)
        {
            var tag = b != null ? b.Tag as object[] : null;
            return tag != null && tag.Length > 3 ? tag[3] as Border : null;
        }

        // Ряд кнопок во всю ширину блока: равные колонки (тот же CellRow, что и у
        // карточек), край ряда совпадает с краем блока над ним, ряд всегда один —
        // при нехватке места подпись переносится ВНУТРИ кнопки. Плотная группировка
        // по ширине подписи была опробована и отвергнута: ряд «съезжал» влево,
        // у краёв оставалось пустое место, и он не соответствовал ширине блока.
        public static CellRow ButtonRow(params FrameworkElement[] items)
        {
            var row = new CellRow { Gap = Theme.Dense(12), EvenRows = false, MinCell = 0,
                Margin = new Thickness(0, 0, 0, Theme.Dense(12)) };
            int n = 0;
            foreach (var it in items)
            {
                if (it == null) continue;
                it.Margin = new Thickness(0);
                PrepareRowItem(it);
                row.Children.Add(it);
                n++;
            }
            row.ForceCols = n;
            return row;
        }

        // Набор рядов кнопок с ОДИНАКОВОЙ шириной ячейки. Один вызов, а не несколько
        // ButtonRow, потому что общая ширина считается по самому длинному ряду:
        // отдельно взятый CellRow про соседний ряд ничего не знает. Без этого ряд
        // из трёх кнопок делит блок на три колонки и выглядит на треть шире ряда
        // из четырёх над ним — «карточки разными по размеру».
        public static Panel ButtonRows(params FrameworkElement[][] rows)
        {
            int max = 0, live = 0;
            foreach (var r in rows)
            {
                if (r == null) continue;
                int n = 0;
                foreach (var it in r) if (it != null) n++;
                if (n == 0) continue;
                live++;
                if (n > max) max = n;
            }

            var sp = new StackPanel();
            int idx = 0;
            foreach (var r in rows)
            {
                var row = ButtonRow(r);
                if (row.Children.Count == 0) continue;
                idx++;
                row.UniformCols = max;
                row.Margin = new Thickness(0, 0, 0, Theme.Dense(idx == live ? 12 : 6));
                sp.Children.Add(row);
            }
            return sp;
        }

        // Готовит кнопку к ряду: иконка + переносящаяся подпись в Grid (Auto/Star).
        // Отдаёт TextBlock подписи — по нему CellRow меряет естественную ширину.
        static void PrepareRowItem(FrameworkElement fe)
        {
            var b = fe as Button;
            if (b == null) return;
            var bd = Surface(b);
            if (bd == null) return;
            var box = ContentHost(b);
            var sp = (box != null ? box.Child : bd.Child) as StackPanel;
            if (sp == null) return;
            if (box != null) box.Padding = new Thickness(10, 9, 10, 9);
            else bd.Padding = new Thickness(10, 9, 10, 9);

            UIElement icon = null;
            TextBlock label = null;
            foreach (UIElement ch in sp.Children)
            {
                var tb = ch as TextBlock;
                if (tb != null && label == null) label = tb;
                else if (icon == null) icon = ch;
            }

            // StackPanel меряет детей при бесконечной ширине, поэтому переноса в
            // нём не бывает вовсе. Переставляем в Grid: иконка Auto, подпись Star.
            // Узел-иконку переносим существующий, чтобы не потерять hover-анимацию.
            var g = new Grid { HorizontalAlignment = HorizontalAlignment.Left };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sp.Children.Clear();
            if (icon != null)
            {
                var ie = icon as FrameworkElement;
                if (ie != null)
                {
                    // По вертикали — по центру строки. Был Top с отступом 2px:
                    // пока подпись в одну строку, значок выглядел заданным вверх.
                    ie.VerticalAlignment = VerticalAlignment.Center;
                    ie.Margin = new Thickness(0, 0, label != null ? 8 : 0, 0);
                }
                Grid.SetColumn(icon, 0);
                g.Children.Add(icon);
            }
            if (label != null)
            {
                label.TextWrapping = TextWrapping.Wrap;
                label.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(label, 1);
                g.Children.Add(label);
            }
            if (box != null) box.Child = g;
            else bd.Child = g;
        }

        // Хост содержимого кнопки: Grid (в ряду кнопок, подпись переносится) или
        // StackPanel (обычная кнопка). Тип сохраняется, чтобы SetButton мог
        // перерисовать кнопку из ряда, не потеряв перенос.
        static Panel ContentPanel(Button b)
        {
            var box = ContentHost(b);
            if (box != null)
            {
                var p0 = box.Child as Panel;
                if (p0 != null) return p0;
                var sp0 = new StackPanel { Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                box.Child = sp0;
                return sp0;
            }
            var bd = Surface(b);
            if (bd == null) return null;
            var p = bd.Child as Panel;
            if (p != null) return p;
            if (bd.Child == null)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                bd.Child = sp;
                return sp;
            }
            return null;
        }

        static void FillContent(Panel host, Button b, string iconData, string text, Brush fg)
        {
            if (host == null) return;
            var g = host as Grid;
            host.Children.Clear();
            if (g != null && g.ColumnDefinitions.Count == 0)
            {
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
            if (iconData != null)
            {
                var ic = UI.Icon(iconData, 16, fg, 1.8);
                ic.VerticalAlignment = g != null ? VerticalAlignment.Top : VerticalAlignment.Center;
                ic.Margin = new Thickness(0, g != null ? 2 : 0, text != null ? 8 : 0, 0);
                if (g != null) Grid.SetColumn(ic, 0);
                host.Children.Add(ic);
                var animType = (iconData == Icons.Restart || iconData == Icons.Refresh) ? IconAnimType.Rotate360 :
                               (iconData == Icons.Pulse) ? IconAnimType.Pulse :
                               (iconData == Icons.Gear) ? IconAnimType.Rotate90 :
                               (iconData == Icons.Music) ? IconAnimType.Wiggle : IconAnimType.ScaleBounce;
                UI.AttachIconHoverAnimation(b, ic, animType);
            }
            if (text != null)
            {
                var tb = new TextBlock { Text = text, Foreground = fg, FontSize = Theme.FsBody,
                    FontFamily = Theme.UiFont, FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center };
                if (g != null)
                {
                    tb.TextWrapping = TextWrapping.Wrap;
                    Grid.SetColumn(tb, 1);
                }
                host.Children.Add(tb);
            }
        }

        public static void SetButtonText(Button b, string text)
        {
            try
            {
                var host = ContentPanel(b);
                if (host == null) return;
                foreach (var child in host.Children)
                {
                    var tb = child as TextBlock;
                    if (tb != null) { tb.Text = text; return; }
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

                var border = Surface(b);
                if (border != null)
                {
                    border.Background = bg;
                    border.BorderBrush = brd;
                    FillContent(ContentPanel(b), b, iconData, text, fg);
                    b.Tag = new object[] { border, bg, kind, ContentHost(b) };
                }
                AutomationSetName(b, text ?? Loc.T("common.button"));
            }
            catch { }
        }

        static Brush Hover(Brush baseBg, int kind)
        {
            return Theme.Alpha(Theme.Text, 20);
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
                if (Theme.Mode == ThemeMode.System24)
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    scale.ScaleX = 1;
                    scale.ScaleY = 1;
                    return;
                }
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
            box.SetValue(Border.CornerRadiusProperty, Theme.Mode == ThemeMode.System24 ? new CornerRadius(0) : new CornerRadius(5));
            box.SetValue(Border.BorderThicknessProperty, new Thickness(1.6));
            box.SetValue(Border.BorderBrushProperty, Theme.Frozen(Theme.TextFaint));
            box.SetValue(Border.BackgroundProperty, Theme.Frozen(Theme.SurfaceAlt));
            box.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var check = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "tick");
            check.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse(Icons.Check));
            check.SetValue(System.Windows.Shapes.Path.StretchProperty, Stretch.Uniform);
            check.SetValue(System.Windows.Shapes.Path.WidthProperty, 10.0);
            check.SetValue(System.Windows.Shapes.Path.HeightProperty, 7.5);
            check.SetValue(System.Windows.Shapes.Path.StrokeProperty, Theme.Frozen(Theme.Mode == ThemeMode.System24 ? Theme.BgDeep : Colors.White));
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
            return Make(UI2.SevColor(sev), text, sev == Sev.Progress);
        }

        public static Border Make(Color color, string text, bool isProgress = false)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (isProgress)
            {
                var ring = new System.Windows.Shapes.Ellipse
                {
                    Width = 9, Height = 9, Stroke = Theme.Frozen(color), StrokeThickness = 1.8,
                    StrokeDashArray = new DoubleCollection { 3, 2 },
                    VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0),
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };
                if (Theme.AnimationsEnabled && Theme.Mode != ThemeMode.System24)
                {
                    var rt = new RotateTransform(0);
                    ring.RenderTransform = rt;
                    var rotAnim = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(950))
                    {
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    rt.BeginAnimation(RotateTransform.AngleProperty, rotAnim);
                }
                sp.Children.Add(ring);
            }
            else
            {
                if (Theme.Mode == ThemeMode.System24)
                {
                    var sq = new System.Windows.Shapes.Rectangle
                    {
                        Width = 7, Height = 7, Fill = Theme.Frozen(color),
                        VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
                    };
                    sp.Children.Add(sq);
                }
                else if (color == Theme.Ok && Theme.AnimationsEnabled)
                {
                    var beaconGrid = new Grid
                    {
                        Width = 10, Height = 10,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 7, 0)
                    };
                    var wave = new System.Windows.Shapes.Ellipse
                    {
                        Width = 7, Height = 7,
                        Stroke = Theme.Frozen(color),
                        StrokeThickness = 1.2,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        RenderTransformOrigin = new Point(0.5, 0.5)
                    };
                    var st = new ScaleTransform(1, 1);
                    wave.RenderTransform = st;
                    var scaleAnim = new DoubleAnimation(1.0, 2.2, TimeSpan.FromMilliseconds(1600))
                    {
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                    };
                    var opacAnim = new DoubleAnimation(0.9, 0.0, TimeSpan.FromMilliseconds(1600))
                    {
                        RepeatBehavior = RepeatBehavior.Forever,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut }
                    };
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                    wave.BeginAnimation(UIElement.OpacityProperty, opacAnim);

                    var dot = new System.Windows.Shapes.Ellipse
                    {
                        Width = 7, Height = 7, Fill = Theme.Frozen(color),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    beaconGrid.Children.Add(wave);
                    beaconGrid.Children.Add(dot);
                    sp.Children.Add(beaconGrid);
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
            }
            sp.Children.Add(new TextBlock { Text = text, Foreground = Theme.Frozen(color),
                FontSize = Theme.FsSmall, FontWeight = FontWeights.SemiBold, FontFamily = Theme.UiFont,
                VerticalAlignment = VerticalAlignment.Center });

            var bd = new Border
            {
                Background = Theme.Alpha(color, 14), BorderBrush = Theme.Alpha(color, 55),
                BorderThickness = new Thickness(1), CornerRadius = Theme.R8,
                Padding = Theme.Dense(new Thickness(10, 4, 12, 4)), Child = sp,
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
    // В темах Dark/Light — скруглённый прямоугольник со сглаженной ручкой и мягкой тенью.
    // В теме System24 — строгий пиксельный ретро-стиль (CornerRadius 0, плоская ручка без размытой тени, акцентный ховер).
    class Toggle : System.Windows.Controls.Primitives.ToggleButton
    {
        Border _track;
        Border _knob;
        TranslateTransform _tx;
        Action _themeHandler;
        const double TrackW = 46, TrackH = 26, KnobD = 20, Inset = 3, BorderT = 1;
        // Ход ручки считается от ВНУТРЕННЕГО края рамки: без вычета двух бордюров
        // ручка в положении «вкл» вставала в 1px от края вместо 3px, то есть
        // асимметрично на те самые пару пикселей.
        readonly double _travel = TrackW - KnobD - Inset * 2 - BorderT * 2;

        public Toggle(string accessibleName)
        {
            Width = TrackW; Height = TrackH;
            Cursor = System.Windows.Input.Cursors.Hand;
            Focusable = true;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Center;
            SnapsToDevicePixels = true;

            _tx = new TranslateTransform(0, 0);
            // Ручка — сосед заливки, а не её ребёнок: иначе тень подсветки
            // растрирует и ручку, и она мутнеет.
            _knob = new Border
            {
                Width = KnobD, Height = KnobD,
                Background = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Inset + BorderT, 0, 0, 0),
                RenderTransform = _tx
            };
            _track = new Border
            {
                Width = TrackW, Height = TrackH,
                BorderThickness = new Thickness(1)
            };
            var root = new Grid();
            root.Children.Add(_track);
            root.Children.Add(_knob);

            Content = root;
            Template = MakeTemplate();
            FocusVisualStyle = Ctl.FocusStyle();
            Ctl.AddMotion(this);
            Checked += (s, e) => Render(true);
            Unchecked += (s, e) => Render(true);
            Ctl.AutomationSetName(this, accessibleName);

            _themeHandler = delegate { Render(false); };
            Loaded += (s, e) =>
            {
                Theme.ThemeChanged += _themeHandler;
                Render(false);
            };
            Unloaded += (s, e) =>
            {
                Theme.ThemeChanged -= _themeHandler;
            };
            MouseEnter += (s, e) =>
            {
                if (!IsKeyboardFocusWithin) Ctl.SetGlow(_track, true, false);
                if (IsChecked != true && Theme.Mode == ThemeMode.System24)
                    _track.BorderBrush = Theme.BrAccent;
            };
            MouseLeave += (s, e) =>
            {
                if (!IsKeyboardFocusWithin) Ctl.SetGlow(_track, false, false);
                if (IsChecked != true && Theme.Mode == ThemeMode.System24)
                    _track.BorderBrush = Theme.BrStroke;
            };
            GotKeyboardFocus += (s, e) =>
            {
                Ctl.SetFocusRing(_track, true);
                BringIntoView();
            };
            LostKeyboardFocus += (s, e) =>
            {
                Ctl.SetFocusRing(_track, false);
                Render(false);
                if (IsMouseOver) Ctl.SetGlow(_track, true, false);
            };
            KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                {
                    IsChecked = !IsChecked;
                    e.Handled = true;
                }
            };
            IsEnabledChanged += (s, e) =>
            {
                Opacity = IsEnabled ? 1.0 : 0.45;
            };
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
            bool isS24 = (Theme.Mode == ThemeMode.System24);
            _track.CornerRadius = isS24 ? new CornerRadius(0) : new CornerRadius(8);
            _knob.CornerRadius = isS24 ? new CornerRadius(0) : new CornerRadius(6);
            _knob.Effect = isS24 ? null : new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 1, Opacity = 0.35, Direction = 270 };

            bool on = IsChecked == true;
            _track.Background = on ? Theme.BrAccent : Theme.BrSurfaceHi;
            _track.BorderBrush = on ? Theme.BrAccent : Theme.BrStroke;
            _knob.Background = new SolidColorBrush(Colors.White);
            // Кольцо фокуса переживает перерисовку и смену темы: строкой выше
            // BorderBrush затирается, и у карточки с фокусом оставалась тонкая
            // рамка без акцента.
            if (IsKeyboardFocused) _track.BorderBrush = Theme.BrAccentHi;

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
