using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ZapretStudio
{
    class SettingsPage : Page
    {
        public override string Title { get { return Loc.T("settings.title"); } }
        public override string Subtitle { get { return Loc.T("settings.sub"); } }

        readonly MainWindow _win;
        Border _tgSection, _tgPillHost;
        TextBlock _tgHint;
        WrapPanel _tgChips;
        int _tgFlashGen;
        DispatcherTimer _tgScrollTimer, _tgRevealTimer;

        public SettingsPage(MainWindow win)
        {
            _win = win;
            BuildGeneral();
            BuildCheck();
            BuildDns();
            BuildInterface();
            BuildTgProxy();
            BuildPrivacy();
            BuildAntivirus();
            BuildMaintenance();
        }

        public override void OnShow()
        {
            RefreshTgSection();
        }

        public override void OnHide()
        {
            if (_tgRevealTimer != null) { _tgRevealTimer.Stop(); _tgRevealTimer = null; }
            if (_tgScrollTimer != null) { _tgScrollTimer.Stop(); _tgScrollTimer = null; }
        }

        Toggle Tog(string cfgKey, bool dflt, string accName, Action<bool> onChange)
        {
            var t = new Toggle(accName);
            t.IsChecked = Core.GetBool(cfgKey, dflt);
            t.Checked += (s, e) => { Core.SetBool(cfgKey, true); Core.SaveConfig(); if (onChange != null) onChange(true); };
            t.Unchecked += (s, e) => { Core.SetBool(cfgKey, false); Core.SaveConfig(); if (onChange != null) onChange(false); };
            return t;
        }

        void BuildGeneral()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.general")));
            Body.Children.Add(Row(Loc.T("settings.tray"), Loc.T("settings.tray.desc"),
                Tog("tray_on_close", true, Loc.T("settings.tray"), null)));
            Body.Children.Add(space());
            Body.Children.Add(Row(Loc.T("settings.notify"), Loc.T("settings.notify.desc"),
                Tog("notifications", true, Loc.T("settings.notify"), null)));
        }

        void BuildCheck()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.check")));
            var to = Combo(160);
            Ctl.AutomationSetName(to, Loc.T("settings.timeout"));
            foreach (var v in new[] { "3", "5", "8", "10" })
                to.Items.Add(v + " " + Loc.T("time.sec"));
            int savedTo = Core.GetInt("check_timeout_idx", 1);
            to.SelectedIndex = (savedTo >= 0 && savedTo < to.Items.Count) ? savedTo : 1;
            to.SelectionChanged += (s, e) => { Core.SetInt("check_timeout_idx", to.SelectedIndex); Core.SaveConfig(); };
            Body.Children.Add(Row(Loc.T("settings.timeout"), Loc.T("settings.timeout.desc"), to));
        }

        void BuildDns()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.dns")));
            Body.Children.Add(NoteCard(Icons.Info, Theme.BrAccent, Loc.T("settings.dns.note"), Sev.Info));
            var hosts = Core.HostsHasYouTube();
            Body.Children.Add(space());
            Body.Children.Add(Row(Loc.T("settings.hosts"),
                hosts ? Loc.T("settings.hosts.warn") : Loc.T("settings.hosts.ok"),
                Pill.Make(hosts ? Sev.Warn : Sev.Ok,
                    hosts ? Loc.T("settings.hosts.needAttention") : Loc.T("settings.hosts.clean"))));
            Body.Children.Add(space());
            var doh = new Toggle(Loc.T("filters.doh"));
            doh.IsChecked = Core.DohMode > 0;
            doh.Checked += (s, e) => { Core.DohMode = 1; Core.Info(Loc.T("doh.enabled")); };
            doh.Unchecked += (s, e) => { Core.DohMode = 0; Core.Info(Loc.T("doh.disabled")); };
            Body.Children.Add(Row(Loc.T("filters.doh"), Loc.T("filters.doh.desc"), doh));
        }

        void BuildInterface()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.interface")));

            var theme = Combo(180);
            theme.Tag = "setting_theme";
            Ctl.AutomationSetName(theme, Loc.T("settings.theme"));
            // Список и порядок берутся из реестра Theme.Modes: добавление или
            // удаление темы больше не требует правки этого метода.
            for (int i = 0; i < Theme.Modes.Length; i++) theme.Items.Add(Theme.Label(Theme.Modes[i]));
            int curTheme = Theme.IndexOf(Theme.Mode);
            theme.SelectedIndex = curTheme < 0 ? 0 : curTheme;
            theme.SelectionChanged += (s, e) =>
            {
                int idx = theme.SelectedIndex;
                if (idx < 0 || idx >= Theme.Modes.Length) return;
                ThemeMode next = Theme.Modes[idx];
                if (next == Theme.Mode) return;
                Core.Set("theme", Theme.Name(next));
                Core.SaveConfig();
                // Тема применяется фоном после закрытия дропдауна — см. комбо
                // шрифта: пересборка внутри SelectionChanged съедала мышиный ввод.
                Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    (Action)delegate { Theme.Apply(next); });
            };
            Body.Children.Add(Row(Loc.T("settings.theme"), Loc.T("settings.theme.desc"), theme));
            Body.Children.Add(space());

            // Акцентный цвет: пресеты поверх палитры темы. В System24 скрыт —
            // там акцент задают подтемы-флейворы.
            if (Theme.Mode != ThemeMode.System24)
            {
                var accent = Combo(180);
                accent.Tag = "setting_accent";
                Ctl.AutomationSetName(accent, Loc.T("settings.accent"));
                accent.Items.Add(Loc.T("settings.accent.default"));
                foreach (var ap in Theme.Accents)
                    accent.Items.Add(Loc.T("settings.accent." + ap.Key));
                int curAccent = 0;
                string savedAccent = Core.Get("accent", null);
                for (int i = 0; i < Theme.Accents.Length; i++)
                    if (string.Equals(Theme.Accents[i].Key, savedAccent, StringComparison.OrdinalIgnoreCase))
                    { curAccent = i + 1; break; }
                accent.SelectedIndex = curAccent;
                accent.SelectionChanged += (s, e) =>
                {
                    int idx = accent.SelectedIndex;
                    if (idx < 0) return;
                    string key = idx == 0 ? "default" : Theme.Accents[idx - 1].Key;
                    string was = Core.Get("accent", null);
                    if (string.IsNullOrEmpty(was)) was = "default";
                    if (string.Equals(was, key, StringComparison.OrdinalIgnoreCase)) return;
                    Core.Set("accent", key);
                    Core.SaveConfig();
                    // Фоновое применение — как у комбо шрифта: пересборка внутри
                    // SelectionChanged оставляла застрявший мышиный захват.
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        (Action)delegate { Theme.Apply(Theme.Mode); });
                };
                Body.Children.Add(Row(Loc.T("settings.accent"), Loc.T("settings.accent.desc"), accent));
                Body.Children.Add(space());
            }

            // Смена шрифта интерфейса. В System24 скрыт: там своё моно-семейство
            // (см. Theme.System24Font), выбор сохраняется и вернётся с другой темой.
            if (Theme.Mode != ThemeMode.System24)
            {
                var font = Combo(180);
                font.Tag = "setting_ui_font";
                Ctl.AutomationSetName(font, Loc.T("settings.font"));
                // Реестр семейств — Core.FontFamilies: добавление или удаление
                // шрифта не требует правки этого метода (как и с темами).
                var families = Core.FontFamilies;
                var activeFam = Core.ResolveUiFontFamily();
                int curFont = 0;
                for (int i = 0; i < families.Length; i++)
                {
                    font.Items.Add(families[i].Title);
                    if (families[i].Key == activeFam.Key) curFont = i;
                }
                font.SelectedIndex = curFont;
                font.SelectionChanged += (s, e) =>
                {
                    int idx = font.SelectedIndex;
                    if (idx < 0 || idx >= families.Length) return;
                    if (families[idx].Key == Core.ResolveUiFontFamily().Key) return;
                    // Пересборку UI нельзя делать прямо в этом обработчике: Popup
                    // выпадающего списка не успевает закрыться и отпустить мышиный
                    // захват до отцепления комбо, и после этого весь ввод (колесо,
                    // клики) навсегда уходит в мёртвый элемент. Даём дропдауну
                    // закрыться и переключаем фоном.
                    string key = families[idx].Key;
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        (Action)delegate { Core.RequestUiFont(key); });
                };
                Body.Children.Add(Row(Loc.T("settings.font"), Loc.T("settings.font.desc"), font));
                Body.Children.Add(space());
            }

            if (Theme.Mode == ThemeMode.System24)
            {
                var flavorCombo = Combo(180);
                flavorCombo.Tag = "setting_flavor";
                Ctl.AutomationSetName(flavorCombo, Loc.T("settings.system24.flavor"));
                for (int i = 0; i < Theme.System24Flavors.Length; i++)
                {
                    flavorCombo.Items.Add(Theme.GetFlavorName(Theme.System24Flavors[i]));
                }

                int curFlavorIdx = 0;
                for (int i = 0; i < Theme.System24Flavors.Length; i++)
                {
                    if (string.Equals(Theme.System24Flavors[i], Theme.System24Flavor, StringComparison.OrdinalIgnoreCase))
                    {
                        curFlavorIdx = i;
                        break;
                    }
                }
                flavorCombo.SelectedIndex = curFlavorIdx;
                flavorCombo.SelectionChanged += (s, e) =>
                {
                    if (flavorCombo.SelectedIndex >= 0 && flavorCombo.SelectedIndex < Theme.System24Flavors.Length)
                    {
                        string nextFlavor = Theme.System24Flavors[flavorCombo.SelectedIndex];
                        if (!string.Equals(nextFlavor, Theme.System24Flavor, StringComparison.OrdinalIgnoreCase))
                        {
                            Theme.ApplySystem24Flavor(nextFlavor);
                        }
                    }
                };

                Body.Children.Add(Row(Loc.T("settings.system24.flavor"), Loc.T("settings.system24.flavor.desc"), flavorCombo));
                Body.Children.Add(space());
            }

            // Плотность: компактный режим множит отступы карточек, кнопок и строк.
            // Доступен во всех темах — длинный список стратегий есть везде.
            Body.Children.Add(Row(Loc.T("settings.compact"), Loc.T("settings.compact.desc"),
                Tog("compact_ui", false, Loc.T("settings.compact"), delegate(bool v) { Theme.Apply(Theme.Mode); })));
            Body.Children.Add(space());

            var lang = Combo(180);
            lang.Tag = "setting_lang";
            Ctl.AutomationSetName(lang, Loc.T("settings.lang"));
            lang.Items.Add("Русский");
            lang.Items.Add("English");
            lang.SelectedIndex = Loc.Lang == "en" ? 1 : 0;
            lang.SelectionChanged += (s, e) =>
            {
                string next = lang.SelectedIndex == 1 ? "en" : "ru";
                if (next == Loc.Lang) return;
                // Язык тоже пересобирает окно (LanguageChanged) — фоном, после
                // закрытия дропдауна, чтобы не повторить историю с захватом мыши.
                Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    (Action)delegate { Loc.SetLang(next); });
            };
            Body.Children.Add(Row(Loc.T("settings.lang"), Loc.T("settings.lang.desc"), lang));
            Body.Children.Add(space());

            Body.Children.Add(Row(Loc.T("settings.reduceMotion"), Loc.T("settings.reduceMotion.desc"),
                Tog("reduce_motion", false, Loc.T("settings.reduceMotion"), null)));
        }

        internal static string NormVer(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            v = v.Trim().TrimStart('v', 'V');
            string[] p = v.Split('.');
            if (p.Length == 4 && p[3] == "0")
                return p[0] + "." + p[1] + "." + p[2];
            return v;
        }

        static void ParseVerPart(string part, out int num, out string suf)
        {
            num = 0;
            suf = "";
            if (string.IsNullOrEmpty(part)) return;
            int i = 0;
            while (i < part.Length && char.IsDigit(part[i])) i++;
            if (i > 0) int.TryParse(part.Substring(0, i), out num);
            if (i < part.Length) suf = part.Substring(i);
        }

        internal static int CompareVersions(string left, string right)
        {
            if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right)) return 0;
            if (string.IsNullOrEmpty(left)) return -1;
            if (string.IsNullOrEmpty(right)) return 1;

            string l = TrimZeroComponents(NormVer(left));
            string r = TrimZeroComponents(NormVer(right));
            if (string.Equals(l, r, StringComparison.OrdinalIgnoreCase)) return 0;

            string[] lp = l.Split('.');
            string[] rp = r.Split('.');
            int max = Math.Max(lp.Length, rp.Length);
            for (int i = 0; i < max; i++)
            {
                string p1 = i < lp.Length ? lp[i] : "0";
                string p2 = i < rp.Length ? rp[i] : "0";
                int n1, n2; string s1, s2;
                ParseVerPart(p1, out n1, out s1);
                ParseVerPart(p2, out n2, out s2);
                if (n1 != n2) return n1.CompareTo(n2);
                int sc = string.Compare(s1, s2, StringComparison.OrdinalIgnoreCase);
                if (sc != 0)
                {
                    if (string.IsNullOrEmpty(s1)) return -1;
                    if (string.IsNullOrEmpty(s2)) return 1;
                    return sc;
                }
            }
            return 0;
        }

        static string TrimZeroComponents(string v)
        {
            if (string.IsNullOrEmpty(v)) return v;
            var parts = new System.Collections.Generic.List<string>(v.Split('.'));
            while (parts.Count > 2 && parts[parts.Count - 1] == "0")
                parts.RemoveAt(parts.Count - 1);
            return string.Join(".", parts.ToArray());
        }

        // ---------- Telegram-прокси ----------

        void BuildTgProxy()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.tg")));

            var content = new StackPanel();

            // Статус — сверху, действия — ровными рядами ниже. Никаких
            // разбросанных кнопок по углам карточки.
            _tgPillHost = new Border { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left };
            content.Children.Add(_tgPillHost);

            var desc = UI.T(Loc.T("settings.tg.desc"), Theme.FsSmall, Theme.BrMuted);
            desc.Margin = new Thickness(0, 8, 0, 0);
            content.Children.Add(desc);

            _tgChips = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            content.Children.Add(_tgChips);

            _tgHint = UI.T("", Theme.FsSmall, Theme.BrFaint);
            _tgHint.Margin = new Thickness(0, 0, 0, 0);
            content.Children.Add(_tgHint);

            _tgSection = UI.Card(content, new Thickness(16, 12, 16, 12), Theme.R10, Theme.BrSurface);
            Body.Children.Add(_tgSection);
            Body.Children.Add(space());
            RefreshTgSection();
        }

        Button TgChip(string text, string icon, int kind)
        {
            var b = Ctl.Button(text, icon, kind);
            var bb = Ctl.Surface(b);
            var bbox = Ctl.ContentHost(b);
            if (bb != null) { bb.Height = 32; bb.CornerRadius = Theme.R8; if (bbox != null) bbox.Padding = new Thickness(10, 0, 10, 0); }
            b.Margin = new Thickness(0, 0, 6, 6);
            return b;
        }

