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
        ContentControl _zapStatusPill;
        Button _zapUpdateBtn;
        Border _zapUpToDatePill;
        UpdateProgressBar _zapProgress;
        string _zapLatest;

        // TG Proxy
        ContentControl _tgPillWrap;
        TextBlock _tgLocalVer, _tgLatestVer, _tgStatusLine;
        Button _tgUpdateBtn;
        Border _tgUpToDatePill;
        UpdateProgressBar _tgProgress;
        string _tgLatest;

        // Lantern App
        TextBlock _appLocalVer, _appLatestVer, _appStatusLine;
        ContentControl _appStatusPill;
        Button _appUpdateBtn;
        Border _appUpToDatePill;
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
            InitInitialStatus();
        }

        bool _hasInitialData;
        public override void OnShow()
        {
            if (!_hasInitialData)
            {
                InitInitialStatus();
                _hasInitialData = true;
            }
            else
            {
                string zLoc = SettingsPage.NormVer(Core.ZapretVersion());
                if (_zapLocalVer != null) _zapLocalVer.Text = Loc.T("updates.installedTag") + zLoc;
                bool tgInst = Core.TgProxyInstalled();
                string tgLoc = tgInst ? SettingsPage.NormVer(Core.TgProxyLocalVersion()) : null;
                if (_tgLocalVer != null) _tgLocalVer.Text = Loc.T("updates.installedTag") + (tgInst && !string.IsNullOrEmpty(tgLoc) ? tgLoc : "—");
            }
        }

        void BuildHeaderAction()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var ic = UI.Icon(Icons.Refresh, 14, Theme.BrAccent, 1.8);
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 7, 0);

            _checkSpinTransform = new RotateTransform(0);
            ic.RenderTransformOrigin = new Point(0.5, 0.5);
            ic.RenderTransform = _checkSpinTransform;

            sp.Children.Add(ic);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("updates.check"),
                Foreground = Theme.BrText,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            });

            var border = new Border
            {
                Background = Theme.BrSurfaceAlt,
                BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R8,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                Child = sp
            };

            _checkBtn = new Button
            {
                Content = border,
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true
            };
            Ctl.StripChrome(_checkBtn);
            Ctl.AddMotion(_checkBtn);
            Ctl.AutomationSetName(_checkBtn, Loc.T("updates.check"));

            _checkBtn.MouseEnter += (s, e) =>
            {
                border.Background = Theme.BrSurfaceHi;
                border.BorderBrush = Theme.BrAccent;
            };
            _checkBtn.MouseLeave += (s, e) =>
            {
                border.Background = Theme.BrSurfaceAlt;
                border.BorderBrush = Theme.BrStroke;
            };

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
            Body.Children.Add(new Border { Height = 12 });
            Body.Children.Add(BuildTgProxyCard());
            Body.Children.Add(new Border { Height = 12 });
            Body.Children.Add(BuildAppCard());
        }

        static Border VersionBadge(TextBlock textBlock)
        {
            textBlock.VerticalAlignment = VerticalAlignment.Center;
            textBlock.Margin = new Thickness(0, 1, 0, 0);
            return new Border
            {
                Background = Theme.BrSurfaceAlt,
                BorderBrush = Theme.BrStrokeSoft,
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R6,
                Height = 26,
                Padding = new Thickness(9, 0, 9, 0),
                Margin = new Thickness(0, 0, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
                Child = textBlock
            };
        }

        static Border MakeUpToDateIndicator()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var ic = UI.Icon(Icons.Check, 13, Theme.BrOk, 2.0);
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 6, 0);
            sp.Children.Add(ic);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("updates.upToDate"),
                Foreground = Theme.BrOk,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            });

            return new Border
            {
                Background = Theme.Alpha(Theme.Ok, 14),
                BorderBrush = Theme.Alpha(Theme.Ok, 55),
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R8,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = sp,
                Visibility = Visibility.Collapsed
            };
        }

        Border BuildComponentCard(
            string iconData,
            string titleKey,
            string descKey,
            TextBlock localVerTb,
            TextBlock latestVerTb,
            ContentControl statusPillWrap,
            TextBlock statusLine,
            UpdateProgressBar progress,
            Button updateBtn,
            Border upToDateIndicator)
        {
            var cardPanel = new StackPanel();

            // --- Row 1: Header (Icon + Title/Desc on Left, Action on Right) ---
            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            leftGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBox = new Border
            {
                Width = 40, Height = 40,
                CornerRadius = Theme.R10,
                Background = Theme.Alpha(Theme.AccentMain, 16),
                BorderBrush = Theme.Alpha(Theme.AccentMain, 50),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Child = UI.Icon(iconData, 18, Theme.BrAccent, 1.8)
            };
            Grid.SetColumn(iconBox, 0);
            leftGrid.Children.Add(iconBox);

            var infoBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            infoBox.Children.Add(UI.T(Loc.T(titleKey), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            var descTb = new TextBlock
            {
                Text = Loc.T(descKey),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            infoBox.Children.Add(descTb);
            Grid.SetColumn(infoBox, 1);
            leftGrid.Children.Add(infoBox);

            Grid.SetColumn(leftGrid, 0);
            topGrid.Children.Add(leftGrid);

            var rightBox = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            if (upToDateIndicator != null) rightBox.Children.Add(upToDateIndicator);
            if (updateBtn != null) rightBox.Children.Add(updateBtn);
            Grid.SetColumn(rightBox, 1);
            topGrid.Children.Add(rightBox);

            cardPanel.Children.Add(topGrid);

            // --- Row 2: Badges row (Installed version, Latest version, Status pill) ---
            var badgesRow = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 0)
            };

            badgesRow.Children.Add(VersionBadge(localVerTb));
            badgesRow.Children.Add(VersionBadge(latestVerTb));
            if (statusPillWrap != null)
            {
                statusPillWrap.VerticalAlignment = VerticalAlignment.Center;
                statusPillWrap.Margin = new Thickness(0, 0, 8, 4);
                badgesRow.Children.Add(statusPillWrap);
            }

            cardPanel.Children.Add(badgesRow);

            // --- Row 3: Progress bar and status line ---
            if (progress != null)
                cardPanel.Children.Add(progress.View);

            cardPanel.Children.Add(statusLine);

            return UI.Card(cardPanel, new Thickness(18, 16, 18, 16));
        }

        Border BuildZapretCard()
        {
            _zapLocalVer = new TextBlock { FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, Foreground = Theme.BrText, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _zapLatestVer = new TextBlock { FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, Foreground = Theme.BrMuted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _zapStatusLine = new TextBlock
            {
                Text = "", Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed
            };
            _zapProgress = new UpdateProgressBar();
            _zapStatusPill = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
            _zapUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _zapUpdateBtn.Visibility = Visibility.Collapsed;
            var zapBd = _zapUpdateBtn.Content as Border;
            if (zapBd != null) { zapBd.Height = 32; zapBd.CornerRadius = Theme.R8; zapBd.Padding = new Thickness(14, 0, 14, 0); }
            _zapUpdateBtn.Click += (s, e) => _win.UpdateZapret(_zapLatest);
            _zapUpToDatePill = MakeUpToDateIndicator();

            return BuildComponentCard(
                Icons.Bolt,
                "updates.zapret.title",
                "updates.zapret.desc",
                _zapLocalVer,
                _zapLatestVer,
                _zapStatusPill,
                _zapStatusLine,
                _zapProgress,
                _zapUpdateBtn,
                _zapUpToDatePill);
        }

        Border BuildTgProxyCard()
        {
            _tgLocalVer = new TextBlock { FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, Foreground = Theme.BrText, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _tgLatestVer = new TextBlock { FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, Foreground = Theme.BrMuted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _tgStatusLine = new TextBlock
            {
                Text = "", Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed
            };
            _tgProgress = new UpdateProgressBar();
            _tgPillWrap = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
            _tgUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            var tgBd = _tgUpdateBtn.Content as Border;
            if (tgBd != null) { tgBd.Height = 32; tgBd.CornerRadius = Theme.R8; tgBd.Padding = new Thickness(14, 0, 14, 0); }
            _tgUpdateBtn.Click += (s, e) => UpdateTgProxy();
            _tgUpToDatePill = MakeUpToDateIndicator();

            return BuildComponentCard(
                Icons.Telegram,
                "updates.tg.title",
                "updates.tg.desc",
                _tgLocalVer,
                _tgLatestVer,
                _tgPillWrap,
                _tgStatusLine,
                _tgProgress,
                _tgUpdateBtn,
                _tgUpToDatePill);
        }

        Border BuildAppCard()
        {
            _appLocalVer = new TextBlock { FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, Foreground = Theme.BrText, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _appLatestVer = new TextBlock { FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont, Foreground = Theme.BrMuted, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _appStatusLine = new TextBlock
            {
                Text = "", Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed
            };
            _appProgress = new UpdateProgressBar();
            _appStatusPill = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
            _appUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            var appBd = _appUpdateBtn.Content as Border;
            if (appBd != null) { appBd.Height = 32; appBd.CornerRadius = Theme.R8; appBd.Padding = new Thickness(14, 0, 14, 0); }
            _appUpdateBtn.Click += (s, e) => DoAppUpdate();
            _appUpToDatePill = MakeUpToDateIndicator();

            return BuildComponentCard(
                Icons.Lantern,
                "updates.app.title",
                "updates.app.desc",
                _appLocalVer,
                _appLatestVer,
                _appStatusPill,
                _appStatusLine,
                _appProgress,
                _appUpdateBtn,
                _appUpToDatePill);
        }

        void InitInitialStatus()
        {
            string zLat = SettingsPage.NormVer(Core.Get("latest_zapret", null));
            string zLoc = SettingsPage.NormVer(Core.ZapretVersion());
            _zapLatest = zLat;
            if (_zapLocalVer != null) _zapLocalVer.Text = Loc.T("updates.installedTag") + zLoc;
            if (_zapLatestVer != null) _zapLatestVer.Text = Loc.T("updates.latestTag") + (string.IsNullOrEmpty(zLat) ? "—" : zLat);
            SetCardStatus(_zapStatusLine, _zapStatusPill, _zapUpdateBtn, zLat, zLoc, isInitial: true);

            bool tgInst = Core.TgProxyInstalled();
            string tgLocal = tgInst ? Core.TgProxyLocalVersion() : null;
            string tgLat = SettingsPage.NormVer(Core.Get("latest_tg", null));
            string tgLoc = tgInst && !string.IsNullOrEmpty(tgLocal) ? SettingsPage.NormVer(tgLocal) : null;
            _tgLatest = tgLat;
            if (_tgLocalVer != null) _tgLocalVer.Text = Loc.T("updates.installedTag") + (tgInst && !string.IsNullOrEmpty(tgLoc) ? tgLoc : "—");
            if (_tgLatestVer != null) _tgLatestVer.Text = Loc.T("updates.latestTag") + (string.IsNullOrEmpty(tgLat) ? "—" : tgLat);
            SetCardStatus(_tgStatusLine, _tgPillWrap, _tgUpdateBtn, tgLat, tgLoc, isInitial: true);

            string apLat = SettingsPage.NormVer(Core.Get("latest_app", null));
            string apLoc = SettingsPage.NormVer(Core.AppVersion);
            _appLatest = apLat;
            if (_appLocalVer != null) _appLocalVer.Text = Loc.T("updates.installedTag") + apLoc;
            if (_appLatestVer != null) _appLatestVer.Text = Loc.T("updates.latestTag") + (string.IsNullOrEmpty(apLat) ? "—" : apLat);
            SetCardStatus(_appStatusLine, _appStatusPill, _appUpdateBtn, apLat, apLoc, isInitial: true);
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
                _zapLocalVer.Text = Loc.T("updates.installedTag") + zLoc;
            if (_zapLatestVer != null)
                _zapLatestVer.Text = Loc.T("updates.latestTag") + (string.IsNullOrEmpty(zLat) ? "—" : zLat);
            SetCardStatus(_zapStatusLine, _zapStatusPill, _zapUpdateBtn, zLat, zLoc);

            // TG proxy
            bool tgInst = !string.IsNullOrEmpty(tgLoc) || Core.TgProxyInstalled();
            if (_tgLocalVer != null)
                _tgLocalVer.Text = Loc.T("updates.installedTag") + (tgInst && !string.IsNullOrEmpty(tgLoc) ? tgLoc : "—");
            if (_tgLatestVer != null)
                _tgLatestVer.Text = Loc.T("updates.latestTag") + (string.IsNullOrEmpty(tgLat) ? "—" : tgLat);
            SetCardStatus(_tgStatusLine, _tgPillWrap, _tgUpdateBtn, tgLat, !string.IsNullOrEmpty(tgLoc) ? tgLoc : (tgInst ? Core.TgProxyLocalVersion() : null));

            // App
            if (_appLocalVer != null)
                _appLocalVer.Text = Loc.T("updates.installedTag") + apLoc;
            if (_appLatestVer != null)
                _appLatestVer.Text = Loc.T("updates.latestTag") + (string.IsNullOrEmpty(apLat) ? "—" : apLat);
            SetCardStatus(_appStatusLine, _appStatusPill, _appUpdateBtn, apLat, apLoc);
        }

        public void SetCheckingUpdates()
        {
            StartSpinAnimation();
            if (_zapStatusPill != null) _zapStatusPill.Content = Pill.Make(Sev.Progress, Loc.T("updates.checking"));
            if (_tgPillWrap != null) _tgPillWrap.Content = Pill.Make(Sev.Progress, Loc.T("updates.checking"));
            if (_appStatusPill != null) _appStatusPill.Content = Pill.Make(Sev.Progress, Loc.T("updates.checking"));
            if (_zapUpToDatePill != null) _zapUpToDatePill.Visibility = Visibility.Collapsed;
            if (_tgUpToDatePill != null) _tgUpToDatePill.Visibility = Visibility.Collapsed;
            if (_appUpToDatePill != null) _appUpToDatePill.Visibility = Visibility.Collapsed;
            if (_zapUpdateBtn != null) _zapUpdateBtn.Visibility = Visibility.Collapsed;
            if (_tgUpdateBtn != null) _tgUpdateBtn.Visibility = Visibility.Collapsed;
            if (_appUpdateBtn != null) _appUpdateBtn.Visibility = Visibility.Collapsed;
            HideLine(_zapStatusLine);
            HideLine(_tgStatusLine);
            HideLine(_appStatusLine);
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

        void SetCardStatus(TextBlock line, ContentControl statusPillWrap, Button updateButton, string latest, string local, bool isInitial = false)
        {
            if (updateButton == null) return;
            var existingProgress = ProgressFor(updateButton);
            if (existingProgress != null) existingProgress.Hide();
            updateButton.Visibility = Visibility.Collapsed;
            var upInd = UpToDateIndicatorFor(updateButton);
            if (upInd != null) upInd.Visibility = Visibility.Collapsed;
            HideLine(line);

            if (string.IsNullOrEmpty(latest))
            {
                if (statusPillWrap != null)
                {
                    if (isInitial)
                    {
                        if (statusPillWrap == _tgPillWrap && !Core.TgProxyInstalled())
                            statusPillWrap.Content = Pill.Make(Sev.Warn, Loc.T("settings.tg.notInstalled"));
                        else
                            statusPillWrap.Content = null;
                    }
                    else
                    {
                        statusPillWrap.Content = Pill.Make(Sev.Err, Loc.T("mw.verFail"));
                    }
                }
                return;
            }
            if (string.IsNullOrEmpty(local))
            {
                if (statusPillWrap != null)
                    statusPillWrap.Content = Pill.Make(Sev.Warn, Loc.T("settings.tg.notInstalled"));
                return;
            }
            if (SettingsPage.CompareVersions(latest, local) > 0)
            {
                if (statusPillWrap != null)
                    statusPillWrap.Content = Pill.Make(Sev.Warn, Loc.T("updates.available") + ": " + latest);
                updateButton.Visibility = Visibility.Visible;
                return;
            }
            if (SettingsPage.CompareVersions(latest, local) == 0)
            {
                if (statusPillWrap != null)
                    statusPillWrap.Content = null;
                if (upInd != null) upInd.Visibility = Visibility.Visible;
                return;
            }
            if (statusPillWrap != null)
                statusPillWrap.Content = Pill.Make(Sev.Neutral, Loc.T("updates.localNewer"));
            if (upInd != null) upInd.Visibility = Visibility.Visible;
        }

        Border UpToDateIndicatorFor(Button button)
        {
            if (button == _zapUpdateBtn) return _zapUpToDatePill;
            if (button == _tgUpdateBtn) return _tgUpToDatePill;
            return _appUpToDatePill;
        }

        UpdateProgressBar ProgressFor(Button button)
        {
            if (button == _zapUpdateBtn) return _zapProgress;
            if (button == _tgUpdateBtn) return _tgProgress;
            return _appProgress;
        }

        void ShowProgress(UpdateProgressBar progress, TextBlock line, string phase, int percent, Brush color, long bytesRead = -1, long totalBytes = -1, double speedBps = 0)
        {
            string text;
            if (bytesRead >= 0 && totalBytes > 0)
            {
                string sizePart = Core.HumanSize(bytesRead) + " / " + Core.HumanSize(totalBytes);
                string speedPart = speedBps > 0 ? " (" + Core.HumanSpeed(speedBps) + ")" : "";
                text = phase + " — " + sizePart + speedPart + (percent >= 0 ? " · " + percent + "%" : "");
            }
            else if (bytesRead >= 0)
            {
                string sizePart = Core.HumanSize(bytesRead);
                string speedPart = speedBps > 0 ? " (" + Core.HumanSpeed(speedBps) + ")" : "";
                text = phase + " — " + sizePart + speedPart + (percent >= 0 ? " · " + percent + "%" : "");
            }
            else
            {
                text = percent >= 0 ? phase + " — " + percent + "%" : phase;
            }
            ShowLine(line, text, color);
            if (progress != null) progress.Show(phase, percent, color);
        }

        public void SetZapretUpdateProgress(string phase, int percent, long bytesRead = -1, long totalBytes = -1, double speedBps = 0)
        {
            ShowProgress(_zapProgress, _zapStatusLine, phase, percent, Theme.BrAccent, bytesRead, totalBytes, speedBps);
        }

        public void FinishZapretUpdate(string text, bool ok)
        {
            FinishProgress(_zapProgress, _zapStatusLine, text, ok);
            if (ok && _zapLocalVer != null)
            {
                _zapLocalVer.Text = Loc.T("settings.localVersion") + Core.ZapretVersion();
            }
        }

        void SetTgUpdateProgress(string phase, int percent, long bytesRead = -1, long totalBytes = -1, double speedBps = 0)
        {
            ShowProgress(_tgProgress, _tgStatusLine, phase, percent, Theme.BrAccent, bytesRead, totalBytes, speedBps);
        }

        void SetAppUpdateProgress(string phase, int percent, long bytesRead = -1, long totalBytes = -1, double speedBps = 0)
        {
            ShowProgress(_appProgress, _appStatusLine, phase, percent, Theme.BrAccent, bytesRead, totalBytes, speedBps);
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
                                SetTgUpdateProgress(Loc.T("settings.update.downloading"), pct, p.BytesRead, p.Total, p.SpeedBps);
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
                                SetAppUpdateProgress(Loc.T("settings.update.downloading"), pct, p.BytesRead, p.Total, p.SpeedBps);
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
