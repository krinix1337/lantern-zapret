using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZapretStudio
{
    class OverviewPage : Page
    {
        public override string Title { get { return Loc.T("overview.title"); } }
        public override string Subtitle { get { return Loc.T("overview.sub"); } }

        readonly MainWindow _win;
        Border _statusCard, _tgCard;
        TextBlock _statusTitle, _statusSub, _stratName, _modeVal, _uptimeVal;
        TextBlock _tgStatusTitle, _tgStatusSub;
        Button _mainBtn, _restartBtn, _tgBtn, _tgFolderBtn;
        Border _cDiscord, _cYouTube, _cDivert, _cService;
        bool? _lastRunning;
        bool _zapBusy, _tgBusy;
        readonly DispatcherTimer _timer;
        TextBlock _trafficDown, _trafficUp, _trafficTotalDown, _trafficTotalUp;

        public OverviewPage(MainWindow win)
        {
            _win = win;
            BuildHero();
            BuildMeta();
            BuildTraffic();
            BuildCards();
            BuildQuickActions();
            BuildRiskNote();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (s, e) => Refresh();
        }

        public override void OnShow() { Refresh(); _timer.Start(); }
        public override void OnHide() { _timer.Stop(); }

        public void SetZapretTransition(bool busy)
        {
            _zapBusy = busy;
            if (busy)
            {
                _statusTitle.Text = Loc.T("ov.launching");
                _statusTitle.Foreground = Theme.BrAccent;
                _statusSub.Text = Loc.T("mw.startTask");
                if (_mainBtn != null) _mainBtn.IsEnabled = false;
                if (_restartBtn != null) _restartBtn.IsEnabled = false;
                PulseCard(_statusCard, true);
            }
            else
            {
                PulseCard(_statusCard, false);
                if (_mainBtn != null) _mainBtn.IsEnabled = true;
                if (_restartBtn != null) _restartBtn.IsEnabled = true;
                _lastRunning = null;
                Refresh();
            }
        }

        public void SetTgTransition(bool busy)
        {
            _tgBusy = busy;
            if (busy)
            {
                _tgStatusTitle.Text = Loc.T("ov.tgLaunching");
                _tgStatusTitle.Foreground = Theme.BrAccent;
                _tgStatusSub.Text = Loc.T("tg.dlProgress");
                if (_tgBtn != null) _tgBtn.IsEnabled = false;
                PulseCard(_tgCard, true);
            }
            else
            {
                PulseCard(_tgCard, false);
                if (_tgBtn != null) _tgBtn.IsEnabled = true;
                _lastTgRunning = null;
                _lastTgInstalled = null;
                RefreshTg();
            }
        }

        static void PulseCard(Border card, bool active)
        {
            if (card == null) return;
            var scale = card.RenderTransform as ScaleTransform;
            if (scale == null)
            {
                scale = new ScaleTransform(1, 1);
                card.RenderTransform = scale;
                card.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            if (!active)
            {
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = scale.ScaleY = 1;
                return;
            }
            var pulse = new System.Windows.Media.Animation.DoubleAnimation(1, 1.018, TimeSpan.FromMilliseconds(520))
            {
                AutoReverse = true,
                RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        }

        void BuildHero()
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var zap = BuildZapretCard(); zap.Margin = new Thickness(0, 0, 7, 0);
            Grid.SetColumn(zap, 0); g.Children.Add(zap);
            var tg = BuildTgCard(); tg.Margin = new Thickness(7, 0, 0, 0);
            Grid.SetColumn(tg, 1); g.Children.Add(tg);
            Body.Children.Add(g);
        }

        Border BuildZapretCard()
        {
            var g = new Grid { MinHeight = 185 };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            head.Children.Add(UI.Icon(Icons.Shield, 18, Theme.BrAccent, 1.8));
            var ht = UI.T(Loc.T("ov.zap.title"), Theme.FsSmall, Theme.BrFaint, FontWeights.SemiBold);
            ht.Margin = new Thickness(9, 0, 0, 0); ht.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(ht);
            Grid.SetRow(head, 0); g.Children.Add(head);

            _statusTitle = UI.T("—", Theme.FsDisplay, Theme.BrText, FontWeights.Bold);
            Grid.SetRow(_statusTitle, 1); g.Children.Add(_statusTitle);

            _statusSub = new TextBlock
            {
                Text = "",
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont,
                Margin = new Thickness(0, 6, 0, 16),
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 40,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(_statusSub, 2); g.Children.Add(_statusSub);

            var btnRow = new ActionRow();
            _mainBtn = Ctl.Button(Loc.T("common.start"), Icons.Play, 0);
            _mainBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            _mainBtn.Height = 40;
            _mainBtn.Click += (s, e) =>
            {
                if (!System.IO.File.Exists(Core.WinwsExe)) { ZapretDownload(); return; }
                _win.ToggleRun();
            };
            btnRow.Children.Add(_mainBtn);
            _restartBtn = Ctl.Button(Loc.T("ov.restart"), Icons.Restart, 3);
            _restartBtn.Height = 40;
            _restartBtn.Click += (s, e) => _win.RestartCurrent();
            btnRow.Children.Add(_restartBtn);
            Grid.SetRow(btnRow, 3); g.Children.Add(btnRow);

            _statusCard = UI.Card(g, new Thickness(22, 20, 22, 20));
            return _statusCard;
        }

        Border BuildTgCard()
        {
            var g = new Grid { MinHeight = 185 };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            head.Children.Add(UI.Icon(Icons.Telegram, 18, Theme.BrAccent, 1.8));
            var ht = UI.T(Loc.T("ov.sec.tg"), Theme.FsSmall, Theme.BrFaint, FontWeights.SemiBold);
            ht.Margin = new Thickness(9, 0, 0, 0); ht.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(ht);
            Grid.SetRow(head, 0); g.Children.Add(head);

            _tgStatusTitle = UI.T("—", Theme.FsDisplay, Theme.BrText, FontWeights.Bold);
            Grid.SetRow(_tgStatusTitle, 1); g.Children.Add(_tgStatusTitle);

            _tgStatusSub = new TextBlock
            {
                Text = Loc.T("ov.tg.desc"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont,
                Margin = new Thickness(0, 6, 0, 16),
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 40,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(_tgStatusSub, 2); g.Children.Add(_tgStatusSub);

            var btnRow = new ActionRow();
            _tgBtn = Ctl.Button(Loc.T("common.start"), Icons.Play, 0);
            _tgBtn.HorizontalAlignment = HorizontalAlignment.Stretch;
            _tgBtn.Height = 40;
            _tgBtn.Click += (s, e) => TgToggle();
            btnRow.Children.Add(_tgBtn);
            _tgFolderBtn = Ctl.Button(Loc.T("ov.qa.tgFolder"), Icons.Folder, 3);
            _tgFolderBtn.Height = 40;
            _tgFolderBtn.Click += (s, e) => { try { System.IO.Directory.CreateDirectory(Core.TgToolsDir); Core.OpenFolder(Core.TgToolsDir); } catch { } };
            btnRow.Children.Add(_tgFolderBtn);
            Grid.SetRow(btnRow, 3); g.Children.Add(btnRow);

            _tgCard = UI.Card(g, new Thickness(22, 20, 22, 20));
            return _tgCard;
        }

        void BuildMeta()
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            for (int i = 0; i < 3; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var c0 = MetaCard(Loc.T("ov.meta.strat"), out _stratName);
            var c1 = MetaCard(Loc.T("ov.meta.mode"), out _modeVal);
            var c2 = MetaCard(Loc.T("ov.meta.uptime"), out _uptimeVal);
            c0.Margin = new Thickness(0, 0, 6, 0);
            c1.Margin = new Thickness(6, 0, 6, 0);
            c2.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(c0, 0); Grid.SetColumn(c1, 1); Grid.SetColumn(c2, 2);
            g.Children.Add(c0); g.Children.Add(c1); g.Children.Add(c2);
            Body.Children.Add(g);
        }

        Border MetaCard(string label, out TextBlock val)
        {
            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = label.ToUpperInvariant(),
                Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold
            });
            val = UI.T("—", Theme.FsH2, Theme.BrText, FontWeights.SemiBold);
            val.Margin = new Thickness(0, 6, 0, 0);
            val.TextTrimming = TextTrimming.CharacterEllipsis;
            sp.Children.Add(val);
            return UI.Card(sp, new Thickness(16, 14, 16, 14));
        }

        void BuildTraffic()
        {
            Body.Children.Add(SectionLabel(Loc.T("ov.sec.traffic")));
            var g = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            for (int i = 0; i < 4; i++) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var c0 = MetaCard(Loc.T("ov.traffic.down"), out _trafficDown);
            var c1 = MetaCard(Loc.T("ov.traffic.up"), out _trafficUp);
            var c2 = MetaCard(Loc.T("ov.traffic.totalDown"), out _trafficTotalDown);
            var c3 = MetaCard(Loc.T("ov.traffic.totalUp"), out _trafficTotalUp);
            c0.Margin = new Thickness(0, 0, 6, 0);
            c1.Margin = new Thickness(6, 0, 6, 0);
            c2.Margin = new Thickness(6, 0, 6, 0);
            c3.Margin = new Thickness(6, 0, 0, 0);
            Grid.SetColumn(c0, 0); Grid.SetColumn(c1, 1); Grid.SetColumn(c2, 2); Grid.SetColumn(c3, 3);
            g.Children.Add(c0); g.Children.Add(c1); g.Children.Add(c2); g.Children.Add(c3);
            Body.Children.Add(g);
        }

        void BuildCards()
        {
            Body.Children.Add(SectionLabel(Loc.T("ov.sec.components")));
            // CellRow, а не Grid из четырёх Star-колонок: на минимальной ширине
            // окна карточка сужалась до ~158 px и плашка статуса («Готов к
            // запуску», «Не установлена») обрезалась. Теперь при нехватке места
            // строка переносится на два ряда по две карточки.
            var g = new CellRow { MinCell = 178, Gap = 12 };
            _cDiscord = MiniCard("Discord", Icons.Dot);
            _cYouTube = MiniCard("YouTube", Icons.Play);
            _cDivert = MiniCard("WinDivert", Icons.Shield);
            _cService = MiniCard(Loc.T("ov.card.service"), Icons.Server);
            g.Children.Add(_cDiscord); g.Children.Add(_cYouTube); g.Children.Add(_cDivert); g.Children.Add(_cService);
            Body.Children.Add(g);
        }

        Border MiniCard(string name, string icon)
        {
            var sp = new StackPanel();
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            top.Children.Add(UI.Icon(icon, 16, Theme.BrMuted, 1.8));
            var t = UI.T(name, Theme.FsSmall, Theme.BrMuted, FontWeights.SemiBold);
            t.Margin = new Thickness(8, 0, 0, 0); t.VerticalAlignment = VerticalAlignment.Center;
            top.Children.Add(t);
            sp.Children.Add(top);
            var pill = Pill.Make(Sev.Neutral, "—");
            pill.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.Add(pill);
            return UI.Card(sp, new Thickness(16, 14, 16, 16));
        }

        void SetMini(Border card, Sev sev, string text)
        {
            if (card == null) return;
            var sp = card.Child as StackPanel;
            if (sp == null || sp.Children.Count < 2) return;
            var currentPill = sp.Children[1] as Border;
            if (currentPill != null && Pill.GetText(currentPill) == text) return;

            var newPill = Pill.Make(sev, text);
            newPill.Margin = new Thickness(0, 12, 0, 0);
            sp.Children.RemoveAt(1);
            sp.Children.Insert(1, newPill);
        }

        void TgToggle()
        {
            if (!Core.TgProxyInstalled()) { TgDownload(); return; }
            if (Core.TgProxyRunning()) TgStop();
            else TgStart();
        }

        void TgStart()
        {
            if (!Core.TgProxyInstalled())
            {
                Core.Warn(Loc.T("ov.tg.notInstalled"));
                RefreshTg();
                return;
            }
            SetTgTransition(true);
            Dispatcher.BeginInvoke((Action)delegate
            {
                try
                {
                    string err;
                    if (Core.TgProxyStart(out err)) { Core.Good(Loc.T("tg.startedOk")); _win.ShowToast(Loc.T("tg.startedOk"), Sev.Ok); }
                    else Core.Fail(string.Format(Loc.T("tg.startErr"), err));
                }
                finally { SetTgTransition(false); }
            }, DispatcherPriority.Background);
        }

        void TgStop()
        {
            Core.TgProxyStop();
            Core.Info(Loc.T("tg.stoppedOk"));
            _win.ShowToast(Loc.T("tg.stoppedOk"), Sev.Warn);
            RefreshTg();
        }

        void TgDownload()
        {
            var r = MessageBox.Show(
                string.Format(Loc.T("tg.dlDlg"), Core.TgToolsDir),
                Loc.T("tg.dlTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
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
                        if (ok) Core.Good(Loc.T("tg.dlOk"));
                        else Core.Fail(Loc.T("tg.dlFail"));
                        RefreshTg();
                    });
                }
                catch { }
            });
        }

        bool? _lastTgRunning;
        bool? _lastTgInstalled;

        void RefreshTg()
        {
            if (_tgBtn == null) return;
            _tgBtn.IsEnabled = !_tgBusy;
            if (_tgBusy) return;
            bool installed = Core.TgProxyInstalled();
            bool running = Core.TgProxyRunning();

            if (_lastTgRunning.HasValue && _lastTgRunning.Value == running &&
                _lastTgInstalled.HasValue && _lastTgInstalled.Value == installed)
                return;

            _lastTgRunning = running;
            _lastTgInstalled = installed;

            if (running)
            {
                _tgStatusTitle.Text = Loc.T("ov.running");
                _tgStatusTitle.Foreground = Theme.BrOk;
                _tgStatusSub.Text = Loc.T("ov.tg.desc");
                Ctl.SetButton(_tgBtn, Loc.T("common.stop"), Icons.Stop, 2);
                _tgCard.BorderBrush = Theme.Alpha(Theme.Ok, 80);
                _tgCard.Background = Theme.Alpha(Theme.Ok, 12);
            }
            else if (installed)
            {
                _tgStatusTitle.Text = Loc.T("ov.stopped");
                _tgStatusTitle.Foreground = Theme.BrText;
                _tgStatusSub.Text = Loc.T("ov.tg.desc");
                Ctl.SetButton(_tgBtn, Loc.T("common.start"), Icons.Play, 0);
                _tgCard.BorderBrush = Theme.BrStroke;
                _tgCard.Background = Theme.BrSurface;
            }
            else
            {
                _tgStatusTitle.Text = Loc.T("tg.notInstalled");
                _tgStatusTitle.Foreground = Theme.BrWarn;
                _tgStatusSub.Text = Loc.T("ov.tg.notInstalled");
                Ctl.SetButton(_tgBtn, Loc.T("tg.download"), Icons.Download, 0);
                _tgCard.BorderBrush = Theme.BrStroke;
                _tgCard.Background = Theme.BrSurface;
            }
        }

        void BuildQuickActions()
        {
            Body.Children.Add(SectionLabel(Loc.T("ov.sec.quick")));
            var wrap = new WrapPanel();
            wrap.Children.Add(QA(Loc.T("ov.qa.check"), Icons.Pulse, delegate { _win.Navigate("check"); }));
            wrap.Children.Add(QA(Loc.T("ov.qa.strat"), Icons.Grid, delegate { _win.Navigate("strategies"); }));
            wrap.Children.Add(QA(Loc.T("ov.qa.folder"), Icons.Folder, delegate { Core.OpenFolder(Core.Root); }));
            wrap.Children.Add(QA(Loc.T("ov.qa.log"), Icons.List, delegate { _win.Navigate("log"); }));
            Body.Children.Add(wrap);
        }

        Button QA(string text, string icon, Action act)
        {
            var b = Ctl.Button(text, icon, 1);
            b.Margin = new Thickness(0, 0, 10, 10);
            b.Click += (s, e) => act();
            return b;
        }

        void BuildRiskNote()
        {
            var card = NoteCard(Icons.Warn, Theme.BrWarn, Loc.T("ov.risk"), Sev.Warn);
            card.Margin = new Thickness(0, 18, 0, 0);
            Body.Children.Add(card);
        }

        void PulseStatus()
        {
            if (_statusCard == null) return;
            var st = _statusCard.RenderTransform as ScaleTransform;
            if (st == null)
            {
                st = new ScaleTransform(1, 1);
                _statusCard.RenderTransformOrigin = new Point(0.5, 0.5);
                _statusCard.RenderTransform = st;
            }
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            var a = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.985,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new System.Windows.Media.Animation.BackEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut, Amplitude = 0.5 }
            };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
            var fade = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.55,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(320),
                EasingFunction = ease
            };
            _statusTitle.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        public void Refresh()
        {
            if (_mainBtn != null) _mainBtn.IsEnabled = !_zapBusy;
            if (_zapBusy) { RefreshTg(); return; }
            if (!System.IO.File.Exists(Core.WinwsExe))
            {
                _statusTitle.Text = Loc.T("ov.zap.notInstalledTitle");
                _statusTitle.Foreground = Theme.BrWarn;
                _statusSub.Text = Loc.T("ov.zap.notInstalled");
                Ctl.SetButton(_mainBtn, Loc.T("ov.zap.download"), Icons.Download, 0);
                _statusCard.BorderBrush = Theme.BrStroke;
                _statusCard.Background = Theme.BrSurface;
                if (_restartBtn != null) _restartBtn.IsEnabled = false;
                RefreshTg();
                return;
            }

            bool running = _win.IsActive2();
            bool changed = !_lastRunning.HasValue || _lastRunning.Value != running;
            _lastRunning = running;
            string mode = _win.CurrentMode();
            string strat = _win.CurrentStrategyName();

            if (changed)
            {
                if (running)
                {
                    _statusTitle.Text = Loc.T("ov.running");
                    _statusTitle.Foreground = Theme.BrOk;
                    _statusSub.Text = mode == Loc.T("mode.service") ? Loc.T("ov.sub.service") : Loc.T("ov.sub.manual");
                    Ctl.SetButton(_mainBtn, Loc.T("common.stop"), Icons.Stop, 2);
                    _statusCard.BorderBrush = Theme.Alpha(Theme.Ok, 80);
                    _statusCard.Background = Theme.Alpha(Theme.Ok, 12);
                    if (_restartBtn != null) _restartBtn.IsEnabled = true;
                    PulseStatus();
                }
                else
                {
                    _statusTitle.Text = Loc.T("ov.stopped");
                    _statusTitle.Foreground = Theme.BrText;
                    _statusSub.Text = Loc.T("ov.sub.off");
                    Ctl.SetButton(_mainBtn, Loc.T("common.start"), Icons.Play, 0);
                    _statusCard.BorderBrush = Theme.BrStroke;
                    _statusCard.Background = Theme.BrSurface;
                    if (_restartBtn != null) _restartBtn.IsEnabled = false;
                }
            }
            else if (running)
            {
                _statusSub.Text = mode == Loc.T("mode.service") ? Loc.T("ov.sub.service") : Loc.T("ov.sub.manual");
            }

            if (_stratName != null) _stratName.Text = string.IsNullOrEmpty(strat) ? Loc.T("ov.stratNone") : strat;
            _modeVal.Text = running ? mode : "—";
            _uptimeVal.Text = _win.UptimeText();

            SetMini(_cDiscord, running ? Sev.Ok : Sev.Neutral, running ? Loc.T("ov.comp.active") : Loc.T("ov.comp.off"));
            SetMini(_cYouTube, running ? Sev.Ok : Sev.Neutral, running ? Loc.T("ov.comp.active") : Loc.T("ov.comp.off"));
            bool wdFile = Core.WinDivertFilePresent();
            bool wdLoaded = Core.WinDivertLoadedCached(); // кэш: sc query не запускается каждую секунду
            if (!wdFile) SetMini(_cDivert, Sev.Err, Loc.T("ov.wd.absent"));
            else if (wdLoaded) SetMini(_cDivert, Sev.Ok, Loc.T("ov.wd.loaded"));
            else SetMini(_cDivert, Sev.Info, Loc.T("ov.wd.ready"));
            string ss = Core.ServiceState();
            if (ss == "running") SetMini(_cService, Sev.Ok, Loc.T("ov.svc.running"));
            else if (ss == "stopped") SetMini(_cService, Sev.Warn, Loc.T("ov.svc.stopped"));
            else SetMini(_cService, Sev.Neutral, Loc.T("ov.svc.absent"));

            // Трафик
            var tr = Core.GetTraffic();
            _trafficDown.Text = Core.HumanSpeed(tr.SpeedRecv);
            _trafficUp.Text = Core.HumanSpeed(tr.SpeedSent);
            _trafficTotalDown.Text = Core.HumanSize(tr.TotalRecv);
            _trafficTotalUp.Text = Core.HumanSize(tr.TotalSent);

            RefreshTg();
        }

        void ZapretDownload()
        {
            var dl = new DownloadWindow();
            dl.ShowDialog();
            if (dl.Succeeded)
            {
                Core.LoadConfig();
                _win.ShowToast(string.Format(Loc.T("dl.installed"), Core.Root), Sev.Ok);
            }
            Refresh();
        }

        public void StartTimer() { _timer.Start(); }
        public void StopTimer() { _timer.Stop(); }
    }
}