#if SELFTEST
        // Тестовый набор трёх чипов: проверяем, что иконки и подписи влезают
        // в одну линию, не растягивая кнопки на всю карточку.
        public void ShowDemoTgChips()
        {
            if (_tgChips == null) return;
            _tgChips.Children.Clear();
            _tgChips.Children.Add(TgChip("AyuGram Desktop", Icons.Telegram, 1));
            _tgChips.Children.Add(TgChip(Loc.T("ov.tg.copy"), Icons.Link, 1));
            _tgChips.Children.Add(TgChip(Loc.T("settings.tg.folder"), Icons.Folder, 1));
        }

        public void HideDemoTgChips()
        {
            RefreshTgSection();
        }
#endif

        void RefreshTgSection()
        {
            if (_tgSection == null) return;

            bool installed = Core.TgProxyInstalled();
            bool running = Core.TgProxyRunning();
            TgProxySettings cfg = null;
            string cfgErr = null;
            bool cfgOk = installed && Core.TryTgProxySettings(out cfg, out cfgErr);
            bool canUse = running && cfgOk;

            if (_tgPillHost != null)
            {
                if (!installed) _tgPillHost.Child = Pill.Make(Sev.Warn, Loc.T("tg.notInstalled"));
                else if (running && cfgOk) _tgPillHost.Child = Pill.Make(Sev.Ok, Loc.T("ov.running"));
                else if (running) _tgPillHost.Child = Pill.Make(Sev.Warn, Loc.T("ov.tg.cfgInvalid"));
                else _tgPillHost.Child = Pill.Make(Sev.Neutral, Loc.T("ov.stopped"));
            }

            var clients = installed ? Core.TgProxyClients() : new System.Collections.Generic.List<TgProxyClient>();
            if (_tgHint != null)
            {
                if (!installed) _tgHint.Text = Loc.T("settings.tg.hint.notInstalled");
                else if (!running) _tgHint.Text = Loc.T("settings.tg.hint.stopped");
                else if (!cfgOk) _tgHint.Text = cfgErr ?? Loc.T("ov.tg.cfgInvalid");
                else if (clients.Count == 0) _tgHint.Text = Loc.T("settings.tg.noClientsHint");
                else _tgHint.Text = Loc.T("settings.tg.hint.running");
            }

            if (_tgChips == null) return;
            _tgChips.Children.Clear();

            // Компактные кнопки по содержимому, в одну линию: клиент(ы),
            // копирование и папка. Никаких растянутых на всю карточку кнопок.
            if (canUse)
            {
                foreach (TgProxyClient client in clients)
                {
                    TgProxyClient c = client;
                    string name = c.Name ?? "";
                    if (name.Length > 24) name = name.Substring(0, 23) + "…";
                    var b = TgChip(name, Icons.Telegram, 1);
                    string tip = Loc.T("ov.tg.clientTip");
                    if (!string.Equals(name, c.Name, StringComparison.Ordinal)) tip = c.Name + " — " + tip;
                    if (c.IsDefault) tip += " · " + Loc.T("ov.tg.defaultClient");
                    b.ToolTip = tip;
                    b.Click += (s, e) => TgOpenClient(c);
                    _tgChips.Children.Add(b);
                }

                var copy = TgChip(Loc.T("ov.tg.copy"), Icons.Link, 1);
                copy.ToolTip = Loc.T("ov.tg.copyTip");
                copy.Click += (s, e) => TgCopyLink();
                _tgChips.Children.Add(copy);
            }

            var folder = TgChip(Loc.T("settings.tg.folder"), Icons.Folder, 1);
            folder.ToolTip = Loc.T("settings.tg.folder");
            folder.Click += (s, e) =>
            {
                try
                {
                    System.IO.Directory.CreateDirectory(Core.TgToolsDir);
                    Core.OpenFolder(Core.TgToolsDir);
                }
                catch { }
            };
            _tgChips.Children.Add(folder);
        }

        void TgOpenClient(TgProxyClient client)
        {
            if (client == null) return;
            TgProxySettings cfg;
            string err;
            if (!Core.TryTgProxySettings(out cfg, out err))
            {
                _win.ShowToast(err ?? Loc.T("ov.tg.cfgMissing"), Sev.Warn);
                return;
            }
            if (!Core.TgOpenInClient(client.Path, cfg.Link, out err))
            {
                Core.Warn("TG client " + client.Name + ": " + err);
                _win.ShowToast(string.Format(Loc.T("ov.tg.openFail"), err), Sev.Err);
            }
        }

        void TgCopyLink()
        {
            TgProxySettings cfg;
            string err;
            if (!Core.TryTgProxySettings(out cfg, out err))
            {
                _win.ShowToast(err ?? Loc.T("ov.tg.cfgMissing"), Sev.Warn);
                return;
            }
            try
            {
                Clipboard.SetText(cfg.Link);
                _win.ShowToast(Loc.T("ov.tg.copied"), Sev.Ok);
            }
            catch (Exception ex)
            {
                Core.Warn("TG copy: " + Core.Short(ex.Message));
                _win.ShowToast(string.Format(Loc.T("ov.tg.copyFail"), Core.Short(ex.Message)), Sev.Err);
            }
        }

        // Переход с главной: сначала даём странице проявиться, затем плавно
        // докручиваем к секции и только после этого мигаем рамкой.
        public void RevealTgSection()
        {
            RefreshTgSection();

            if (_tgRevealTimer != null) { _tgRevealTimer.Stop(); _tgRevealTimer = null; }
            var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
            _tgRevealTimer = delay;
            delay.Tick += (s, e) =>
            {
                delay.Stop();
                if (_tgRevealTimer == delay) _tgRevealTimer = null;
                ScrollToTgSection(FlashTgSection);
            };
            delay.Start();
        }

        void ScrollToTgSection(Action onDone)
        {
            if (_tgScrollTimer != null) { _tgScrollTimer.Stop(); _tgScrollTimer = null; }
            if (_tgSection == null)
            {
                if (onDone != null) onDone();
                return;
            }
            try
            {
                Body.UpdateLayout();
                _tgSection.UpdateLayout();
                Point p = _tgSection.TransformToAncestor(Body).Transform(new Point(0, 0));
                double dest = Math.Max(0, Math.Min(ScrollHost.ScrollableHeight, p.Y - 12));
                double start = ScrollHost.VerticalOffset;
                if (!Theme.AnimationsEnabled || Math.Abs(dest - start) < 1)
                {
                    ScrollHost.JumpTo(dest);
                    if (onDone != null) onDone();
                    return;
                }

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
                _tgScrollTimer = timer;
                timer.Tick += (s, e) =>
                {
                    double t = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / 450.0);
                    // easeInOutCubic: мягкий старт и финиш вместо рывка в начале.
                    double eased = t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3.0) / 2.0;
                    ScrollHost.JumpTo(start + (dest - start) * eased);
                    if (t >= 1.0)
                    {
                        timer.Stop();
                        if (_tgScrollTimer == timer) _tgScrollTimer = null;
                        if (onDone != null) onDone();
                    }
                };
                timer.Start();
            }
            catch
            {
                if (onDone != null) onDone();
            }
        }

        void FlashTgSection()
        {
            if (_tgSection == null) return;
            int gen = ++_tgFlashGen;

            if (!Theme.AnimationsEnabled)
            {
                _tgSection.BorderBrush = Theme.BrAccent;
                _tgSection.BorderThickness = new Thickness(2);
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    if (gen != _tgFlashGen) return;
                    _tgSection.BorderBrush = Theme.BrStroke;
                    _tgSection.BorderThickness = new Thickness(1);
                };
                t.Start();
                return;
            }

            var brush = new SolidColorBrush(Theme.AccentMain);
            _tgSection.BorderBrush = brush;
            _tgSection.BorderThickness = new Thickness(2);
            var anim = new ColorAnimation
            {
                From = Theme.AccentMain,
                To = Theme.Stroke,
                Duration = TimeSpan.FromMilliseconds(550),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(2)
            };
            anim.Completed += (s, e) =>
            {
                if (gen != _tgFlashGen) return;
                _tgSection.BorderBrush = Theme.BrStroke;
                _tgSection.BorderThickness = new Thickness(1);
            };
            brush.BeginAnimation(SolidColorBrush.ColorProperty, anim);
        }

        void BuildPrivacy()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.privacy")));
            Body.Children.Add(NoteCard(Icons.Shield, Theme.BrOk, Loc.T("settings.privacy.note"), Sev.Ok));
        }

        void BuildAntivirus()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.antivirus")));

            var btn = Ctl.Button(Loc.T("settings.defender.btn"), Icons.Shield, 1);
            var pillHost = new Border { VerticalAlignment = VerticalAlignment.Center };
            pillHost.Child = Pill.Make(Sev.Neutral, Loc.T("settings.defender.notIn"));

            Action updateStatus = delegate
            {
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    var status = Core.GetDefenderExclusionStatus();
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (!status.HasValue)
                        {
                            pillHost.Child = Pill.Make(Sev.Neutral, Loc.T("settings.defender.disabled"));
                            btn.IsEnabled = false;
                        }
                        else if (status.Value)
                        {
                            pillHost.Child = Pill.Make(Sev.Ok, Loc.T("settings.defender.inList"));
                            btn.IsEnabled = false;
                        }
                        else
                        {
                            pillHost.Child = Pill.Make(Sev.Warn, Loc.T("settings.defender.notIn"));
                            btn.IsEnabled = true;
                        }
                    });
                });
            };

            btn.Click += (s, e) =>
            {
                btn.IsEnabled = false;
                _win.ShowToast(Loc.T("settings.defender.btn") + "...", Sev.Info);
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    bool ok = Core.AddDefenderExclusion();
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ok)
                        {
                            _win.ShowToast(Loc.T("settings.defender.ok"), Sev.Ok);
                            Core.Info(Loc.T("settings.defender.ok"));
                            updateStatus();
                        }
                        else
                        {
                            _win.ShowToast(Loc.T("settings.defender.fail"), Sev.Err);
                            btn.IsEnabled = true;
                        }
                    });
                });
            };

            updateStatus();

            // Кнопка сверху, статус «добавлено / не добавлено» под ней. В одну
            // строку они съедали бы ~380 px из ~800, и текст карточки растягался
            // на 7 строк.
            var side = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center };
            side.Children.Add(btn);
            pillHost.HorizontalAlignment = HorizontalAlignment.Left;
            pillHost.Margin = new Thickness(0, 8, 0, 0);
            side.Children.Add(pillHost);

            Body.Children.Add(ActionCard(Icons.Shield, Theme.BrWarn,
                Loc.T("settings.defender.note"), Sev.Warn, side));
        }

        void BuildMaintenance()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.maintenance")));

            var resetBtn = Ctl.Button(Loc.T("settings.reset.btn"), Icons.Trash, 2);
            resetBtn.Click += (s, e) =>
            {
                var res = MessageBox.Show(
                    Loc.T("settings.reset.confirm"),
                    Loc.T("settings.reset.btn"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (res != MessageBoxResult.Yes) return;

                string error;
                if (Core.FactoryReset(out error))
                {
                    _win.ShowToast(Loc.T("settings.reset.ok"), Sev.Ok);
                    Core.Good(Loc.T("settings.reset.ok"));
                    MessageBox.Show(Loc.T("settings.reset.ok"), Loc.T("settings.reset.btn"), MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _win.ShowToast(error ?? "Reset failed", Sev.Err);
                    Core.Fail(error ?? "Reset failed");
                    MessageBox.Show(error ?? "Reset failed", Loc.T("settings.reset.btn"), MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            Body.Children.Add(ActionCard(Icons.Alert, Theme.BrWarn,
                Loc.T("settings.reset.desc"), Sev.Warn, resetBtn));
        }

        static UIElement space() { return new Border { Height = Theme.Dense(10) }; }
    }
}
