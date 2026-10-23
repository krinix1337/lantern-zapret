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
        Border _zapLocalNewerPill;
        UpdateProgressBar _zapProgress;
        string _zapLatest;

        // TG Proxy
        ContentControl _tgPillWrap;
        TextBlock _tgLocalVer, _tgLatestVer, _tgStatusLine;
        Button _tgUpdateBtn;
        Border _tgUpToDatePill;
        Border _tgLocalNewerPill;
        UpdateProgressBar _tgProgress;
        string _tgLatest;

        // Lantern App
        TextBlock _appLocalVer, _appLatestVer, _appStatusLine;
        ContentControl _appStatusPill;
        Button _appUpdateBtn;
        Border _appUpToDatePill;
        Border _appLocalNewerPill;
        UpdateProgressBar _appProgress;
        string _appLatest;

        // Header check button
        Button _checkBtn, _verifyFilesBtn;
        RotateTransform _checkSpinTransform;
        readonly System.Collections.Generic.List<string> _zapIntegrity = new System.Collections.Generic.List<string>();
        readonly System.Collections.Generic.List<string> _tgIntegrity = new System.Collections.Generic.List<string>();
        bool _integrityBusy, _zapIntegrityShown, _tgIntegrityShown;

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
            RunIntegrityCheck(false);
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
                Content = Ctl.SplitSurface(border),
                Cursor = System.Windows.Input.Cursors.Hand,
                Focusable = true
            };
            Ctl.StripChrome(_checkBtn);
            Ctl.AddMotion(_checkBtn);
            Ctl.AttachGlow(_checkBtn, border);
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

            _verifyFilesBtn = Ctl.Button(Loc.T("updates.integrity.check"), Icons.Check, 1);
            var vb = Ctl.Surface(_verifyFilesBtn);
            var vbox = Ctl.ContentHost(_verifyFilesBtn);
            if (vb != null) { vb.Height = 32; vb.CornerRadius = Theme.R8; if (vbox != null) vbox.Padding = new Thickness(12, 0, 12, 0); }
            _verifyFilesBtn.ToolTip = Loc.T("updates.integrity.check");
            _verifyFilesBtn.Click += (s, e) => RunIntegrityCheck(true);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            actions.Children.Add(_verifyFilesBtn);
            actions.Children.Add(new Border { Width = 8 });
            actions.Children.Add(_checkBtn);
            HeaderActionSlot.Content = actions;
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
            var ic = UI.Icon(Icons.Check, 13, Theme.BrAccent, 2.0);
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 6, 0);
            sp.Children.Add(ic);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("updates.upToDate"),
                Foreground = Theme.BrAccent,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            });

            return new Border
            {
                Background = Theme.Alpha(Theme.AccentMain, 14),
                BorderBrush = Theme.Alpha(Theme.AccentMain, 55),
                BorderThickness = new Thickness(1),
                CornerRadius = Theme.R8,
                Height = 32,
                Padding = new Thickness(12, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = sp,
                Visibility = Visibility.Collapsed
            };
        }

        // Индикатор «локальная версия новее» — тот же слот справа в шапке
        // карточки, где у равных версий горит зелёное «Актуально». Без него
        // карточка компонента, собранного локально новее опубликованного
        // релиза, оставалась справа пустой: только кнопка истории релизов.
        static Border MakeLocalNewerIndicator()
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 7, Height = 7, Fill = Theme.Frozen(Theme.TextMuted),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 7, 0)
            };
            sp.Children.Add(dot);
            sp.Children.Add(new TextBlock
            {
                Text = Loc.T("updates.localNewer"),
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            });

            return new Border
            {
                Background = Theme.BrSurfaceAlt,
                BorderBrush = Theme.BrStroke,
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
            Border upToDateIndicator,
            Border localNewerIndicator,
            string releasesApi,
            string releasesPage)
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

            // Панель истории — внутри карточки, раскрывается под её содержимым.
            // Pop-up для этого не годился: он выглядит как отдельное окно, лежит
            // поверх других блоков и не следует за прокруткой страницы.
            StackPanel historyBody = null;
            Border historyBox = null;
            if (!string.IsNullOrEmpty(releasesApi))
            {
                historyBody = new StackPanel();
                historyBox = new Border
                {
                    Visibility = Visibility.Collapsed,
                    Margin = new Thickness(0, 12, 0, 0),
                    Background = Theme.BrSurfaceAlt,
                    BorderBrush = Theme.BrStroke,
                    BorderThickness = new Thickness(1),
                    CornerRadius = Theme.R10,
                    Padding = new Thickness(14, 12, 14, 12),
                    Child = historyBody
                };
                _histBoxes.Add(historyBox);
                _histBodies.Add(historyBody);
            }

            var rightBox = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            // «Что обновлялось» рядом с кнопкой обновления: разворачивает список
            // последних релизов этого компонента ниже по карточке.
            if (historyBox != null)
                rightBox.Children.Add(ReleaseHistoryButton(historyBox, historyBody, releasesApi, releasesPage));
            if (upToDateIndicator != null) rightBox.Children.Add(upToDateIndicator);
            if (localNewerIndicator != null) rightBox.Children.Add(localNewerIndicator);
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
                statusPillWrap.Focusable = false;
                statusPillWrap.VerticalAlignment = VerticalAlignment.Center;
                statusPillWrap.Margin = new Thickness(0, 0, 8, 4);
                badgesRow.Children.Add(statusPillWrap);
            }

            cardPanel.Children.Add(badgesRow);

            // --- Row 3: Progress bar and status line ---
            if (progress != null)
                cardPanel.Children.Add(progress.View);

            cardPanel.Children.Add(statusLine);
            if (historyBox != null) cardPanel.Children.Add(historyBox);

            return UI.Card(cardPanel, new Thickness(18, 16, 18, 16));
        }

        // ---------- История релизов компонента ----------
        // Обновления мало установить — нужно ещё и объяснить, что менялось. Кнопка
        // у карточки раскрывает последние релизы внутри самой карточки, ниже её
        // содержимого: pop-up для этого не подошёл — он выглядит отдельным окном,
        // лежит поверх других блоков и не следует за прокруткой страницы.
        // Описание не резуется: markdown вычищается до читаемого текста, а длинное
        // тело просто растягивает карточку.
        // Панели истории по карточкам — нужны тестовому хуку ShowDemoHistory, чтобы
        // аудит разметки и скриншот видели раскрытую панель, а не свёрнутую.
        readonly System.Collections.Generic.List<Border> _histBoxes = new System.Collections.Generic.List<Border>();
        readonly System.Collections.Generic.List<StackPanel> _histBodies = new System.Collections.Generic.List<StackPanel>();

        Button ReleaseHistoryButton(Border box, StackPanel body, string releasesApi, string releasesPage)
        {
            var b = Ctl.Button(null, Icons.List, 1);
            // Та же высота, что у плашки «Актуально» и кнопки «Обновить» (32 px):
            // иначе в шапке карточки стоят элементы разного размера.
            var bb = Ctl.Surface(b);
            var bbox = Ctl.ContentHost(b);
            if (bb != null) { bb.Height = 32; bb.CornerRadius = Theme.R8; if (bbox != null) bbox.Padding = new Thickness(9, 0, 9, 0); }
            string tip = Loc.T("updates.history.tip");
            b.ToolTip = tip;
            Ctl.AutomationSetName(b, tip);
            b.Margin = new Thickness(0, 0, 8, 0);
            b.Click += (s, e) => ToggleReleaseHistory(box, body, releasesApi, releasesPage);
            return b;
        }

        // Закрывает та же кнопка «Что обновлялось»: второй крестик внутри панели
        // дублировал действие и выглядел как лишняя кнопка в шапке блока.
        void ToggleReleaseHistory(Border box, StackPanel body, string releasesApi, string releasesPage)
        {
            if (box == null || body == null) return;
            if (box.Visibility == Visibility.Visible) { SetHistoryOpen(box, false); return; }
            SetHistoryOpen(box, true);
            if (body.Children.Count > 0) return;      // второй раз — без повторной сети
            LoadReleaseHistory(box, body, releasesApi, releasesPage);
        }

        void SetHistoryOpen(Border box, bool open)
        {
            if (box == null) return;
            if (open)
            {
                box.Visibility = Visibility.Visible;
                box.BeginAnimation(OpacityProperty, null);
                box.Opacity = 1;
                if (!Theme.AnimationsEnabled) return;
                box.RenderTransformOrigin = new Point(0.5, 0);
                var st = box.RenderTransform as ScaleTransform;
                if (st == null) { st = new ScaleTransform(1, 1); box.RenderTransform = st; }
                var grow = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                box.BeginAnimation(OpacityProperty, fade);
                return;
            }

            if (box.Visibility != Visibility.Visible) return;
            if (!Theme.AnimationsEnabled) { box.Visibility = Visibility.Collapsed; return; }
            var hide = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(130))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            hide.Completed += (s, e) =>
            {
                // Разворачивание всегда начинается с полной непрозрачности.
                box.BeginAnimation(OpacityProperty, null);
                box.Opacity = 1;
                box.Visibility = Visibility.Collapsed;
            };
            box.BeginAnimation(OpacityProperty, hide);
        }

        void LoadReleaseHistory(Border box, StackPanel body, string releasesApi, string releasesPage)
        {
            body.Children.Add(HistoryTitle(box));
            var loading = UI.T(Loc.T("updates.history.loading"), Theme.FsSmall, Theme.BrFaint);
            body.Children.Add(loading);

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var releases = Core.FetchReleaseList(releasesApi);
                try
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate
                    {
                        body.Children.Remove(loading);
                        FillHistoryBody(body, releases, releasesPage);
                    });
                }
                catch (Exception ex) { Core.Warn("LoadReleaseHistory: " + Core.Short(ex.Message)); }
            });
        }

        // releases == null — это «GitHub не ответил», а пустой список — «релизов
        // нет»; показывать одно и то же за другое нельзя.
        void FillHistoryBody(StackPanel body, System.Collections.Generic.List<Endpoints.ReleaseInfo> releases, string releasesPage)
        {
            if (releases == null) body.Children.Add(HistoryNote(Loc.T("updates.history.unavailable")));
            else if (releases.Count == 0) body.Children.Add(HistoryNote(Loc.T("updates.history.empty")));
            else foreach (var r in releases) body.Children.Add(HistoryRow(r));
            body.Children.Add(HistoryFooter(releasesPage));
        }

