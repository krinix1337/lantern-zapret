using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZapretStudio
{
    class ServicePage : Page
    {
        public override string Title { get { return Loc.T("service.title"); } }
        public override string Subtitle { get { return Loc.T("service.sub"); } }

        readonly MainWindow _win;
        Border _statePill, _wdPill;
        TextBlock _stratVal;
        ComboBox _stratPick;
        DispatcherTimer _timer;

        // Элементы управления автозапуском
        Toggle _appAutoToggle;
        Toggle _appTrayToggle;
        Border _appTrayRow;
        Toggle _tgAutoToggle;
        Toggle _bypassAppToggle;
        Toggle _tgAppToggle;
        Border _bypassAppRow;
        TextBlock _bypassAppDesc;
        readonly bool[] _appAutoReady = new bool[1];

        public ServicePage(MainWindow win)
        {
            _win = win;
            BuildStatus();
            BuildServiceConfig();
            BuildWindowsAutostart();
            BuildAppAutostart();
            BuildAdminNote();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (s, e) => Refresh();
        }

        public override void OnShow() { Refresh(); _timer.Start(); }
        public override void OnHide() { _timer.Stop(); }

        void BuildStatus()
        {
            Body.Children.Add(SectionLabel(Loc.T("service.sec.status")));
            var sp = new StackPanel();
            var g1 = new Grid();
            g1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g1.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l1 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            l1.Children.Add(UI.T(Loc.T("service.svc"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            _stratVal = new TextBlock { Text = "", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0) };
            l1.Children.Add(_stratVal);
            Grid.SetColumn(l1, 0); g1.Children.Add(l1);
            _statePill = Pill.Make(Sev.Neutral, "—");
            _statePill.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_statePill, 1); g1.Children.Add(_statePill);
            sp.Children.Add(UI.Card(g1, new Thickness(16, 14, 16, 14)));

            sp.Children.Add(new Border { Height = 12 });

            var g2 = new Grid();
            g2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l2 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            l2.Children.Add(UI.T(Loc.T("service.driver"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            l2.Children.Add(new TextBlock { Text = Loc.T("service.driver.desc"),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0) });
            Grid.SetColumn(l2, 0); g2.Children.Add(l2);
            _wdPill = Pill.Make(Sev.Neutral, "—");
            _wdPill.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_wdPill, 1); g2.Children.Add(_wdPill);
            sp.Children.Add(UI.Card(g2, new Thickness(16, 14, 16, 14)));
            Body.Children.Add(sp);
        }

        void BuildServiceConfig()
        {
            Body.Children.Add(SectionLabel(Loc.T("service.sec.serviceConfig")));
            _stratPick = Combo(380);
            Ctl.AutomationSetName(_stratPick, Loc.T("service.pick.name"));
            foreach (var f in Core.GetStrategyFiles()) _stratPick.Items.Add(Core.PrettyName(f));
            if (_stratPick.Items.Count > 0) _stratPick.SelectedIndex = 0;
            Body.Children.Add(Row(Loc.T("service.which"), Loc.T("service.which.desc"), _stratPick));

            Body.Children.Add(new Border { Height = 12 });

            var wrap = new WrapPanel();
            var install = Ctl.Button(Loc.T("service.install"), Icons.Server, 0);
            install.Margin = new Thickness(0, 0, 10, 10);
            install.Click += (s, e) => DoInstall();
            var start = Ctl.Button(Loc.T("service.start"), Icons.Play, 1);
            start.Margin = new Thickness(0, 0, 10, 10);
            start.Click += (s, e) => Guarded(Loc.T("service.task.start"), delegate { if (!Core.StartService()) throw new Exception("sc start failed"); Core.Good(Loc.T("service.started")); });
            var stop = Ctl.Button(Loc.T("service.stop"), Icons.Stop, 1);
            stop.Margin = new Thickness(0, 0, 10, 10);
            stop.Click += (s, e) => Guarded(Loc.T("service.task.stop"), delegate { if (!Core.StopService()) throw new Exception("sc stop failed"); Core.Info(Loc.T("service.svcStopped")); });
            var remove = Ctl.Button(Loc.T("service.remove"), Icons.Cross, 2);
            remove.Margin = new Thickness(0, 0, 10, 10);
            remove.Click += (s, e) => DoRemove();
            wrap.Children.Add(install);
            wrap.Children.Add(start);
            wrap.Children.Add(stop);
            wrap.Children.Add(remove);
            Body.Children.Add(wrap);
        }

        void BuildWindowsAutostart()
        {
            Body.Children.Add(SectionLabel(Loc.T("service.sec.winAutostart")));

            _appAutoToggle = new Toggle(Loc.T("service.appAuto"));
            _appAutoToggle.Checked += (s, e) =>
            {
                if (_appTrayRow != null) _appTrayRow.Visibility = Visibility.Visible;
                if (_appAutoReady[0])
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { Core.SetAppAutostart(true); });
            };
            _appAutoToggle.Unchecked += (s, e) =>
            {
                if (_appTrayRow != null) _appTrayRow.Visibility = Visibility.Collapsed;
                if (_appAutoReady[0])
                    System.Threading.ThreadPool.QueueUserWorkItem(delegate { Core.SetAppAutostart(false); });
            };

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool on = Core.AppAutostartEnabled();
                try
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        _appAutoToggle.IsChecked = on;
                        if (_appTrayRow != null) _appTrayRow.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
                        _appAutoReady[0] = true;
                    });
                }
                catch { }
            });

            Body.Children.Add(Row(Loc.T("service.appAuto"), Loc.T("service.appAuto.desc"), _appAutoToggle));

            Body.Children.Add(new Border { Height = 10 });

            _appTrayToggle = new Toggle(Loc.T("service.appAutoTray"));
            _appTrayToggle.IsChecked = Core.AppAutostartInTray();
            _appTrayToggle.Checked += (s, e) => Core.SetAppAutostartInTray(true);
            _appTrayToggle.Unchecked += (s, e) => Core.SetAppAutostartInTray(false);
            _appTrayRow = Row(Loc.T("service.appAutoTray"), Loc.T("service.appAutoTray.desc"), _appTrayToggle);
            _appTrayRow.Visibility = Visibility.Collapsed;
            Body.Children.Add(_appTrayRow);

            Body.Children.Add(new Border { Height = 10 });

            _tgAutoToggle = new Toggle(Loc.T("service.tgAuto"));
            bool tgInstalled = Core.TgProxyInstalled();
            _tgAutoToggle.IsEnabled = tgInstalled;
            _tgAutoToggle.IsChecked = tgInstalled && Core.TgAutostartEnabled();
            _tgAutoToggle.Checked += (s, e) => Core.SetTgAutostart(true);
            _tgAutoToggle.Unchecked += (s, e) => Core.SetTgAutostart(false);
            string tgDesc = tgInstalled ? Loc.T("service.tgAuto.desc") : Loc.T("service.tgAuto.notInstalled");
            Body.Children.Add(Row(Loc.T("service.tgAuto"), tgDesc, _tgAutoToggle));
        }

        void BuildAppAutostart()
        {
            Body.Children.Add(SectionLabel(Loc.T("service.sec.appAutostart")));

            _bypassAppToggle = new Toggle(Loc.T("service.bypassAuto"));
            _bypassAppToggle.IsChecked = Core.GetBool("autostart_run", false);
            _bypassAppToggle.Checked += (s, e) => { Core.SetBool("autostart_run", true); Core.SaveConfig(); };
            _bypassAppToggle.Unchecked += (s, e) => { Core.SetBool("autostart_run", false); Core.SaveConfig(); };

            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(UI.T(Loc.T("service.bypassAuto"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            _bypassAppDesc = new TextBlock
            {
                Text = Loc.T("service.bypassAuto.desc"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            left.Children.Add(_bypassAppDesc);
            Grid.SetColumn(left, 0); g.Children.Add(left);
            var rc = new ContentControl { Content = _bypassAppToggle, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(rc, 1); g.Children.Add(rc);
            _bypassAppRow = UI.Card(g, new Thickness(16, 14, 16, 14));
            Body.Children.Add(_bypassAppRow);

            Body.Children.Add(new Border { Height = 10 });

            _tgAppToggle = new Toggle(Loc.T("service.tgAutoApp"));
            bool tgInstalled = Core.TgProxyInstalled();
            _tgAppToggle.IsEnabled = tgInstalled;
            _tgAppToggle.IsChecked = tgInstalled && Core.TgAutostartAppEnabled;
            _tgAppToggle.Checked += (s, e) => Core.TgAutostartAppEnabled = true;
            _tgAppToggle.Unchecked += (s, e) => Core.TgAutostartAppEnabled = false;
            string tgAppDesc = tgInstalled ? Loc.T("service.tgAutoApp.desc") : Loc.T("service.tgAuto.notInstalled");
            Body.Children.Add(Row(Loc.T("service.tgAutoApp"), tgAppDesc, _tgAppToggle));
        }

        void DoInstall()
        {
            var files = Core.GetStrategyFiles();
            int idx = _stratPick.SelectedIndex;
            if (idx < 0 || idx >= files.Count) { Core.Warn(Loc.T("service.noStrat")); return; }
            string file = files[idx];
            string name = Core.PrettyName(file);
            var r = MessageBox.Show(
                string.Format(Loc.T("service.installDlg"), name),
                Loc.T("service.installTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            Guarded(Loc.T("service.task.install"), delegate
            {
                if (!Core.InstallService(file)) throw new Exception("service installation failed");
                Core.Good(string.Format(Loc.T("service.installed"), name));
            });
        }

        void DoRemove()
        {
            var r = MessageBox.Show(
                Loc.T("service.removeDlg"),
                Loc.T("service.removeTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (r != MessageBoxResult.OK) return;
            Guarded(Loc.T("service.task.remove"), delegate
            {
                if (!Core.RemoveService()) throw new Exception("service removal failed");
                Core.Info(Loc.T("service.removed"));
            });
        }

        void Guarded(string what, Action act)
        {
            if (!Core.IsAdmin())
            {
                Core.Warn(string.Format(Loc.T("service.noAdmin.log"), what));
                var res = MessageBox.Show(string.Format(Loc.T("service.noAdmin.msg"), what) + "\n\n" + Loc.T("mw.restartAsAdminPrompt"),
                    Loc.T("service.noAdmin.title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    Core.RestartAsAdmin();
                }
                return;
            }
            // sc create/start/stop/delete отвечают до 20 с каждый (RemoveService —
            // это остановка плюс удаление), поэтому действие уходит в пул потоков:
            // раньше окно на это время полностью замирало. Журнал потокобезопасен,
            // а перерисовка страницы возвращается в UI-поток.
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { act(); }
                catch (Exception ex) { Core.Fail(what + ": " + ex.Message); }
                try { Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate { Refresh(); }); }
                catch { }
            });
        }

        void BuildAdminNote()
        {
            var card = NoteCard(Icons.Shield, Theme.BrAccent, Loc.T("service.note"), Sev.Info);
            card.Margin = new Thickness(0, 18, 0, 0);
            Body.Children.Add(card);
        }

        void Refresh()
        {
            string ss = Core.ServiceState();
            Sev sev = ss == "running" ? Sev.Ok : ss == "stopped" ? Sev.Warn : Sev.Neutral;
            string txt = ss == "running" ? Loc.T("service.state.running") : ss == "stopped" ? Loc.T("service.state.stopped") : Loc.T("service.state.absent");
            ReplacePill(ref _statePill, sev, txt);
            string strat = Core.ServiceStrategy();
            _stratVal.Text = string.IsNullOrEmpty(strat) ? Loc.T("service.strat.none") : Loc.T("service.strat.prefix") + strat;

            bool wf = Core.WinDivertFilePresent();
            bool wl = Core.WinDivertLoadedCached(); // кэш: sc query не запускается каждые 2 секунды
            if (!wf) ReplacePill(ref _wdPill, Sev.Err, Loc.T("service.driver.absent"));
            else if (wl) ReplacePill(ref _wdPill, Sev.Ok, Loc.T("service.driver.loaded"));
            else ReplacePill(ref _wdPill, Sev.Info, Loc.T("service.driver.ready"));

            // Согласованность с состоянием службы:
            if (_bypassAppDesc != null && _bypassAppToggle != null)
            {
                if (ss == "running")
                {
                    _bypassAppDesc.Text = Loc.T("service.bypassAuto.svcActive");
                    _bypassAppDesc.Foreground = Theme.BrOk;
                }
                else
                {
                    _bypassAppDesc.Text = Loc.T("service.bypassAuto.desc");
                    _bypassAppDesc.Foreground = Theme.BrMuted;
                }
            }

            // Доступность TG-Proxy тумблеров:
            bool tgInstalled = Core.TgProxyInstalled();
            if (_tgAutoToggle != null && _tgAutoToggle.IsEnabled != tgInstalled)
                _tgAutoToggle.IsEnabled = tgInstalled;
            if (_tgAppToggle != null && _tgAppToggle.IsEnabled != tgInstalled)
                _tgAppToggle.IsEnabled = tgInstalled;
        }

        void ReplacePill(ref Border pill, Sev sev, string text)
        {
            var parent = pill.Parent as Grid;
            if (parent == null) return;
            int col = Grid.GetColumn(pill);
            int idx = parent.Children.IndexOf(pill);
            var np = Pill.Make(sev, text);
            np.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(np, col);
            if (idx >= 0) { parent.Children.RemoveAt(idx); parent.Children.Insert(idx, np); pill = np; }
        }
    }
}
