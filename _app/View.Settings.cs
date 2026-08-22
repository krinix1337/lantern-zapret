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
            var appAuto = new Toggle(Loc.T("settings.autostart"));
            appAuto.IsChecked = Core.AppAutostartEnabled();
            appAuto.Checked += (s, e) => Core.SetAppAutostart(true);
            appAuto.Unchecked += (s, e) => Core.SetAppAutostart(false);
            Body.Children.Add(Row(Loc.T("settings.autostart"), Loc.T("settings.autostart.desc"), appAuto));
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
            if (Theme.Mode != ThemeMode.Peter) return;
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
            _win.PeterMusic.StateChanged += updateMusicBtn;

            Body.Children.Add(Row(Loc.T("settings.peter.song"), Loc.T("settings.peter.song.desc"), musicBtn));
        }

        void BuildUpdates()
        {
            Body.Children.Add(SectionLabel(Loc.T("settings.sec.updates")));

            // zapret версия
            var zapLeft = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            zapLeft.Children.Add(UI.T(Loc.T("settings.zapretVer"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            _zapLocalVersion = new TextBlock { Text = Loc.T("settings.localVersion") + Core.ZapretVersion(),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 3, 0, 0) };
            zapLeft.Children.Add(_zapLocalVersion);
            _zapStatusLine = new TextBlock { Text = Loc.T("settings.checkingOnStart"), Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            zapLeft.Children.Add(_zapStatusLine);
            _zapProgress = new UpdateProgressBar();
            zapLeft.Children.Add(_zapProgress.View);

            _zapUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _zapUpdateBtn.Visibility = Visibility.Collapsed;
            _zapUpdateBtn.Click += (s, e) => _win.UpdateZapret(_zapLatest);
            var zapBtnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
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
            _tgLocalVersion = new TextBlock {
                Text = Loc.T("settings.localVersion") + (string.IsNullOrEmpty(tgLocal) ? "—" : NormVer(tgLocal)),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 3, 0, 0) };
            tgLeft.Children.Add(_tgLocalVersion);
            _tgStatusLine = new TextBlock { Text = Loc.T("settings.checkingOnStart"), Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            tgLeft.Children.Add(_tgStatusLine);
            _tgProgress = new UpdateProgressBar();
            tgLeft.Children.Add(_tgProgress.View);

            _tgUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            _tgUpdateBtn.Click += (s, e) => UpdateTgProxy();
            var tgBtnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
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
            _appLocalVersion = new TextBlock { Text = Loc.T("settings.localVersion") + Core.AppVersion,
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 3, 0, 0) };
            appLeft.Children.Add(_appLocalVersion);
            _appStatusLine = new TextBlock { Text = Loc.T("settings.checkingOnStart"), Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap };
            appLeft.Children.Add(_appStatusLine);
            _appProgress = new UpdateProgressBar();
            appLeft.Children.Add(_appProgress.View);

            _appUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            _appUpdateBtn.Click += (s, e) => DoAppUpdate();
            var appBtnRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
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
        TextBlock _tgLocalVersion;
        TextBlock _zapLocalVersion;
        TextBlock _appLocalVersion;
        UpdateProgressBar _zapProgress;
        UpdateProgressBar _tgProgress;
        UpdateProgressBar _appProgress;
        // Кнопки обновления (ручная проверка удалена — используется автоматическая через SetAutomaticUpdateResults).
        Button _zapUpdateBtn;
        Button _tgUpdateBtn;
        Button _appUpdateBtn;
        string _zapLatest;

        // Вызывается главным окном после единой автоматической проверки при старте.
        // Статус остаётся в настройках и не превращается обратно в кнопку через 5 секунд.
        public void SetAutomaticUpdateResults(string zapretLatest, string zapretLocal,
            string tgLatest, string tgLocal, string appLatest, string appLocal)
        {
            string zLat = NormVer(zapretLatest);
            string zLoc = NormVer(zapretLocal);
            string tgLat = NormVer(tgLatest);
            string tgLoc = NormVer(tgLocal);
            string apLat = NormVer(appLatest);
            string apLoc = NormVer(appLocal);

            _zapLatest = zLat;
            if (_zapLocalVersion != null)
                _zapLocalVersion.Text = Loc.T("settings.localVersion") + zLoc;
            if (_tgLocalVersion != null)
                _tgLocalVersion.Text = Loc.T("settings.localVersion") + (string.IsNullOrEmpty(tgLoc) ? "—" : tgLoc);
            if (_appLocalVersion != null)
                _appLocalVersion.Text = Loc.T("settings.localVersion") + apLoc;

            SetAutomaticStatus(_zapStatusLine, _zapUpdateBtn, zLat, zLoc, Core.ReleaseUrl);
            SetAutomaticStatus(_tgStatusLine, _tgUpdateBtn, tgLat, tgLoc, Core.TgProxyReleasePage);
            SetAutomaticStatus(_appStatusLine, _appUpdateBtn, apLat, apLoc, Core.AppReleaseUrl);
        }

        void SetAutomaticStatus(TextBlock line, Button updateButton, string latest, string local, string updateUrl)
        {
            if (line == null || updateButton == null) return;
            var existingProgress = ProgressFor(updateButton);
            if (existingProgress != null) existingProgress.Hide();
            updateButton.Visibility = Visibility.Collapsed;
            if (string.IsNullOrEmpty(latest))
            {
                line.Text = Loc.T("mw.verFail");
                line.Foreground = Theme.BrWarn;
                return;
            }
            if (string.IsNullOrEmpty(local))
            {
                line.Text = string.Format(Loc.T("settings.latestFull"), latest);
                line.Foreground = Theme.BrMuted;
                return;
            }
            int comparison = CompareVersions(latest, local);
            if (comparison > 0)
            {
                line.Text = string.Format(Loc.T("settings.updateFull"), latest);
                line.Foreground = Theme.BrWarn;
                updateButton.Visibility = Visibility.Visible;
                return;
            }
            if (comparison < 0)
            {
                line.Text = string.Format(Loc.T("settings.localNewer"), local, latest);
                line.Foreground = Theme.BrOk;
                return;
            }
            line.Text = string.Format(Loc.T("settings.latestFull"), latest);
            line.Foreground = Theme.BrOk;
        }

        UpdateProgressBar ProgressFor(Button button)
        {
            if (button == _zapUpdateBtn) return _zapProgress;
            if (button == _tgUpdateBtn) return _tgProgress;
            return _appProgress;
        }

        void ShowProgress(UpdateProgressBar progress, TextBlock line, string phase, int percent, Brush color)
        {
            if (line != null)
            {
                line.Text = percent >= 0 ? phase + " — " + percent + "%" : phase;
                line.Foreground = color;
            }
            if (progress != null) progress.Show(phase, percent, color);
        }

        public void SetZapretUpdateProgress(string phase, int percent)
        {
            ShowProgress(_zapProgress, _zapStatusLine, phase, percent, Theme.BrAccent);
        }

        public void FinishZapretUpdate(string text, bool ok)
        {
            FinishProgress(_zapProgress, _zapStatusLine, text, ok);
            if (ok && _zapLocalVersion != null)
            {
                _zapLocalVersion.Text = Loc.T("settings.localVersion") + Core.ZapretVersion();
            }
        }

        void SetTgUpdateProgress(string phase, int percent)
        {
            ShowProgress(_tgProgress, _tgStatusLine, phase, percent, Theme.BrAccent);
        }

        void SetAppUpdateProgress(string phase, int percent)
        {
            ShowProgress(_appProgress, _appStatusLine, phase, percent, Theme.BrAccent);
        }

        void FinishProgress(UpdateProgressBar progress, TextBlock line, string text, bool ok)
        {
            if (line != null) { line.Text = text; line.Foreground = ok ? Theme.BrOk : Theme.BrWarn; }
            if (progress != null)
            {
                progress.Show(text, ok ? 100 : -1, ok ? Theme.BrOk : Theme.BrWarn);
                if (ok)
                {
                    var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
                    tm.Tick += (s, e) => { tm.Stop(); progress.Hide(); };
                    tm.Start();
                }
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

        internal static int CompareVersions(string left, string right)
        {
            if (string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right)) return 0;
            if (string.IsNullOrEmpty(left)) return -1;
            if (string.IsNullOrEmpty(right)) return 1;
            string l = NormVer(left);
            string r = NormVer(right);
            if (string.Equals(l, r, StringComparison.OrdinalIgnoreCase)) return 0;
            Version a, b;
            if (Version.TryParse(l, out a) && Version.TryParse(r, out b)) return a.CompareTo(b);
            return string.Compare(l, r, StringComparison.OrdinalIgnoreCase);
        }

        void UpdateTgProxy()
        {
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            SetTgUpdateProgress(Loc.T("settings.update.downloading"), 0);
            bool wasRunning = Core.TgProxyRunning();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (wasRunning) Core.TgProxyStop();
                    try { System.IO.Directory.CreateDirectory(Core.TgToolsDir); } catch { }
                    bool ok = Core.DownloadFile(Core.TgProxyDownloadUrl(), Core.TgProxyExe, delegate (DlProgress p)
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                int pct = p.Total > 0 ? (int)(p.BytesRead * 88 / p.Total) : -1;
                                SetTgUpdateProgress(Loc.T("settings.update.downloading"), pct);
                            });
                        }
                        catch { }
                    }, null);
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ok)
                        {
                            SetTgUpdateProgress(Loc.T("settings.update.replacing"), 96);
                            string nv = Core.TgProxyLocalVersion();
                            string nvNorm = string.IsNullOrEmpty(nv) ? "—" : NormVer(nv);
                            FinishProgress(_tgProgress, _tgStatusLine,
                                Loc.T("settings.update.done") + ": " + nvNorm, true);
                            if (_tgLocalVersion != null)
                                _tgLocalVersion.Text = Loc.T("settings.localVersion") + nvNorm;
                            _win.ShowToast(Loc.T("tg.dlOk"), Sev.Ok);
                            _win.RefreshSidebarVersions();
                            if (wasRunning) { string tgErr; Core.TgProxyStart(out tgErr); }
                        }
                        else
                        {
                            FinishProgress(_tgProgress, _tgStatusLine, Loc.T("tg.dlFail"), false);
                            _win.ShowToast(Loc.T("tg.dlFail"), Sev.Warn);
                        }
                    });
                }
                catch { }
            });
        }

        void DoAppUpdate()
        {
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            SetAppUpdateProgress(Loc.T("settings.update.downloading"), 0);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string url = Core.AppInstallerUrl();
                    string err;
                    bool ok = Core.SelfUpdate(url, delegate (DlProgress p)
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                int pct = p.Total > 0 ? (int)(p.BytesRead * 92 / p.Total) : -1;
                                SetAppUpdateProgress(Loc.T("settings.update.downloading"), pct);
                            });
                        }
                        catch { }
                    }, out err);
                    string notes = ok ? Core.AppReleaseNotes() : null;
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ok)
                        {
                            SetAppUpdateProgress(Loc.T("settings.update.installer"), 98);
                            FinishProgress(_appProgress, _appStatusLine, Loc.T("settings.app.installerStarted"), true);
                            _win.ShowToast(Loc.T("settings.app.installerStarted"), Sev.Ok);
                            if (!string.IsNullOrEmpty(notes))
                                MessageBox.Show(notes, Loc.T("settings.app.changelog"), MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            FinishProgress(_appProgress, _appStatusLine,
                                Loc.T("tg.dlFail") + (err != null ? ": " + err : ""), false);
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
                Core.IpsetEnabled, Core.DohMode);
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
            Core.GameMode = p.GameMode;
            Core.IpsetEnabled = p.Ipset;
            Core.DohMode = p.Doh;
            // Если обход уже запущен, SelectStrategy перезапустит его с уже
            // применёнными параметрами профиля.
            if (!string.IsNullOrEmpty(p.Strategy)) _win.SelectStrategy(p.Strategy);
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

    // Компактный индикатор для карточек обновления (чистая анимированная полоса без дублирующего текста)
    sealed class UpdateProgressBar
    {
        const double TrackWidth = 240;
        readonly Border _root;
        readonly Border _fill;

        public UIElement View { get { return _root; } }

        public UpdateProgressBar()
        {
            _fill = new Border
            {
                Width = 0,
                Height = 4,
                CornerRadius = Theme.Rpill,
                Background = Theme.BrAccent,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var track = new Border
            {
                Width = TrackWidth,
                Height = 4,
                CornerRadius = Theme.Rpill,
                Background = Theme.BrSurfaceHi,
                ClipToBounds = true,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = _fill
            };

            _root = new Border { Child = track, Visibility = Visibility.Collapsed, Opacity = 0 };
        }

        public void Show(string phase, int percent, Brush color)
        {
            if (_root.Visibility != Visibility.Visible)
            {
                _root.Visibility = Visibility.Visible;
                if (Theme.AnimationsEnabled)
                {
                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
                    _root.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }
                else _root.Opacity = 1;
            }

            int safe = percent < 0 ? 20 : Math.Max(0, Math.Min(100, percent));
            _fill.Background = color;
            double target = TrackWidth * safe / 100.0;
            if (Theme.AnimationsEnabled)
            {
                var animation = new DoubleAnimation(target, TimeSpan.FromMilliseconds(160))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                _fill.BeginAnimation(FrameworkElement.WidthProperty, animation);
            }
            else
            {
                _fill.BeginAnimation(FrameworkElement.WidthProperty, null);
                _fill.Width = target;
            }
        }

        public void Hide()
        {
            if (_root.Visibility == Visibility.Collapsed) return;
            if (Theme.AnimationsEnabled)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fadeOut.Completed += (s, e) =>
                {
                    _fill.BeginAnimation(FrameworkElement.WidthProperty, null);
                    _fill.Width = 0;
                    _root.Visibility = Visibility.Collapsed;
                };
                _root.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            else
            {
                _fill.BeginAnimation(FrameworkElement.WidthProperty, null);
                _fill.Width = 0;
                _root.Opacity = 0;
                _root.Visibility = Visibility.Collapsed;
            }
        }
    }
}
