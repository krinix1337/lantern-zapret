using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            BuildLaunch();
            BuildCheck();
            BuildDns();
            BuildInterface();
            BuildUpdates();
            BuildWatchdog();
            BuildProfiles();
            BuildPrivacy();
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

        void BuildLaunch()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.launch")));
            Body.Children.Add(Row(Loc.T("settings.autorun"), Loc.T("settings.autorun.desc"),
                Tog("autostart_run", false, Loc.T("settings.autorun"), null)));
            Body.Children.Add(space());
            Body.Children.Add(Row(Loc.T("settings.autostart"), Loc.T("settings.autostart.desc"),
                Tog("autostart_app", false, Loc.T("settings.autostart"), null)));
            Body.Children.Add(space());
            var tgAuto = new Toggle(Loc.T("settings.tgAutostart"));
            tgAuto.IsChecked = Core.TgAutostartEnabled();
            tgAuto.Checked += (s, e) => Core.SetTgAutostart(true);
            tgAuto.Unchecked += (s, e) => Core.SetTgAutostart(false);
            Body.Children.Add(Row(Loc.T("settings.tgAutostart"), Loc.T("settings.tgAutostart.desc"), tgAuto));
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
        }

        void BuildInterface()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.interface")));

            var theme = Combo(180);
            Ctl.AutomationSetName(theme, Loc.T("settings.theme"));
            theme.Items.Add(Loc.T("settings.theme.dark"));
            theme.Items.Add(Loc.T("settings.theme.amoled"));
            theme.Items.Add(Loc.T("settings.theme.light"));
            theme.SelectedIndex = Theme.Mode == ThemeMode.Amoled ? 1 : Theme.Mode == ThemeMode.Light ? 2 : 0;
            theme.SelectionChanged += (s, e) =>
            {
                ThemeMode next = theme.SelectedIndex == 2 ? ThemeMode.Light : theme.SelectedIndex == 1 ? ThemeMode.Amoled : ThemeMode.Dark;
                if (next == Theme.Mode) return;
                Core.Set("theme", next == ThemeMode.Light ? "light" : next == ThemeMode.Amoled ? "amoled" : "dark");
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
        }

        void BuildUpdates()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.updates")));
            Body.Children.Add(Row(Loc.T("settings.checkUpdates"), Loc.T("settings.checkUpdates.desc"),
                Tog("check_updates", true, Loc.T("settings.checkUpdates"), null)));
            Body.Children.Add(space());

            // zapret версия
            var zapLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            zapLeft.Children.Add(UI.T(Loc.T("settings.zapretVer"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            zapLeft.Children.Add(new TextBlock { Text = Loc.T("settings.localVersion") + Core.ZapretVersion(),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 3, 0, 0) });
            _zapStatusLine = new TextBlock { Text = "", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            zapLeft.Children.Add(_zapStatusLine);

            _zapCheckBtn = Ctl.Button(Loc.T("settings.checkNow"), Icons.Refresh, 1);
            _zapCheckBtn.Click += (s, e) => CheckZapretVersion();
            _zapUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _zapUpdateBtn.Margin = new Thickness(10, 0, 0, 0);
            _zapUpdateBtn.Visibility = Visibility.Collapsed;
            _zapUpdateBtn.Click += (s, e) => _win.CheckUpdates();
            var zapBtnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            zapBtnRow.Children.Add(_zapCheckBtn);
            zapBtnRow.Children.Add(_zapUpdateBtn);

            var zapGrid = new Grid();
            zapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            zapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(zapLeft, 0); zapGrid.Children.Add(zapLeft);
            var zapBtnWrap = new ContentControl { Content = zapBtnRow, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(zapBtnWrap, 1); zapGrid.Children.Add(zapBtnWrap);
            Body.Children.Add(UI.Card(zapGrid, new Thickness(16, 14, 16, 14)));
            Body.Children.Add(space());

            // Telegram-прокси версия (одинаковая структура с zapret)
            string tgLocal = Core.TgProxyInstalled() ? Core.TgProxyLocalVersion() : null;
            var tgLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            tgLeft.Children.Add(UI.T(Loc.T("settings.tgVer"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            tgLeft.Children.Add(new TextBlock {
                Text = Loc.T("settings.localVersion") + (string.IsNullOrEmpty(tgLocal) ? "—" : NormVer(tgLocal)),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 3, 0, 0) });
            _tgStatusLine = new TextBlock { Text = "", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            tgLeft.Children.Add(_tgStatusLine);

            _tgCheckBtn = Ctl.Button(Loc.T("settings.checkNow"), Icons.Refresh, 1);
            _tgCheckBtn.Click += (s, e) => CheckTgVersion();
            _tgUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _tgUpdateBtn.Margin = new Thickness(10, 0, 0, 0);
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            _tgUpdateBtn.Click += (s, e) => UpdateTgProxy();
            var tgBtnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            tgBtnRow.Children.Add(_tgCheckBtn);
            tgBtnRow.Children.Add(_tgUpdateBtn);

            var tgGrid = new Grid();
            tgGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            tgGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(tgLeft, 0); tgGrid.Children.Add(tgLeft);
            var tgBtnWrap = new ContentControl { Content = tgBtnRow, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(tgBtnWrap, 1); tgGrid.Children.Add(tgBtnWrap);
            Body.Children.Add(UI.Card(tgGrid, new Thickness(16, 14, 16, 14)));
            Body.Children.Add(space());

            // Версия самого приложения (Lantern)
            var appLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            appLeft.Children.Add(UI.T(Loc.T("settings.appVer"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            appLeft.Children.Add(new TextBlock { Text = Loc.T("settings.localVersion") + Core.AppVersion,
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 3, 0, 0) });
            _appStatusLine = new TextBlock { Text = "", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            appLeft.Children.Add(_appStatusLine);

            _appCheckBtn = Ctl.Button(Loc.T("settings.checkNow"), Icons.Refresh, 1);
            _appCheckBtn.Click += (s, e) => CheckAppVersion();
            _appUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _appUpdateBtn.Margin = new Thickness(10, 0, 0, 0);
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            _appUpdateBtn.Click += (s, e) => DoAppUpdate();
            var appBtnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            appBtnRow.Children.Add(_appCheckBtn);
            appBtnRow.Children.Add(_appUpdateBtn);

            var appGrid = new Grid();
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(appLeft, 0); appGrid.Children.Add(appLeft);
            var appBtnWrap = new ContentControl { Content = appBtnRow, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(appBtnWrap, 1); appGrid.Children.Add(appBtnWrap);
            Body.Children.Add(UI.Card(appGrid, new Thickness(16, 14, 16, 14)));
        }

        TextBlock _tgStatusLine;
        TextBlock _zapStatusLine;
        TextBlock _appStatusLine;
        Button _zapCheckBtn, _zapUpdateBtn;
        Button _tgCheckBtn, _tgUpdateBtn;
        Button _appCheckBtn, _appUpdateBtn;
        string _appUpdateUrl;

        void CheckZapretVersion()
        {
            _zapStatusLine.Text = Loc.T("mw.checkVer");
            _zapUpdateBtn.Visibility = Visibility.Collapsed;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string latest = Core.CheckLatestVersion();
                    string local = Core.ZapretVersion();
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (latest == null)
                        {
                            _zapStatusLine.Text = Loc.T("mw.verFail");
                            return;
                        }
                        if (NormVer(latest) == NormVer(local))
                        {
                            _zapStatusLine.Text = string.Format(Loc.T("mw.verOk"), NormVer(latest));
                            _zapStatusLine.Foreground = Theme.BrOk;
                            // Зелёная кнопка "Последняя версия" на 5 секунд
                            var greenBtn = Ctl.Button(Loc.T("settings.latestVer"), Icons.Check, 0);
                            greenBtn.Click += (s2, e2) => { };
                            var parent = _zapCheckBtn.Parent as StackPanel;
                            if (parent != null)
                            {
                                int idx = parent.Children.IndexOf(_zapCheckBtn);
                                if (idx >= 0) { parent.Children.RemoveAt(idx); parent.Children.Insert(idx, greenBtn); }
                            }
                            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                            timer.Tick += (s2, e2) =>
                            {
                                timer.Stop();
                                _zapStatusLine.Foreground = Theme.BrMuted;
                                if (parent != null)
                                {
                                    int idx2 = parent.Children.IndexOf(greenBtn);
                                    if (idx2 >= 0) { parent.Children.RemoveAt(idx2); parent.Children.Insert(idx2, _zapCheckBtn); }
                                }
                            };
                            timer.Start();
                        }
                        else
                        {
                            _zapStatusLine.Text = string.Format(Loc.T("mw.verNew"), NormVer(latest), NormVer(local));
                            _zapStatusLine.Foreground = Theme.BrWarn;
                            _zapUpdateBtn.Visibility = Visibility.Visible;
                        }
                    });
                }
                catch { }
            });
        }

        internal static string NormVer(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            v = v.TrimStart('v', 'V');
            while (v.EndsWith(".0")) v = v.Substring(0, v.Length - 2);
            return v;
        }

        void CheckTgVersion()
        {
            _tgStatusLine.Text = Loc.T("settings.tg.checking");
            _tgStatusLine.Foreground = Theme.BrMuted;
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            string local = Core.TgProxyInstalled() ? Core.TgProxyLocalVersion() : null;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string latest = Core.TgProxyLatestVersion();
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (latest == null)
                        {
                            _tgStatusLine.Text = Loc.T("mw.verFail");
                            _tgStatusLine.Foreground = Theme.BrWarn;
                            return;
                        }
                        if (!Core.TgProxyInstalled() || string.IsNullOrEmpty(local))
                        {
                            _tgStatusLine.Text = string.Format(Loc.T("settings.tg.latestOnly"), latest);
                            _tgStatusLine.Foreground = Theme.BrMuted;
                            return;
                        }
                        if (NormVer(local) == NormVer(latest))
                        {
                            _tgStatusLine.Text = string.Format(Loc.T("mw.verOk"), latest);
                            _tgStatusLine.Foreground = Theme.BrOk;
                            var greenBtn = Ctl.Button(Loc.T("settings.latestVer"), Icons.Check, 0);
                            greenBtn.Click += (s2, e2) => { };
                            var parent = _tgCheckBtn.Parent as StackPanel;
                            if (parent != null)
                            {
                                int idx = parent.Children.IndexOf(_tgCheckBtn);
                                if (idx >= 0) { parent.Children.RemoveAt(idx); parent.Children.Insert(idx, greenBtn); }
                            }
                            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                            timer.Tick += (s2, e2) =>
                            {
                                timer.Stop();
                                if (parent != null)
                                {
                                    int idx2 = parent.Children.IndexOf(greenBtn);
                                    if (idx2 >= 0) { parent.Children.RemoveAt(idx2); parent.Children.Insert(idx2, _tgCheckBtn); }
                                }
                            };
                            timer.Start();
                        }
                        else
                        {
                            _tgStatusLine.Text = string.Format(Loc.T("mw.verNew"), NormVer(latest), NormVer(local));
                            _tgStatusLine.Foreground = Theme.BrWarn;
                            _tgUpdateBtn.Visibility = Visibility.Visible;
                        }
                    });
                }
                catch { }
            });
        }

        void UpdateTgProxy()
        {
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            _tgStatusLine.Text = Loc.T("tg.dlProgress");
            _tgStatusLine.Foreground = Theme.BrMuted;
            string url = Core.TgProxyDownloadUrl();
            string dest = Core.TgProxyExe;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    try { System.IO.Directory.CreateDirectory(Core.TgToolsDir); } catch { }
                    bool ok = Core.DownloadFile(url, dest, null, null);
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ok)
                        {
                            string nv = Core.TgProxyLocalVersion();
                            _tgStatusLine.Text = string.Format(Loc.T("mw.verOk"), string.IsNullOrEmpty(nv) ? "—" : NormVer(nv));
                            _tgStatusLine.Foreground = Theme.BrOk;
                            _win.ShowToast(Loc.T("tg.dlOk"), Sev.Ok);
                        }
                        else
                        {
                            _tgStatusLine.Text = Loc.T("tg.dlFail");
                            _tgStatusLine.Foreground = Theme.BrWarn;
                            _win.ShowToast(Loc.T("tg.dlFail"), Sev.Warn);
                        }
                    });
                }
                catch { }
            });
        }

        void CheckAppVersion()
        {
            _appStatusLine.Text = Loc.T("mw.checkVer");
            _appStatusLine.Foreground = Theme.BrMuted;
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string latest = Core.AppLatestVersion();
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (latest == null)
                        {
                            _appStatusLine.Text = Loc.T("mw.verFail");
                            _appStatusLine.Foreground = Theme.BrWarn;
                            return;
                        }
                        if (NormVer(latest) == NormVer(Core.AppVersion))
                        {
                            _appStatusLine.Text = string.Format(Loc.T("mw.verOk"), NormVer(latest));
                            _appStatusLine.Foreground = Theme.BrOk;
                            var greenBtn = Ctl.Button(Loc.T("settings.latestVer"), Icons.Check, 0);
                            greenBtn.Click += (s2, e2) => { };
                            var parent = _appCheckBtn.Parent as StackPanel;
                            if (parent != null)
                            {
                                int idx = parent.Children.IndexOf(_appCheckBtn);
                                if (idx >= 0) { parent.Children.RemoveAt(idx); parent.Children.Insert(idx, greenBtn); }
                            }
                            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                            timer.Tick += (s2, e2) =>
                            {
                                timer.Stop();
                                if (parent != null)
                                {
                                    int idx2 = parent.Children.IndexOf(greenBtn);
                                    if (idx2 >= 0) { parent.Children.RemoveAt(idx2); parent.Children.Insert(idx2, _appCheckBtn); }
                                }
                            };
                            timer.Start();
                        }
                        else
                        {
                            _appStatusLine.Text = string.Format(Loc.T("mw.verNew"), NormVer(latest), Core.AppVersion);
                            _appStatusLine.Foreground = Theme.BrWarn;
                            _appUpdateUrl = Core.AppInstallerUrl();
                            _appUpdateBtn.Visibility = Visibility.Visible;
                        }
                    });
                }
                catch { }
            });
        }

        void DoAppUpdate()
        {
            if (string.IsNullOrEmpty(_appUpdateUrl)) return;
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            _appStatusLine.Text = Loc.T("settings.app.downloading");
            _appStatusLine.Foreground = Theme.BrMuted;
            string url = _appUpdateUrl;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string err;
                    bool ok = Core.SelfUpdate(url, out err);
                    string notes = ok ? Core.AppReleaseNotes() : null;
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ok)
                        {
                            _appStatusLine.Text = Loc.T("settings.app.installerStarted");
                            _appStatusLine.Foreground = Theme.BrOk;
                            _win.ShowToast(Loc.T("settings.app.installerStarted"), Sev.Ok);
                            if (!string.IsNullOrEmpty(notes))
                                MessageBox.Show(notes, Loc.T("settings.app.changelog"), MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            _appStatusLine.Text = Loc.T("tg.dlFail") + (err != null ? ": " + err : "");
                            _appStatusLine.Foreground = Theme.BrWarn;
                        }
                    });
                }
                catch { }
            });
        }

        void BuildWatchdog()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.watchdog")));
            var wdToggle = Tog("watchdog_enabled", false, Loc.T("settings.watchdog"), delegate (bool on)
            {
                Core.WatchdogEnabled = on;
                _win.RestartWatchdog();
            });
            Body.Children.Add(Row(Loc.T("settings.watchdog"), Loc.T("settings.watchdog.desc"), wdToggle));
            Body.Children.Add(space());

            var interval = Combo(120);
            int cur = Core.WatchdogIntervalMin;
            int[] opts = { 2, 5, 10, 15, 30 };
            int selIdx = 1;
            for (int i = 0; i < opts.Length; i++)
            {
                interval.Items.Add(opts[i] + " " + Loc.T("time.min"));
                if (opts[i] == cur) selIdx = i;
            }
            interval.SelectedIndex = selIdx;
            interval.SelectionChanged += (s, e) =>
            {
                Core.WatchdogIntervalMin = opts[interval.SelectedIndex];
                _win.RestartWatchdog();
            };
            Body.Children.Add(Row(Loc.T("settings.watchdog.interval"), Loc.T("settings.watchdog.interval.desc"), interval));
        }

        ComboBox _profileSelector;

        void BuildProfiles()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.profiles")));

            var panel = new StackPanel();
            var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _profileSelector = Combo(200);
            RefreshProfileList();
            row1.Children.Add(_profileSelector);

            var loadBtn = Ctl.Button(Loc.T("settings.profile.load"), Icons.Play, 0);
            loadBtn.Margin = new Thickness(8, 0, 0, 0);
            loadBtn.Click += (s, e) => LoadProfile();
            row1.Children.Add(loadBtn);

            var delBtn = Ctl.Button(Loc.T("settings.profile.del"), Icons.Cross, 2);
            delBtn.Margin = new Thickness(8, 0, 0, 0);
            delBtn.Click += (s, e) => DeleteProfile();
            row1.Children.Add(delBtn);
            panel.Children.Add(row1);

            var saveBtn = Ctl.Button(Loc.T("settings.profile.save"), Icons.Save, 1);
            saveBtn.Click += (s, e) => SaveProfile();
            panel.Children.Add(saveBtn);

            Body.Children.Add(UI.Card(panel, new Thickness(16, 14, 16, 14)));
        }

        void RefreshProfileList()
        {
            if (_profileSelector == null) return;
            _profileSelector.Items.Clear();
            var profiles = Core.GetProfiles();
            foreach (var p in profiles)
                _profileSelector.Items.Add(p.Name);
            if (_profileSelector.Items.Count > 0) _profileSelector.SelectedIndex = 0;
        }

        void SaveProfile()
        {
            string name = _win.CurrentStrategyName();
            if (string.IsNullOrEmpty(name)) name = "default";
            // Диалог ввода имени через простой prompt.
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                Loc.T("settings.profile.namePrompt"), Loc.T("settings.sec.profiles"), name);
            if (string.IsNullOrEmpty(input.Trim())) return;
            input = input.Trim();
            Core.SaveProfile(input, _win.CurrentStrategyFile() ?? "", Core.GameMode,
                System.IO.File.Exists(Core.IpsetFile), Core.DohMode);
            RefreshProfileList();
            Core.Good(string.Format(Loc.T("settings.profile.saved"), input));
        }

        void LoadProfile()
        {
            if (_profileSelector.SelectedIndex < 0) return;
            string name = _profileSelector.SelectedItem as string;
            if (name == null) return;
            var profiles = Core.GetProfiles();
            Core.Profile p = null;
            foreach (var pr in profiles) if (pr.Name == name) { p = pr; break; }
            if (p == null) return;
            if (!string.IsNullOrEmpty(p.Strategy)) _win.SelectStrategy(p.Strategy);
            Core.GameMode = p.GameMode;
            Core.DohMode = p.Doh;
            Core.SaveConfig();
            Core.Good(string.Format(Loc.T("settings.profile.loaded"), name));
        }

        void DeleteProfile()
        {
            if (_profileSelector.SelectedIndex < 0) return;
            string name = _profileSelector.SelectedItem as string;
            if (name == null) return;
            Core.DeleteProfile(name);
            RefreshProfileList();
            Core.Info(string.Format(Loc.T("settings.profile.deleted"), name));
        }

        void BuildPrivacy()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.privacy")));
            Body.Children.Add(NoteCard(Icons.Shield, Theme.BrOk, Loc.T("settings.privacy.note"), Sev.Ok));
        }

        static UIElement space() { return new Border { Height = 10 }; }
    }
}