#if SELFTEST
        // Тестовый хук: раскрывает историю релизов на фиктивных данных с живым
        // markdown (жирный, код, ссылки, бейдж-картинка, цитата, заголовки), чтобы
        // аудит проверял панель, а скриншот — что это читается.
        public bool ShowDemoHistory()
        {
            if (_histBoxes.Count == 0 || _histBodies.Count == 0) return false;
            var rel = new System.Collections.Generic.List<Endpoints.ReleaseInfo>();
            rel.Add(new Endpoints.ReleaseInfo
            {
                Tag = "1.9.9",
                Date = "2026-03-05T12:00:00Z",
                Notes = "## Что нового\n\n- **Добавлен** режим `alt3` для [Discord](https://discord.com)\n" +
                        "- Исправлен ~~вылет~~ остановка при завершении\n\n> Требуются права администратора\n\n" +
                        "![shield](https://img.example/badge.svg)\n\n* мелкие правки"
            });
            rel.Add(new Endpoints.ReleaseInfo { Tag = "1.9.8", Date = "2026-02-01T12:00:00Z", Notes = "- fix A\n- fix B" });
            var box = _histBoxes[0];
            var body = _histBodies[0];
            body.Children.Clear();
            body.Children.Add(HistoryTitle(box));
            FillHistoryBody(body, rel, Endpoints.ZapretReleasesPage);
            box.Visibility = Visibility.Visible;
            return true;
        }

        public void HideDemoHistory()
        {
            foreach (var b in _histBoxes) if (b != null) b.Visibility = Visibility.Collapsed;
        }
