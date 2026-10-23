using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZapretStudio
{
    class AboutPage : Page
    {
        public override string Title { get { return Loc.T("about.title"); } }
        public override string Subtitle { get { return Loc.T("about.sub"); } }

        readonly MainWindow _win;

        public AboutPage(MainWindow win)
        {
            _win = win;
            Rebuild();
        }

        public override void OnShow()
        {
            Rebuild();
        }

        void Rebuild()
        {
            Body.Children.Clear();
            BuildHeader();
            BuildVersions();
            BuildLinks();
            BuildLicenses();
            BuildChecksums();
            BuildFakeWarning();
        }

        void BuildVersions()
        {
            Body.Children.Add(SectionLabel(Loc.T("about.sec.versions")));

            // То же оформление списка, что у лицензий: одна карточка, строка на
            // компонент, разделитель между строками. Плашка справа у строк со
            // ссылкой — кнопка: клик открывает сайт.
            var list = new StackPanel();

            // Версия приложения
            string appVer = SettingsPage.NormVer(Core.AppVersion);
            ListRow(list, Loc.T("about.ver.app"), Loc.T("about.ver.app.desc"),
                Pill.Make(Theme.AccentMain, appVer));

            // Версия zapret
            string zv = Core.ZapretVersion();
            string zapVer = string.IsNullOrEmpty(zv) ? "—" : SettingsPage.NormVer(zv);
            ListRow(list, Loc.T("about.ver.zapret"), Loc.T("about.ver.zapret.desc"),
                Pill.Make(Sev.Neutral, zapVer));

            // Версия Telegram-прокси
            string tv = Core.TgProxyInstalled() ? Core.TgProxyLocalVersion() : null;
            string tgVer = Core.TgProxyInstalled() ? (string.IsNullOrEmpty(tv) ? "?" : SettingsPage.NormVer(tv)) : Loc.T("settings.tg.notInstalled");
            ListRow(list, Loc.T("about.ver.tg"), Loc.T("about.ver.tg.desc"),
                Pill.Make(Core.TgProxyInstalled() ? Sev.Neutral : Sev.Warn, tgVer));

            // Репозиторий приложения
            ListRow(list, Loc.T("about.ver.repo"), Core.AppRepo,
                PillLink(HostOf(Core.AppRepo), Core.AppRepo));

            // Автор / разработчик
            ListRow(list, Loc.T("about.ver.author"), Loc.T("about.ver.author.desc"),
                PillLink(HostOf(Core.AppRepo), Core.AppRepo));

            Body.Children.Add(UI.Card(list, new Thickness(16, 4, 16, 4)));
        }

        void BuildHeader()
        {
            var sp = new StackPanel();
            sp.Children.Add(UI.T(string.Format(Loc.T("about.appName"), Core.AppName), Theme.FsH1, Theme.BrText, FontWeights.SemiBold));
            string appVer = SettingsPage.NormVer(Core.AppVersion);
            string zapVer = SettingsPage.NormVer(Core.ZapretVersion());
            string tgVer = Core.TgProxyInstalled()
                ? (string.IsNullOrEmpty(Core.TgProxyLocalVersion()) ? "?" : SettingsPage.NormVer(Core.TgProxyLocalVersion()))
                : Loc.T("settings.tg.notInstalled");
            sp.Children.Add(new TextBlock { Text = string.Format(Loc.T("about.versionLine"), appVer, zapVer, tgVer),
                Foreground = Theme.BrMuted, FontSize = Theme.FsBody, FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("about.intro"),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0)
            });
            Body.Children.Add(UI.Card(sp, new Thickness(18, 16, 18, 16)));
        }

        void BuildLinks()
        {
            Body.Children.Add(SectionLabel(Loc.T("about.sec.links")));

            var list = new StackPanel();
            LinkRow(list, "zapret-discord-youtube (Flowseal)", Loc.T("about.link.flowseal"),
                "https://github.com/Flowseal/zapret-discord-youtube");
            LinkRow(list, "tg-ws-proxy (Flowseal)", Loc.T("about.link.tgproxy"),
                Core.TgProxyRepo);
            LinkRow(list, "zapret (bol-van)", Loc.T("about.link.engine"),
                "https://github.com/bol-van/zapret");
            LinkRow(list, "WinDivert", Loc.T("about.link.windivert"),
                "https://github.com/basil00/Divert");
            LinkRow(list, "System24 theme (refact0r)", Loc.T("about.link.system24"),
                "https://github.com/refact0r/system24");
            Body.Children.Add(UI.Card(list, new Thickness(16, 4, 16, 4)));
        }

        static void LinkRow(StackPanel host, string title, string desc, string url)
        {
            ListRow(host, title, desc, PillLink(HostOf(url), url));
        }

        // Строка списка в оформлении лицензий: имя и пояснение слева, плашка
        // справа; между строками — разделитель в линию карточки.
        static void ListRow(StackPanel host, string name, string desc, FrameworkElement right)
        {
            if (host.Children.Count > 0)
                host.Children.Add(new Border { Height = 1, Background = Theme.BrStrokeSoft });

            var g = new Grid { MinHeight = 36 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 12, 6) };
            left.Children.Add(new TextBlock
            {
                Text = name, Foreground = Theme.BrText, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap
            });
            if (!string.IsNullOrEmpty(desc))
                left.Children.Add(new TextBlock
                {
                    Text = desc, Foreground = Theme.BrMuted, FontSize = Theme.FsTiny,
                    FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            Grid.SetColumn(left, 0);
            g.Children.Add(left);

            var rc = new ContentControl { Content = right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(rc, 1);
            g.Children.Add(rc);

            host.Children.Add(g);
        }

        // Плашка-кнопка: выглядит как пилюля из списка лицензий, но кликабельна —
        // открывает ссылку. Домен в подписи и полный адрес в подсказке.
        static Button PillLink(string text, string url)
        {
            var b = new Button { Cursor = System.Windows.Input.Cursors.Hand };
            var pill = Pill.Make(Theme.AccentMain, text);
            b.Content = Ctl.SplitSurface(pill);
            Ctl.StripChrome(b);
            Ctl.AttachGlow(b, pill);
            b.ToolTip = url;
            b.Click += (s, e) => Core.OpenUrl(url);
            return b;
        }

        static string HostOf(string url)
        {
            try { return new Uri(url).Host; } catch { return url; }
        }

        void BuildLicenses()
        {
            Body.Children.Add(SectionLabel(Loc.T("about.sec.licenses")));

            // Раньше: восемь отдельных карточек в две строки + отступ между ними,
            // причём тип лицензии был написан дважды (подпись и пилюля). Список
            // занимает одну карточку по строке на компонент.
            var list = new StackPanel();
            LicRow(list, Loc.T("about.lic.engine"), "MIT");
            LicRow(list, "WinDivert", "LGPLv3 / GPLv2");
            LicRow(list, Loc.T("about.lic.flowseal"), "MIT");
            LicRow(list, Loc.T("about.lic.system24"), "MIT");
            LicRow(list, "Lucide Icons", "ISC");
            LicRow(list, "Google Sans / Google Sans Code", "Apache 2.0");
            LicRow(list, "tg-ws-proxy (Flowseal)", "MIT");
            LicRow(list, "curl", "MIT-like");
            Body.Children.Add(UI.Card(list, new Thickness(16, 4, 16, 4)));
        }

        static void LicRow(StackPanel host, string name, string lic)
        {
            if (host.Children.Count > 0)
                host.Children.Add(new Border { Height = 1, Background = Theme.BrStrokeSoft });

            var g = new Grid { MinHeight = 36 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var t = new TextBlock
            {
                Text = name, Foreground = Theme.BrText, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 12, 6)
            };
            Grid.SetColumn(t, 0);
            g.Children.Add(t);

            var rc = new ContentControl
            {
                Content = Pill.Make(Theme.AccentMain, lic),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rc, 1);
            g.Children.Add(rc);

            host.Children.Add(g);
        }

        Button _verifyBtn;
        void BuildChecksums()
        {
            Body.Children.Add(SectionLabel(Loc.T("about.sec.integrity")));
            _verifyBtn = Ctl.Button(Loc.T("about.verify"), Icons.Check, 1);
            _verifyBtn.HorizontalAlignment = HorizontalAlignment.Left;
            _verifyBtn.Click += (s, e) => ToggleVerify();
            Body.Children.Add(Row(Loc.T("about.checksums"),
                Loc.T("about.checksums.desc"),
                _verifyBtn));
            _sumResult = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            Body.Children.Add(_sumResult);
        }

        StackPanel _sumResult;
        bool _verifying;

        void ToggleVerify()
        {
            if (_verifying) return;
            if (_sumResult.Children.Count > 0)
            {
                _sumResult.Children.Clear();
                Ctl.SetButton(_verifyBtn, Loc.T("about.verify"), Icons.Check, 1);
                return;
            }
            Verify();
        }

        void Verify()
        {
            if (_verifying) return;
            _sumResult.Children.Clear();
            string sumsFile = System.IO.Path.Combine(Core.Root, "SHA256SUMS.txt");
            if (!System.IO.File.Exists(sumsFile))
            {
                // Апстрим Flowseal не кладёт SHA256SUMS.txt в архив движка:
                // скачанный ZIP проверяется по digest GitHub. Поэтому вместо
                // ложного «файл не найден» проверяем обязательные файлы bin/.
                var issues = Core.EngineIntegrityIssues();
                _sumResult.Children.Add(UI.T(
                    string.Format(Loc.T("about.sums.noManifest"), issues.Count),
                    Theme.FsBody, issues.Count == 0 ? Theme.BrOk : Theme.BrWarn,
                    FontWeights.SemiBold));
                foreach (string issue in issues)
                    _sumResult.Children.Add(Warn(issue));
                Ctl.SetButton(_verifyBtn, Loc.T("common.close"), Icons.Cross, 1);
                return;
            }
            _verifying = true;
            Ctl.SetButton(_verifyBtn, Loc.T("common.close"), Icons.Cross, 1);
            _sumResult.Children.Add(UI.T(Loc.T("common.checking") + "...", Theme.FsSmall, Theme.BrMuted, FontWeights.Normal));

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    var lines = System.IO.File.ReadAllLines(sumsFile);
                    var checkedItems = new System.Collections.Generic.List<Tuple<string, Sev, string>>();
                    var skippedItems = new System.Collections.Generic.List<Tuple<string, Sev, string>>();
                    int ok = 0, bad = 0, miss = 0, skipped = 0;
                    foreach (var line in lines)
                    {
                        string s = line.Trim();
                        if (s.Length == 0 || s.StartsWith("#")) continue;
                        int sp = s.IndexOf(' ');
                        if (sp < 0) continue;
                        string hash = s.Substring(0, sp).Trim().ToLowerInvariant();
                        string rel = s.Substring(sp).Trim().TrimStart('*').Trim();
                        string cleanRel = rel.Replace('/', System.IO.Path.DirectorySeparatorChar).TrimStart('.', System.IO.Path.DirectorySeparatorChar);
                        // SHA256SUMS.txt: манифест пакета zapret, он описывает
                        // весь корень: сам себя, README/LICENSE (это файлы Lantern),
                        // lists/* и general*.bat, которые легально перезаписывают
                        // приложение и пользователь. Сверка по всем строкам давала
                        // 28 «подмен» из 43 и обесценивала проверку. Проверяем
                        // только bin/: исполняемые файлы движка.
                        if (!InChecksumScope(cleanRel))
                        {
                            skipped++;
                            skippedItems.Add(Tuple.Create(rel, Sev.Neutral, Loc.T("about.sums.skipped")));
                            continue;
                        }
                        string full = System.IO.Path.Combine(Core.Root, cleanRel);
                        if (!System.IO.File.Exists(full)) { miss++; checkedItems.Add(Tuple.Create(rel, Sev.Warn, Loc.T("about.sums.missing"))); continue; }
                        string actual;
                        try { actual = Sha256(full); }
                        catch { miss++; checkedItems.Add(Tuple.Create(rel, Sev.Warn, Loc.T("about.sums.missing"))); continue; }
                        if (actual == hash) { ok++; checkedItems.Add(Tuple.Create(rel, Sev.Ok, Loc.T("about.sums.match"))); }
                        else { bad++; checkedItems.Add(Tuple.Create(rel, Sev.Err, Loc.T("about.sums.mismatch"))); }
                    }
                    string msg = string.Format(Loc.T("about.sums.summary"), ok, bad, miss, skipped);
                    Core.Info(msg);
                    Dispatcher.Invoke((Action)delegate
                    {
                        _sumResult.Children.Clear();
                        var headerTb = UI.T(msg, Theme.FsBody, bad > 0 ? Theme.BrErr : Theme.BrOk, FontWeights.SemiBold);
                        headerTb.Margin = new Thickness(0, 0, 0, 8);
                        _sumResult.Children.Add(headerTb);

                        var listPanel = new StackPanel();
                        // Сначала реальная проверка, «вне проверки» — ниже
                        foreach (var it in checkedItems)
                        {
                            listPanel.Children.Add(SumLine(it.Item1, it.Item2, it.Item3));
                        }
                        foreach (var it in skippedItems)
                        {
                            listPanel.Children.Add(SumLine(it.Item1, it.Item2, it.Item3));
                        }
                        var sv = new ScrollViewer
                        {
                            MaxHeight = 360,
                            Focusable = false,
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                            Content = listPanel
                        };
                        sv.PreviewMouseWheel += (ws, we) =>
                        {
                            SmoothWheel.Scroll(sv, we.Delta, 40);
                            we.Handled = true;
                        };
                        _sumResult.Children.Add(UI.Card(sv, new Thickness(14, 10, 14, 10)));
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        _sumResult.Children.Clear();
                        _sumResult.Children.Add(Warn(ex.Message));
                    });
                }
                finally
                {
                    _verifying = false;
                }
            });
        }

        // В область сверки попадают только файлы движка из bin/.
        static bool InChecksumScope(string cleanRel)
        {
            string norm = cleanRel.Replace(System.IO.Path.AltDirectorySeparatorChar, System.IO.Path.DirectorySeparatorChar);
            return norm.StartsWith("bin" + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        static string Sha256(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs = System.IO.File.OpenRead(path))
            {
                var h = sha.ComputeHash(fs);
                var sb = new System.Text.StringBuilder();
                foreach (var b in h) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        Border SumLine(string name, Sev sev, string text)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var n = new TextBlock { Text = name, Foreground = Theme.BrText, FontSize = Theme.FsSmall,
                FontFamily = Theme.MonoFont, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(n, 0); g.Children.Add(n);
            var p = Pill.Make(sev, text); p.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(p, 1); g.Children.Add(p);
            var c = UI.Card(g, new Thickness(14, 9, 14, 9));
            c.Margin = new Thickness(0, 0, 0, 6);
            return c;
        }

        TextBlock Warn(string t)
        {
            return new TextBlock { Text = t, Foreground = Theme.BrWarn, FontSize = Theme.FsSmall, FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap };
        }

        void BuildFakeWarning()
        {
            var card = NoteCard(Icons.Warn, Theme.BrWarn, Loc.T("about.warn"), Sev.Warn);
            card.Margin = new Thickness(0, 18, 0, 0);
            Body.Children.Add(card);
        }
    }
}
