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

        public SettingsPage(MainWindow win)
        {
            _win = win;
            BuildGeneral();
            BuildCheck();
            BuildDns();
            BuildInterface();
            BuildPrivacy();
            BuildAntivirus();
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
                to.Items.Add(v + (Loc.Lang == "en" ? " s" : " сек"));
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
            Ctl.AutomationSetName(theme, Loc.T("settings.theme"));
            theme.Items.Add(Loc.T("settings.theme.dark"));
            theme.Items.Add(Loc.T("settings.theme.amoled"));
            theme.Items.Add(Loc.T("settings.theme.light"));
            theme.Items.Add(Loc.T("settings.theme.aurora"));
            theme.Items.Add(Loc.T("settings.theme.sunset"));
            theme.Items.Add(Loc.T("settings.theme.peter"));
            theme.SelectedIndex = Theme.Mode == ThemeMode.Amoled ? 1 : Theme.Mode == ThemeMode.Light ? 2 : Theme.Mode == ThemeMode.Aurora ? 3 : Theme.Mode == ThemeMode.Sunset ? 4 : Theme.Mode == ThemeMode.Peter ? 5 : 0;
            theme.SelectionChanged += (s, e) =>
            {
                ThemeMode next = theme.SelectedIndex == 5 ? ThemeMode.Peter : theme.SelectedIndex == 4 ? ThemeMode.Sunset : theme.SelectedIndex == 3 ? ThemeMode.Aurora : theme.SelectedIndex == 2 ? ThemeMode.Light : theme.SelectedIndex == 1 ? ThemeMode.Amoled : ThemeMode.Dark;
                if (next == Theme.Mode) return;
                Core.Set("theme", next == ThemeMode.Light ? "light" : next == ThemeMode.Amoled ? "amoled" : next == ThemeMode.Aurora ? "aurora" : next == ThemeMode.Sunset ? "sunset" : next == ThemeMode.Peter ? "peter" : "dark");
                Core.SaveConfig();
                Theme.Apply(next);
            };
            Body.Children.Add(Row(Loc.T("settings.theme"), Loc.T("settings.theme.desc"), theme));
            Body.Children.Add(space());

            var lang = Combo(180);
            Ctl.AutomationSetName(lang, Loc.T("settings.lang"));
            lang.Items.Add("Русский");
            lang.Items.Add("English");
            lang.SelectedIndex = Loc.Lang == "en" ? 1 : 0;
            lang.SelectionChanged += (s, e) =>
            {
                string next = lang.SelectedIndex == 1 ? "en" : "ru";
                if (next == Loc.Lang) return;
                Loc.SetLang(next);
            };
            Body.Children.Add(Row(Loc.T("settings.lang"), Loc.T("settings.lang.desc"), lang));
            Body.Children.Add(space());

            Body.Children.Add(Row(Loc.T("settings.reduceMotion"), Loc.T("settings.reduceMotion.desc"),
                Tog("reduce_motion", false, Loc.T("settings.reduceMotion"), null)));
            BuildPeterMode();
        }

        // Этот блок намеренно не существует в остальных темах.
        void BuildPeterMode()
        {
            if (Theme.Mode != ThemeMode.Peter && Theme.CurrentTheme != "peter") return;
            Body.Children.Add(space());
            Body.Children.Add(SectionLabel(Loc.T("settings.peter.sec")));
            Body.Children.Add(Row(Loc.T("settings.peter.backdrop"), Loc.T("settings.peter.backdrop.desc"),
                Tog("peter_backdrop", true, Loc.T("settings.peter.backdrop"), delegate (bool on)
                {
                    Theme.Apply(ThemeMode.Peter);
                })));
            Body.Children.Add(space());

            bool active = _win.PeterMusic.IsActive;
            var musicBtn = Ctl.Button(
                active ? Loc.T("settings.peter.song.stop") : Loc.T("settings.peter.song.play"),
                active ? Icons.Stop : Icons.Play,
                active ? 3 : 0);
            musicBtn.Click += (s, e) => _win.TogglePeterMusic();

            Action updateMusicBtn = delegate
            {
                bool isAct = _win.PeterMusic.IsActive;
                Ctl.SetButton(musicBtn,
                    isAct ? Loc.T("settings.peter.song.stop") : Loc.T("settings.peter.song.play"),
                    isAct ? Icons.Stop : Icons.Play,
                    isAct ? 3 : 0);
            };
            // Страницы пересоздаются при каждой смене темы/языка, а контроллер
            // один на окно: без отписки обработчики накапливаются.
            _win.PeterMusic.StateChanged += updateMusicBtn;
            _musicBtnHandler = updateMusicBtn;

            Body.Children.Add(Row(Loc.T("settings.peter.song"), Loc.T("settings.peter.song.desc"), musicBtn));
        }

        Action _musicBtnHandler;

        public override void OnHide()
        {
            if (_musicBtnHandler != null)
            {
                try { _win.PeterMusic.StateChanged -= _musicBtnHandler; } catch { }
                _musicBtnHandler = null;
            }
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

        void BuildPrivacy()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.privacy")));
            Body.Children.Add(NoteCard(Icons.Shield, Theme.BrOk, Loc.T("settings.privacy.note"), Sev.Ok));
        }

        void BuildAntivirus()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.antivirus")));
            Body.Children.Add(NoteCard(Icons.Shield, Theme.BrWarn, Loc.T("settings.defender.note"), Sev.Warn));
            Body.Children.Add(space());

            var btn = Ctl.Button(Loc.T("settings.defender.btn"), Icons.Shield, 1);
            btn.HorizontalAlignment = HorizontalAlignment.Left;
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

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(btn, 0);
            Grid.SetColumn(pillHost, 2);
            row.Children.Add(btn);
            row.Children.Add(pillHost);

            Body.Children.Add(row);
        }

        static UIElement space() { return new Border { Height = 10 }; }
    }
}