#endif

        FrameworkElement HistoryTitle(Border box)
        {
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var title = UI.T(Loc.T("updates.history"), Theme.FsSmall, Theme.BrText, FontWeights.SemiBold);
            title.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(title, 0);
            head.Children.Add(title);

            return new Border
            {
                Child = head,
                BorderBrush = Theme.BrStrokeSoft,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
        }

        TextBlock HistoryNote(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap
            };
        }

        FrameworkElement HistoryRow(Endpoints.ReleaseInfo r)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };

            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(UI.T(r.Tag, Theme.FsBody, Theme.BrAccent, FontWeights.SemiBold));
            string date = Endpoints.ReleaseDate(r.Date);
            if (!string.IsNullOrEmpty(date))
            {
                var dateTb = UI.T(date, Theme.FsTiny, Theme.BrFaint);
                dateTb.Margin = new Thickness(8, 0, 0, 0);
                dateTb.VerticalAlignment = VerticalAlignment.Center;
                head.Children.Add(dateTb);
            }
            sp.Children.Add(head);

            string notes = string.IsNullOrEmpty(r.Notes) ? null
                : Endpoints.FormatReleaseNotes(Endpoints.RepairUtf8Mojibake(r.Notes));
            sp.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(notes) ? Loc.T("updates.history.noNotes") : notes,
                Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
            return sp;
        }

        FrameworkElement HistoryFooter(string releasesPage)
        {
            var b = Ctl.Button(Loc.T("updates.history.all"), Icons.External, 1);
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Click += (s, e) => Core.OpenUrl(releasesPage);
            return b;
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
            _zapStatusPill = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Focusable = false };
            _zapUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _zapUpdateBtn.Visibility = Visibility.Collapsed;
            var zapBd = Ctl.Surface(_zapUpdateBtn);
            var zapBox = Ctl.ContentHost(_zapUpdateBtn);
            if (zapBd != null) { zapBd.Height = 32; zapBd.CornerRadius = Theme.R8; if (zapBox != null) zapBox.Padding = new Thickness(14, 0, 14, 0); }
            _zapUpdateBtn.Click += (s, e) => _win.UpdateZapret(_zapLatest, _zapIntegrity.Count > 0);
            _zapUpToDatePill = MakeUpToDateIndicator();
            _zapLocalNewerPill = MakeLocalNewerIndicator();

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
                _zapUpToDatePill,
                _zapLocalNewerPill,
                Endpoints.ZapretReleasesApi,
                Endpoints.ZapretReleasesPage);
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
            _tgPillWrap = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Focusable = false };
            _tgUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _tgUpdateBtn.Visibility = Visibility.Collapsed;
            var tgBd = Ctl.Surface(_tgUpdateBtn);
            var tgBox = Ctl.ContentHost(_tgUpdateBtn);
            if (tgBd != null) { tgBd.Height = 32; tgBd.CornerRadius = Theme.R8; if (tgBox != null) tgBox.Padding = new Thickness(14, 0, 14, 0); }
            _tgUpdateBtn.Click += (s, e) => UpdateTgProxy();
            _tgUpToDatePill = MakeUpToDateIndicator();
            _tgLocalNewerPill = MakeLocalNewerIndicator();

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
                _tgUpToDatePill,
                _tgLocalNewerPill,
                Endpoints.TgProxyReleasesApi,
                Endpoints.TgProxyReleasesAll);
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
            _appStatusPill = new ContentControl { VerticalAlignment = VerticalAlignment.Center, Focusable = false };
            _appUpdateBtn = Ctl.Button(Loc.T("settings.updateNow"), Icons.Download, 0);
            _appUpdateBtn.Visibility = Visibility.Collapsed;
            var appBd = Ctl.Surface(_appUpdateBtn);
            var appBox = Ctl.ContentHost(_appUpdateBtn);
            if (appBd != null) { appBd.Height = 32; appBd.CornerRadius = Theme.R8; if (appBox != null) appBox.Padding = new Thickness(14, 0, 14, 0); }
            _appUpdateBtn.Click += (s, e) => DoAppUpdate();
            _appUpToDatePill = MakeUpToDateIndicator();
            _appLocalNewerPill = MakeLocalNewerIndicator();

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
                _appUpToDatePill,
                _appLocalNewerPill,
                Endpoints.AppReleasesApi,
                Endpoints.AppReleasesPage);
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
            RunIntegrityCheck(false);
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
            if (_zapLocalNewerPill != null) _zapLocalNewerPill.Visibility = Visibility.Collapsed;
            if (_tgLocalNewerPill != null) _tgLocalNewerPill.Visibility = Visibility.Collapsed;
            if (_appLocalNewerPill != null) _appLocalNewerPill.Visibility = Visibility.Collapsed;
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
            var lnInd = LocalNewerIndicatorFor(updateButton);
            if (lnInd != null) lnInd.Visibility = Visibility.Collapsed;
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
                            // Вместо пустой пилюли показываем честное «статус неизвестен».
                            // Пустой Content выглядел как «всё актуально» до первой
                            // проверки обновлений.
                            statusPillWrap.Content = Pill.Make(Sev.Neutral, Loc.T("updates.status.unknown"));
                    }
                    else
                    {
                        // Лимит GitHub API — не поломка компонента. Показываем
                        // короткую плашку, а причину — строкой под ней.
                        statusPillWrap.Content = Pill.Make(Sev.Warn, Loc.T("updates.checkUnavailable"));
                        string why = string.IsNullOrEmpty(Core.LastApiError)
                            ? Loc.T("mw.verFail") : Core.LastApiError;
                        ShowLine(line, why, Theme.BrWarn);
                    }
                }
                return;
            }
            if (string.IsNullOrEmpty(local))
            {
                if (statusPillWrap != null)
                {
                    string notInstKey = (updateButton == _tgUpdateBtn) ? "settings.tg.notInstalled" :
                                        (updateButton == _zapUpdateBtn) ? "updates.notInstalled.engine" : "updates.notInstalled.app";
                    statusPillWrap.Content = Pill.Make(Sev.Warn, Loc.T(notInstKey));
                }
                Ctl.SetButton(updateButton, Loc.T("updates.downloadNow"), Icons.Download, 0);
                updateButton.Visibility = Visibility.Visible;
                return;
            }
            if (SettingsPage.CompareVersions(latest, local) > 0)
            {
                if (statusPillWrap != null)
                    statusPillWrap.Content = Pill.Make(Sev.Warn, Loc.T("updates.available") + ": " + latest);
                Ctl.SetButton(updateButton, Loc.T("settings.updateNow"), Icons.Download, 0);
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
            if (upInd != null) upInd.Visibility = Visibility.Collapsed;
            // Локальная сборка новее опубликованного релиза: статус виден в том же
            // слоте справа, где у равных версий горит «Актуально».
            if (lnInd != null) lnInd.Visibility = Visibility.Visible;
        }

        void RunIntegrityCheck(bool notify)
        {
            if (_integrityBusy) return;
            _integrityBusy = true;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var z = Core.EngineIntegrityIssues();
                var t = Core.TgProxyIntegrityIssues();
                try
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        _integrityBusy = false;
                        _zapIntegrity.Clear();
                        if (z != null) _zapIntegrity.AddRange(z);
                        _tgIntegrity.Clear();
                        if (t != null) _tgIntegrity.AddRange(t);
                        ApplyIntegrityStatus();
                        foreach (string issue in _zapIntegrity) Core.Warn("Integrity zapret: " + issue);
                        foreach (string issue in _tgIntegrity) Core.Warn("Integrity TG: " + issue);
                        int n = _zapIntegrity.Count + _tgIntegrity.Count;
                        if (notify)
                        {
                            if (n == 0)
                            {
                                Core.Good(Loc.T("updates.integrity.ok"));
                                _win.ShowToast(Loc.T("updates.integrity.ok"), Sev.Ok);
                            }
                            else
                            {
                                string msg = string.Format(Loc.T("updates.integrity.found"), n);
                                Core.Warn(msg);
                                _win.ShowToast(msg, Sev.Warn);
                            }
                        }
                        else if (n > 0)
                        {
                            Core.Warn(string.Format(Loc.T("updates.integrity.found"), n));
                        }
                    });
                }
                catch { }
            });
        }

        void ApplyIntegrityStatus()
        {
            bool zapBad = _zapIntegrity.Count > 0;
            if (zapBad)
            {
                if (_zapStatusPill != null)
                {
                    var p = Pill.Make(Sev.Err, Loc.T("updates.integrity.damaged"));
                    p.ToolTip = string.Join("\n", _zapIntegrity.ToArray());
                    _zapStatusPill.Content = p;
                }
                if (_zapUpToDatePill != null) _zapUpToDatePill.Visibility = Visibility.Collapsed;
                if (_zapLocalNewerPill != null) _zapLocalNewerPill.Visibility = Visibility.Collapsed;
                if (_zapUpdateBtn != null)
                {
                    Ctl.SetButton(_zapUpdateBtn, Loc.T("updates.integrity.repair"), Icons.Download, 0);
                    _zapUpdateBtn.Visibility = Visibility.Visible;
                }
                _zapIntegrityShown = true;
            }
            else if (_zapIntegrityShown)
            {
                _zapIntegrityShown = false;
                SetCardStatus(_zapStatusLine, _zapStatusPill, _zapUpdateBtn, _zapLatest, SettingsPage.NormVer(Core.ZapretVersion()));
            }

            bool tgBad = _tgIntegrity.Count > 0;
            if (tgBad)
            {
                if (_tgPillWrap != null)
                {
                    var p = Pill.Make(Sev.Err, Loc.T("updates.integrity.damaged"));
                    p.ToolTip = string.Join("\n", _tgIntegrity.ToArray());
                    _tgPillWrap.Content = p;
                }
                if (_tgUpToDatePill != null) _tgUpToDatePill.Visibility = Visibility.Collapsed;
                if (_tgLocalNewerPill != null) _tgLocalNewerPill.Visibility = Visibility.Collapsed;
                if (_tgUpdateBtn != null)
                {
                    Ctl.SetButton(_tgUpdateBtn, Loc.T("updates.integrity.repair"), Icons.Download, 0);
                    _tgUpdateBtn.Visibility = Visibility.Visible;
                }
                _tgIntegrityShown = true;
            }
            else if (_tgIntegrityShown)
            {
                _tgIntegrityShown = false;
                bool tgInst = Core.TgProxyInstalled();
                string tgLoc = tgInst ? SettingsPage.NormVer(Core.TgProxyLocalVersion()) : null;
                SetCardStatus(_tgStatusLine, _tgPillWrap, _tgUpdateBtn, _tgLatest, tgLoc);
            }
        }

        Border UpToDateIndicatorFor(Button button)
        {
            if (button == _zapUpdateBtn) return _zapUpToDatePill;
            if (button == _tgUpdateBtn) return _tgUpToDatePill;
            return _appUpToDatePill;
        }

        Border LocalNewerIndicatorFor(Button button)
        {
            if (button == _zapUpdateBtn) return _zapLocalNewerPill;
            if (button == _tgUpdateBtn) return _tgLocalNewerPill;
            return _appLocalNewerPill;
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
                _zapLocalVer.Text = Loc.T("updates.installedTag") + SettingsPage.NormVer(Core.ZapretVersion());
                // Дать полосе успеха скрыться, затем перепроверить файлы.
                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
                t.Tick += (s, e) => { t.Stop(); RunIntegrityCheck(false); };
                t.Start();
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
                bool ok = false;
                string tgErr = null;
                try
                {
                    if (wasRunning) Core.TgProxyStop();
                    ok = Core.TgProxyInstall(delegate (DlProgress p)
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
                    }, out tgErr);
                }
                catch (Exception ex)
                {
                    tgErr = Core.Short(ex.Message);
                    Core.Fail("UpdateTgProxy: " + tgErr);
                }
                try
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (ok)
                        {
                            SetTgUpdateProgress(Loc.T("settings.update.replacing"), 96);
                            string nv = Core.TgProxyLocalVersion();
                            string nvNorm = string.IsNullOrEmpty(nv) ? "—" : SettingsPage.NormVer(nv);
                            if (_tgLocalVer != null)
                                _tgLocalVer.Text = Loc.T("updates.installedTag") + nvNorm;
                            if (_tgPillWrap != null)
                                _tgPillWrap.Content = Pill.Make(Theme.AccentMain, Loc.T("updates.installed"));
                            if (_win != null) _win.NoteTgProxyUpdated(nv);
                            FinishProgress(_tgProgress, _tgStatusLine,
                                Loc.T("settings.update.done") + ": " + nvNorm, true);
                            _win.ShowToast(Loc.T("tg.dlOk"), Sev.Ok);
                            if (wasRunning) { string tgStartErr; Core.TgProxyStart(out tgStartErr); }
                        }
                        else
                        {
                            // Кнопку возвращаем: без неё повторить попытку можно
                            // было только повторной проверкой обновлений.
                            _tgUpdateBtn.Visibility = Visibility.Visible;
                            FinishProgress(_tgProgress, _tgStatusLine,
                                Loc.T("tg.dlFail") + (string.IsNullOrEmpty(tgErr) ? "" : ": " + tgErr), false);
                            _win.ShowToast(Loc.T("tg.dlFail"), Sev.Warn);
                            if (!string.IsNullOrEmpty(tgErr)) Core.Fail(tgErr);
                        }
                        if (_win != null) _win.RefreshTop();
                        var it = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
                        it.Tick += (s2, e2) => { it.Stop(); RunIntegrityCheck(false); };
                        it.Start();
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

            // «Локальная версия неизвестна» → карточка предлагает скачать.
            // Проверяем на движке: у TG-карточки на этот случай живой фолбэк
            // Core.TgProxyInstalled()/TgProxyLocalVersion() (View.Updates.cs:454),
            // из-за которого ожидание зависело от того, стоит ли прокси на машине
            // запуска, — тест краснел на установленном TgWsProxy.
            SetAutomaticUpdateResults("1.2", null, "1.2", "1.2", "1.2", "1.2");
            if (_zapUpdateBtn.Visibility != Visibility.Visible)
                throw new Exception("missing engine should show download button");

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
