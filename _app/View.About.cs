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

            // Версия приложения
            Body.Children.Add(Row(Loc.T("about.ver.app"), Loc.T("about.ver.app.desc"),
                Pill.Make(Sev.Info, Core.AppVersion)));
            Body.Children.Add(space());

            // Версия zapret
            string zv = Core.ZapretVersion();
            Body.Children.Add(Row(Loc.T("about.ver.zapret"), Loc.T("about.ver.zapret.desc"),
                Pill.Make(Sev.Neutral, string.IsNullOrEmpty(zv) ? "—" : zv)));
            Body.Children.Add(space());

            // Версия Telegram-прокси
            string tv = Core.TgProxyInstalled() ? Core.TgProxyLocalVersion() : null;
            Body.Children.Add(Row(Loc.T("about.ver.tg"), Loc.T("about.ver.tg.desc"),
                Pill.Make(Core.TgProxyInstalled() ? Sev.Neutral : Sev.Warn,
                    Core.TgProxyInstalled() ? (string.IsNullOrEmpty(tv) ? "?" : SettingsPage.NormVer(tv)) : Loc.T("settings.tg.notInstalled"))));
            Body.Children.Add(space());

            // Репозиторий приложения
            var repoBtn = Ctl.Button(Loc.T("common.open"), Icons.External, 1);
            repoBtn.Click += (s, e) => Core.OpenUrl(Core.AppRepo);
            Body.Children.Add(Row(Loc.T("about.ver.repo"), Core.AppRepo, repoBtn));
        }

        void BuildHeader()
        {
            var sp = new StackPanel();
            sp.Children.Add(UI.T(string.Format(Loc.T("about.appName"), Core.AppName), Theme.FsH1, Theme.BrText, FontWeights.SemiBold));
            sp.Children.Add(new TextBlock { Text = string.Format(Loc.T("about.versionLine"), Core.AppVersion, Core.ZapretVersion()),
                Foreground = Theme.BrMuted, FontSize = Theme.FsBody, FontFamily = Theme.UiFont, Margin = new Thickness(0, 6, 0, 0) });
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
            Body.Children.Add(LinkRow("zapret-discord-youtube (Flowseal)", Loc.T("about.link.flowseal"),
                "https://github.com/Flowseal/zapret-discord-youtube"));
            Body.Children.Add(space());
            Body.Children.Add(LinkRow("tg-ws-proxy (Flowseal)", Loc.T("about.link.tgproxy"),
                Core.TgProxyRepo));
            Body.Children.Add(space());
            Body.Children.Add(LinkRow("zapret (bol-van)", Loc.T("about.link.engine"),
                "https://github.com/bol-van/zapret"));
            Body.Children.Add(space());
            Body.Children.Add(LinkRow("WinDivert", Loc.T("about.link.windivert"),
                "https://github.com/basil00/Divert"));
        }

        Border LinkRow(string title, string desc, string url)
        {
            var btn = Ctl.Button(Loc.T("common.open"), Icons.External, 1);
            btn.Click += (s, e) => Core.OpenUrl(url);
            return Row(title, desc + "\n" + url, btn);
        }

        void BuildLicenses()
        {
            Body.Children.Add(SectionLabel(Loc.T("about.sec.licenses")));
            Body.Children.Add(Row(Loc.T("about.lic.engine"), "GPLv2", Pill.Make(Sev.Info, "GPLv2")));
            Body.Children.Add(space());
            Body.Children.Add(Row("WinDivert", "LGPLv3 / GPLv2", Pill.Make(Sev.Info, "LGPLv3")));
            Body.Children.Add(space());
            Body.Children.Add(Row(Loc.T("about.lic.flowseal"), "MIT", Pill.Make(Sev.Info, "MIT")));
        }

        void BuildChecksums()
        {
            Body.Children.Add(SectionLabel(Loc.T("about.sec.integrity")));
            var btn = Ctl.Button(Loc.T("about.verify"), Icons.Check, 1);
            btn.HorizontalAlignment = HorizontalAlignment.Left;
            btn.Click += (s, e) => Verify();
            Body.Children.Add(Row(Loc.T("about.checksums"),
                Loc.T("about.checksums.desc"),
                btn));
            _sumResult = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            Body.Children.Add(_sumResult);
        }

        StackPanel _sumResult;

        void Verify()
        {
            _sumResult.Children.Clear();
            string sumsFile = System.IO.Path.Combine(Core.Root, "SHA256SUMS.txt");
            if (!System.IO.File.Exists(sumsFile))
            {
                _sumResult.Children.Add(Warn(Loc.T("about.sums.absent")));
                return;
            }
            int ok = 0, bad = 0, miss = 0;
            foreach (var line in System.IO.File.ReadAllLines(sumsFile))
            {
                string s = line.Trim();
                if (s.Length == 0 || s.StartsWith("#")) continue;
                int sp = s.IndexOf(' ');
                if (sp < 0) continue;
                string hash = s.Substring(0, sp).Trim().ToLowerInvariant();
                string rel = s.Substring(sp).Trim().TrimStart('*').Trim();
                string full = System.IO.Path.Combine(Core.Root, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (!System.IO.File.Exists(full)) { miss++; _sumResult.Children.Add(SumLine(rel, Sev.Warn, Loc.T("about.sums.missing"))); continue; }
                string actual;
                try { actual = Sha256(full); }
                catch { miss++; _sumResult.Children.Add(SumLine(rel, Sev.Warn, Loc.T("about.sums.missing"))); continue; }
                if (actual == hash) { ok++; _sumResult.Children.Add(SumLine(rel, Sev.Ok, Loc.T("about.sums.match"))); }
                else { bad++; _sumResult.Children.Add(SumLine(rel, Sev.Err, Loc.T("about.sums.mismatch"))); }
            }
            string msg = string.Format(Loc.T("about.sums.summary"), ok, bad, miss);
            Core.Info(msg);
            _sumResult.Children.Insert(0, UI.T(msg, Theme.FsBody, bad > 0 ? Theme.BrErr : Theme.BrOk, FontWeights.SemiBold));
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

        static UIElement space() { return new Border { Height = 8 }; }
    }
}
