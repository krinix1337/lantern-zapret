using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ZapretStudio
{
    class UpdatesPage : Page
    {
        public override string Title { get { return Loc.T("updates.title"); } }
        public override string Subtitle { get { return Loc.T("updates.sub"); } }

        readonly MainWindow _win;

        // Zapret
        TextBlock _zapLocalVer, _zapLatestVer, _zapStatusLine;
        Button _zapUpdateBtn;
        UpdateProgressBar _zapProgress;
        string _zapLatest;

        // TG Proxy
        ContentControl _tgPillWrap;
        TextBlock _tgLocalVer, _tgLatestVer, _tgStatusLine;
        Button _tgUpdateBtn;
        UpdateProgressBar _tgProgress;
        string _tgLatest;

        // Lantern App
        TextBlock _appLocalVer, _appLatestVer, _appStatusLine;
        Button _appUpdateBtn;
        UpdateProgressBar _appProgress;
        string _appLatest;

        // Header check button
        Button _checkBtn;
        RotateTransform _checkSpinTransform;

        public UpdatesPage(MainWindow win)
        {
            _win = win;
            BuildHeaderAction();
            BuildCards();
        }

        void BuildHeaderAction()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var ic = UI.Icon(Icons.Refresh, 16, Theme.BrOnAccent, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 8, 0);

            _checkSpinTransform = new RotateTransform(0);
            ic.RenderTransformOrigin = new Point(0.5, 0.5);
            ic.RenderTransform = _checkSpinTransform;

            sp.Children.Add(ic);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("updates.check"),
                Foreground = Theme.BrOnAccent,
                FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });

            var border = new Border
            {
                Background = Theme.BrAccent,
                BorderBrush = Theme.BrAccent,
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R10,
                Padding = new Thickness(14, 9, 14, 9),
                Child = sp
            };

            _checkBtn = new Button
            {
                Content = border,
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true
            };
            Ctl.StripChrome(_checkBtn);

            _checkBtn.Click += (s, e) =>
            {
                StartSpinAnimation();
                _win.CheckUpdates();
            };

            HeaderActionSlot.Content = _checkBtn;
        }

        void StartSpinAnimation()
        {
            if (_checkSpinTransform == null) return;
            if (Theme.AnimationsEnabled)
            {
                var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(800));
                spin.RepeatBehavior = RepeatBehavior.Forever;
                _checkSpinTransform.BeginAnimation(RotateTransform.AngleProperty, spin);
            }
        }

        internal void StopSpinAnimation()
        {
            if (_checkSpinTransform != null)
                _checkSpinTransform.BeginAnimation(RotateTransform.AngleProperty, null);
        }

        void BuildCards()
        {
            Body.Children.Add(BuildZapretCard());
            Body.Children.Add(space());
            Body.Children.Add(BuildTgProxyCard());
            Body.Children.Add(space());
            Body.Children.Add(BuildAppCard());
        }

        static UIElement space() { return new Border { Height = 12 }; }

        Border BuildZapretCard()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(UI.T(Loc.T("updates.zapret.title"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));

            var verRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            _zapLocalVer = new TextBlock
            {
                Text = Loc.T("settings.localVersion") + SettingsPage.NormVer(Core.ZapretVersion()),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 0, 16, 0)
            };
            verRow.Children.Add(_zapLocalVer);

            _zapLatestVer = new TextBlock
            {
                Text = Loc.T("updates.latestVersion") + (string.IsNullOrEmpty(_zapLatest) ? "—" : _zapLatest),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont
            };
            verRow.Children.Add(_zapLatestVer);
            left.Children.Add(verRow);

            _zapStatusLine = new TextBlock
            {
                Text = Loc.T("settings.checkingOnStart"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            left.Children.Add(_zapStatusLine);

            _zapProgress = new UpdateProgressBar();
            left.Children.Add(_zapProgress.View);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            _zapUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _zapUpdateBtn.Visibility = Visibility.Collapsed;
            _zapUpdateBtn.Click += (s, e) => _win.UpdateZapret(_zapLatest);

            var right = new ContentControl { Content = _zapUpdateBtn, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            return UI.Card(grid, new Thickness(16, 14, 16, 14));
        }

        Border BuildTgProxyCard()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleRow.Children.Add(UI.T(Loc.T("updates.tg.title"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));

            bool installed = Core.TgProxyInstalled();
            _tgPillWrap = new ContentControl { Margin = new Thickness(10, 0, 0, 0) };
            _tgPillWrap.Content = Pill.Make(installed ? Sev.Ok : Sev.Warn, installed ? Loc.T("updates.installed") : Loc.T("settings.tg.notInstalled"));
            titleRow.Children.Add(_tgPillWrap);
            left.Children.Add(titleRow);

            var verRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            string tgLocal = installed ? Core.TgProxyLocalVersion() : null;
            _tgLocalVer = new TextBlock
            {
                Text = Loc.T("settings.localVersion") + (string.IsNullOrEmpty(tgLocal) ? "—" : SettingsPage.NormVer(tgLocal)),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 0, 16, 0)
            };
            verRow.Children.Add(_tgLocalVer);

            _tgLatestVer = new TextBlock
            {
                Text = Loc.T("updates.latestVersion") + (string.IsNullOrEmpty(_tgLatest) ? "—" : _tgLatest),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont
            };
            verRow.Children.Add(_tgLatestVer);
            left.Children.Add(verRow);

            _tgStatusLine = new TextBlock
            {
                Text = Loc.T("settings.checkingOnStart"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            left.Children.Add(_tgStatusLine);

            _tgProgress = new UpdateProgressBar();
            left.Children.Add(_tgProgress.View);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            _tgUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            _tgUpdateBtn.Click += (s, e) => UpdateTgProxy();

            var right = new ContentControl { Content = _tgUpdateBtn, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            return UI.Card(grid, new Thickness(16, 14, 16, 14));
        }

        Border BuildAppCard()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(UI.T(Loc.T("updates.app.title"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));

            var verRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            _appLocalVer = new TextBlock
            {
                Text = Loc.T("settings.localVersion") + SettingsPage.NormVer(Core.AppVersion),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                Margin = new Thickness(0, 0, 16, 0)
            };
            verRow.Children.Add(_appLocalVer);

            _appLatestVer = new TextBlock
            {
                Text = Loc.T("updates.latestVersion") + (string.IsNullOrEmpty(_appLatest) ? "—" : _appLatest),
                Foreground = Theme.BrMuted, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont
            };
            verRow.Children.Add(_appLatestVer);
            left.Children.Add(verRow);

            _appStatusLine = new TextBlock
            {
                Text = Loc.T("settings.checkingOnStart"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            left.Children.Add(_appStatusLine);

            _appProgress = new UpdateProgressBar();
            left.Children.Add(_appProgress.View);

            Grid.SetColumn(left, 0);
            grid.Children.Add(left);

            _appUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            _appUpdateBtn.Click += (s, e) => DoAppUpdate();

            var right = new ContentControl { Content = _appUpdateBtn, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            return UI.Card(grid, new Thickness(16, 14, 16, 14));
        }

        public void SetAutomaticUpdateResults(string zapretLatest, string zapretLocal,
            string tgLatest, string tgLocal, string appLatest, string appLocal)
        {
            StopSpinAnimation();
            string zLat = SettingsPage.NormVer(zapretLatest);
            string zLoc = SettingsPage.NormVer(zapretLocal);
            string tgLat = SettingsPage.NormVer(tgLatest);
            string tgLoc = SettingsPage.NormVer(tgLocal);
            string apLat = SettingsPage.NormVer(appLatest);
            string apLoc = SettingsPage.NormVer(appLocal);

            _zapLatest = zLat;
            if (!string.IsNullOrEmpty(tgLat)) _tgLatest = tgLat;
            _appLatest = apLat;

            // Zapret
            if (_zapLocalVer != null)
                _zapLocalVer.Text = Loc.T("settings.localVersion") + zLoc;
            if (_zapLatestVer != null)
                _zapLatestVer.Text = Loc.T("updates.latestVersion") + (string.IsNullOrEmpty(zLat) ? "—" : zLat);
            SetCardStatus(_zapStatusLine, _zapUpdateBtn, zLat, zLoc);

            // TG proxy
            bool tgInst = Core.TgProxyInstalled();
            if (_tgPillWrap != null)
                _tgPillWrap.Content = Pill.Make(tgInst ? Sev.Ok : Sev.Warn, tgInst ? Loc.T("updates.installed") : Loc.T("settings.tg.notInstalled"));
            if (_tgLocalVer != null)
                _tgLocalVer.Text = Loc.T("settings.localVersion") + (tgInst && !string.IsNullOrEmpty(tgLoc) ? tgLoc : "—");
            if (_tgLatestVer != null)
                _tgLatestVer.Text = Loc.T("updates.latestVersion") + (string.IsNullOrEmpty(tgLat) ? "—" : tgLat);
            SetCardStatus(_tgStatusLine, _tgUpdateBtn, tgLat, tgInst ? tgLoc : null);

            // App
            if (_appLocalVer != null)
                _appLocalVer.Text = Loc.T("settings.localVersion") + apLoc;
            if (_appLatestVer != null)
                _appLatestVer.Text = Loc.T("updates.latestVersion") + (string.IsNullOrEmpty(apLat) ? "—" : apLat);
            SetCardStatus(_appStatusLine, _appUpdateBtn, apLat, apLoc);
        }

        public void SetCheckingUpdates()
        {
            StartSpinAnimation();
            string t = Loc.T("settings.checkingUpd");
            ShowLine(_zapStatusLine, t, Theme.BrMuted);
            ShowLine(_tgStatusLine, t, Theme.BrMuted);
            ShowLine(_appStatusLine, t, Theme.BrMuted);
        }

        static void ShowLine(TextBlock line, string text, Brush fg)
        {
            if (line == null) return;
            line.Text = text;
            line.Foreground = fg;
            line.Visibility = Visibility.Visible;
        }

        static void HideLine(TextBlock line)
        {
            if (line == null) return;
            line.Text = "";
            line.Visibility = Visibility.Collapsed;
        }

        void SetCardStatus(TextBlock line, Button updateButton, string latest, string local)
        {
            if (line == null || updateButton == null) return;
            var existingProgress = ProgressFor(updateButton);
            if (existingProgress != null) existingProgress.Hide();
            updateButton.Visibility = Visibility.Collapsed;

            if (string.IsNullOrEmpty(latest))
            {
                ShowLine(line, Loc.T("mw.verFail"), Theme.BrWarn);
                return;
            }
            if (string.IsNullOrEmpty(local))
            {
                HideLine(line);
                return;
            }
            if (SettingsPage.CompareVersions(latest, local) > 0)
            {
                ShowLine(line, string.Format(Loc.T("settings.updateFull"), latest), Theme.BrWarn);
                updateButton.Visibility = Visibility.Visible;
                return;
            }
            if (SettingsPage.CompareVersions(latest, local) == 0)
            {
                ShowLine(line, Loc.T("updates.upToDate"), Theme.BrOk);
                return;
            }
            ShowLine(line, Loc.T("updates.localNewer"), Theme.BrMuted);
        }

        UpdateProgressBar ProgressFor(Button button)
        {
            if (button == _zapUpdateBtn) return _zapProgress;
            if (button == _tgUpdateBtn) return _tgProgress;
            return _appProgress;
        }

        void ShowProgress(UpdateProgressBar progress, TextBlock line, string phase, int percent, Brush color)
        {
            ShowLine(line, percent >= 0 ? phase + " — " + percent + "%" : phase, color);
            if (progress != null) progress.Show(phase, percent, color);
        }

        public void SetZapretUpdateProgress(string phase, int percent)
        {
            ShowProgress(_zapProgress, _zapStatusLine, phase, percent, Theme.BrAccent);
        }

        public void FinishZapretUpdate(string text, bool ok)
        {
            FinishProgress(_zapProgress, _zapStatusLine, text, ok);
            if (ok && _zapLocalVer != null)
            {
                _zapLocalVer.Text = Loc.T("settings.localVersion") + Core.ZapretVersion();
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
            ShowLine(line, text, ok ? Theme.BrOk : Theme.BrWarn);
            if (progress != null)
            {
                progress.Show(text, 100, ok ? Theme.BrOk : Theme.BrWarn);
                if (ok)
                {
                    var tm = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
                    tm.Tick += (s, e) => { tm.Stop(); progress.Hide(); };
                    tm.Start();
                }
            }
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
                    try { Directory.CreateDirectory(Core.TgToolsDir); } catch { }
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
                            string tag = !string.IsNullOrEmpty(_tgLatest) ? _tgLatest : Core.Get("latest_tg", "");
                            Core.TgProxyMarkInstalled(tag);
                            string nv = Core.TgProxyLocalVersion();
                            string nvNorm = string.IsNullOrEmpty(nv) ? "—" : SettingsPage.NormVer(nv);
                            if (_tgLocalVer != null)
                                _tgLocalVer.Text = Loc.T("settings.localVersion") + nvNorm;
                            if (_tgPillWrap != null)
                                _tgPillWrap.Content = Pill.Make(Sev.Ok, Loc.Lang == "ru" ? "Установлен" : "Installed");
                            if (_win != null) _win.NoteTgProxyUpdated(nv);
                            FinishProgress(_tgProgress, _tgStatusLine,
                                Loc.T("settings.update.done") + ": " + nvNorm, true);
                            _win.ShowToast(Loc.T("tg.dlOk"), Sev.Ok);
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
                    string url, sha256Url;
                    Core.AppUpdateAssets(out url, out sha256Url);
                    string err;
                    bool ok = Core.SelfUpdate(url, sha256Url, delegate (DlProgress p)
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
                                Loc.T("settings.app.dlFail") + (err != null ? ": " + err : ""), false);
                        }
                    });
                }
                catch { }
            });
        }

#if SELFTEST
        internal void ShowDemoProgress()
        {
            ShowProgress(_zapProgress, _zapStatusLine, Loc.T("settings.update.downloading"), 0, Theme.BrAccent);
            ShowProgress(_tgProgress, _tgStatusLine, Loc.T("settings.update.downloading"), -1, Theme.BrAccent);
            ShowProgress(_appProgress, _appStatusLine, Loc.T("settings.update.replacing"), 63, Theme.BrAccent);
            _zapProgress.SnapForTest(); _tgProgress.SnapForTest(); _appProgress.SnapForTest();
        }

        internal void CheckDemoProgress()
        {
            _zapProgress.SnapForTest(); _tgProgress.SnapForTest(); _appProgress.SnapForTest();
            double track = _appProgress.TrackWidth;
            if (!(_zapProgress.Visible && _tgProgress.Visible && _appProgress.Visible))
                throw new Exception("progress bars hidden");
            if (track < 120) throw new Exception("progress track too narrow: " + track);
            double zero = _zapProgress.FillWidth;
            if (zero < 6 || zero > 24) throw new Exception("zero fill width " + zero);
            if (Math.Abs(_tgProgress.FillWidth - _tgProgress.TrackWidth) > 1.5)
                throw new Exception("indeterminate fill " + _tgProgress.FillWidth + " of " + _tgProgress.TrackWidth);
            double mid = _appProgress.FillWidth / track;
            if (Math.Abs(mid - 0.63) > 0.02) throw new Exception("mid fill " + mid);
        }

        internal void HideDemoProgress()
        {
            _zapProgress.Hide(); _tgProgress.Hide(); _appProgress.Hide();
        }

        internal void ScrollProgressIntoView()
        {
            var fe = _appProgress.View as FrameworkElement;
            if (fe != null) fe.BringIntoView();
        }

        internal void CheckUpdateNotices()
        {
            SetAutomaticUpdateResults("1.2", "1.2", "1.2", "1.2", "1.2", "1.2");
            if (_zapUpdateBtn.Visibility != Visibility.Collapsed ||
                _tgUpdateBtn.Visibility != Visibility.Collapsed ||
                _appUpdateBtn.Visibility != Visibility.Collapsed)
                throw new Exception("up-to-date cards show the update button");

            SetAutomaticUpdateResults("1.3", "1.2", "1.3", "1.2", "1.3", "1.2");
            if (_zapUpdateBtn.Visibility != Visibility.Visible ||
                _tgUpdateBtn.Visibility != Visibility.Visible ||
                _appUpdateBtn.Visibility != Visibility.Visible)
                throw new Exception("available update without the update button");

            SetAutomaticUpdateResults("1.2", "1.2", "1.2", null, "1.2", "1.2");
            if (_tgUpdateBtn.Visibility != Visibility.Collapsed)
                throw new Exception("missing proxy shows update button");

            SetAutomaticUpdateResults(null, "1.2", null, "1.2", null, "1.2");
            if (_zapUpdateBtn.Visibility != Visibility.Collapsed)
                throw new Exception("failed check shows update button");

            SetAutomaticUpdateResults("1.2", "1.2", "1.2", "1.2", "1.2", "1.2");
        }
#endif
    }

    sealed class UpdateProgressBar
    {
        const double BarH = 8;
        readonly Border _root;
        readonly Border _fill;
        readonly ProgressTrack _track;
        bool _pulsing;
        double _target;

        public UIElement View { get { return _root; } }

        public UpdateProgressBar()
        {
            _fill = new Border
            {
                CornerRadius = new CornerRadius((BarH - 2) / 2),
                Background = Theme.BrAccent
            };
            _track = new ProgressTrack { Height = BarH - 2 };
            _track.Children.Add(_fill);
            var groove = new Border
            {
                Height = BarH,
                CornerRadius = new CornerRadius(BarH / 2),
                Background = Theme.BrSurfaceHi,
                BorderBrush = Theme.BrStrokeSoft,
                BorderThickness = new Thickness(1),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = _track
            };

            _root = new Border
            {
                Child = groove,
                Margin = new Thickness(0, 9, 0, 2),
                Visibility = Visibility.Collapsed,
                Opacity = 0
            };
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

            _fill.Background = Sheen(color);
            if (percent < 0)
            {
                SetFraction(1);
                StartPulse();
            }
            else
            {
                StopPulse();
                SetFraction(Math.Max(0, Math.Min(100, percent)) / 100.0);
            }
        }

        public void Hide()
        {
            if (_root.Visibility == Visibility.Collapsed) return;
            StopPulse();
            if (Theme.AnimationsEnabled)
            {
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(180));
                fadeOut.Completed += (s, e) =>
                {
                    SetFraction(0, true);
                    _root.Visibility = Visibility.Collapsed;
                };
                _root.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            else
            {
                SetFraction(0, true);
                _root.Opacity = 0;
                _root.Visibility = Visibility.Collapsed;
            }
        }

        void SetFraction(double f) { SetFraction(f, false); }

        void SetFraction(double f, bool instant)
        {
            _target = f;
            if (!instant && Theme.AnimationsEnabled)
            {
                var a = new DoubleAnimation(f, TimeSpan.FromMilliseconds(180))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                _track.BeginAnimation(ProgressTrack.FractionProperty, a);
            }
            else
            {
                _track.BeginAnimation(ProgressTrack.FractionProperty, null);
                _track.Fraction = f;
            }
        }

        void StartPulse()
        {
            if (_pulsing || !Theme.AnimationsEnabled) return;
            _pulsing = true;
            var a = new DoubleAnimation(0.45, 1.0, TimeSpan.FromMilliseconds(750))
            { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
            _fill.BeginAnimation(UIElement.OpacityProperty, a);
        }

        void StopPulse()
        {
            if (!_pulsing) return;
            _pulsing = false;
            _fill.BeginAnimation(UIElement.OpacityProperty, null);
            _fill.Opacity = 1;
        }

        static Brush Sheen(Brush flat)
        {
            var scb = flat as SolidColorBrush;
            if (scb == null) return flat;
            var c = scb.Color;
            var lg = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            lg.GradientStops.Add(new GradientStop(Dim(c, 0.78), 0));
            lg.GradientStops.Add(new GradientStop(c, 1));
            lg.Freeze();
            return lg;
        }

        static Color Dim(Color c, double k)
        {
            return Color.FromArgb(c.A, (byte)(c.R * k), (byte)(c.G * k), (byte)(c.B * k));
        }

#if SELFTEST
        internal void SnapForTest() { SetFraction(_target, true); }
        internal double FillWidth { get { return _fill.ActualWidth; } }
        internal double TrackWidth { get { return _track.ActualWidth; } }
        internal bool Visible { get { return _root.Visibility == Visibility.Visible; } }
#endif
    }
}
