using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZapretStudio
{
    class FiltersPage : Page
    {
        public override string Title { get { return Loc.T("filters.title"); } }
        public override string Subtitle { get { return Loc.T("filters.sub"); } }

        readonly MainWindow _win;
        ComboBox _gameMode, _ipsetMode;
        TextBox _gameTcpPorts, _gameUdpPorts;
        bool _syncing;
        Border _restartBar;
        Button _updListsBtn;
        TextBlock _listsStatus;
        Border _gameIpsetWarning;

        // Визуальный редактор списков
        ComboBox _listSelector;
        StackPanel _listRows;
        TextBox _entryInput;
        TextBlock _entryPlaceholder, _entryMsg;
        TextBlock _listCount;
        TextBlock _listHint;
        Button _addBtn;
        // Строка-заглушка «показаны не все». Сравниваем с ней точно, а не по
        // префиксу «... +»: текст живёт в локализации и меняется вместе с языком.
        string _moreItem;
        volatile bool _listIoBusy;
        static readonly object _listWriteLock = new object();

        public FiltersPage(MainWindow win)
        {
            _win = win;
            BuildGame();
            BuildIpset();
            BuildListEditor();
            BuildRestartBar();
        }

        public override void OnShow() { Sync(); }

        void BuildGame()
        {
            Body.Children.Add(SectionLabel(Loc.T("filters.sec.game")));

            _gameMode = Combo(185);
            _gameMode.Items.Add(Loc.T("filters.game.off"));
            _gameMode.Items.Add(Loc.T("filters.game.all"));
            _gameMode.Items.Add(Loc.T("filters.game.tcp"));
            _gameMode.Items.Add(Loc.T("filters.game.udp"));
            _gameMode.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                Core.GameMode = _gameMode.SelectedIndex == 1 ? "all" : _gameMode.SelectedIndex == 2 ? "tcp" : _gameMode.SelectedIndex == 3 ? "udp" : "off";
                MarkDirty();
                UpdateGameIpsetWarning();
            };
            Body.Children.Add(Row(Loc.T("filters.game"), Loc.T("filters.game.desc"), _gameMode));
            _gameIpsetWarning = NoteCard(Icons.Warn, Theme.BrWarn, Loc.T("filters.ipset.off.hidesGame"), Sev.Warn);
            _gameIpsetWarning.Margin = new Thickness(0, 8, 0, 0);
            _gameIpsetWarning.Visibility = Visibility.Collapsed;
            Body.Children.Add(_gameIpsetWarning);
            Body.Children.Add(new Border { Height = 10 });

            // Порты и версионный гейтинг (zapret 1.10.3+)
            bool isGated = SettingsPage.CompareVersions(Core.ZapretVersion(), "1.10.3") < 0;

            var portsCard = new StackPanel();

            if (isGated)
            {
                var lockGrid = new Grid();
                lockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                lockGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                left.Children.Add(UI.T(Loc.T("filters.game.portsTitle"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
                left.Children.Add(new TextBlock
                {
                    Text = Loc.T("filters.game.locked"),
                    Foreground = Theme.BrWarn,
                    FontSize = Theme.FsSmall,
                    FontFamily = Theme.UiFont,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 4, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                Grid.SetColumn(left, 0);
                lockGrid.Children.Add(left);

                var updBtn = Ctl.Button(Loc.T("filters.game.updateEngine"), Icons.Download, 1);
                updBtn.Click += (s, e) => _win.Navigate("updates");
                var rc = new ContentControl { Content = updBtn, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
                Grid.SetColumn(rc, 1);
                lockGrid.Children.Add(rc);

                portsCard.Children.Add(lockGrid);
            }
            else
            {
                var headerGrid = new Grid();
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var titleBox = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                titleBox.Children.Add(UI.T(Loc.T("filters.game.portsTitle"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
                titleBox.Children.Add(new TextBlock
                {
                    Text = Loc.T("filters.game.portsDesc"),
                    Foreground = Theme.BrMuted,
                    FontSize = Theme.FsSmall,
                    FontFamily = Theme.UiFont,
                    Margin = new Thickness(0, 3, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });
                Grid.SetColumn(titleBox, 0);
                headerGrid.Children.Add(titleBox);

                var rtmpBtn = Ctl.Button(Loc.T("filters.game.excludeRtmp"), Icons.Filter, 3);
                rtmpBtn.VerticalAlignment = VerticalAlignment.Center;
                var rtmpBorder = Ctl.Surface(rtmpBtn);
                var rtmpBox = Ctl.ContentHost(rtmpBtn);
                if (rtmpBorder != null)
                {
                    rtmpBorder.Height = 32;
                    rtmpBorder.CornerRadius = Theme.R8;
                    if (rtmpBox != null) rtmpBox.Padding = new Thickness(12, 0, 12, 0);
                    var sp = (rtmpBox != null ? rtmpBox.Child : null) as StackPanel;
                    if (sp != null)
                    {
                        foreach (var ch in sp.Children)
                        {
                            var tb = ch as TextBlock;
                            if (tb != null) { tb.FontSize = Theme.FsSmall; tb.VerticalAlignment = VerticalAlignment.Center; }
                        }
                    }
                }
                rtmpBtn.Click += (s, e) =>
                {
                    _gameTcpPorts.Text = "1024-1934,1936-65535";
                    Core.GameFilterTcpPorts = "1024-1934,1936-65535";
                    MarkDirty();
                };
                var btnHost = new ContentControl { Content = rtmpBtn, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
                Grid.SetColumn(btnHost, 1);
                headerGrid.Children.Add(btnHost);

                portsCard.Children.Add(headerGrid);

                var inputsGrid = new Grid { Margin = new Thickness(0, 14, 0, 0) };
                inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                inputsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var tcpCol = new StackPanel();
                tcpCol.Children.Add(new TextBlock
                {
                    Text = Loc.T("filters.game.portsTcp"),
                    Foreground = Theme.BrMuted,
                    FontSize = Theme.FsSmall,
                    FontFamily = Theme.UiFont,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                _gameTcpPorts = new TextBox
                {
                    Height = 32,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Theme.BrText,
                    CaretBrush = Theme.BrText,
                    FontSize = Theme.FsSmall,
                    FontFamily = Theme.MonoFont,
                    TextAlignment = TextAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(10, 0, 10, 0),
                    Text = Core.GameFilterTcpPorts
                };
                _gameTcpPorts.TextChanged += (s, e) => { if (!_syncing && _restartBar != null) _restartBar.Visibility = Visibility.Visible; };
                var tcpBox = new Border
                {
                    Height = 32,
                    Background = Theme.BrSurfaceAlt,
                    BorderBrush = Theme.BrStroke,
                    BorderThickness = new Thickness(1),
                    CornerRadius = Theme.R6,
                    Child = _gameTcpPorts
                };
                _gameTcpPorts.GotFocus += (s, e) => tcpBox.BorderBrush = Theme.BrAccent;
                _gameTcpPorts.LostFocus += (s, e) =>
                {
                    tcpBox.BorderBrush = Theme.BrStroke;
                    if (!_syncing)
                    {
                        string p = _gameTcpPorts.Text.Trim();
                        if (p != Core.GameFilterTcpPorts)
                        {
                            Core.GameFilterTcpPorts = p;
                            MarkDirty();
                        }
                    }
                };
                tcpCol.Children.Add(tcpBox);
                Grid.SetColumn(tcpCol, 0);
                inputsGrid.Children.Add(tcpCol);

                var udpCol = new StackPanel();
                udpCol.Children.Add(new TextBlock
                {
                    Text = Loc.T("filters.game.portsUdp"),
                    Foreground = Theme.BrMuted,
                    FontSize = Theme.FsSmall,
                    FontFamily = Theme.UiFont,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 6)
                });
                _gameUdpPorts = new TextBox
                {
                    Height = 32,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = Theme.BrText,
                    CaretBrush = Theme.BrText,
                    FontSize = Theme.FsSmall,
                    FontFamily = Theme.MonoFont,
                    TextAlignment = TextAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(10, 0, 10, 0),
                    Text = Core.GameFilterUdpPorts
                };
                _gameUdpPorts.TextChanged += (s, e) => { if (!_syncing && _restartBar != null) _restartBar.Visibility = Visibility.Visible; };
                var udpBox = new Border
                {
                    Height = 32,
                    Background = Theme.BrSurfaceAlt,
                    BorderBrush = Theme.BrStroke,
                    BorderThickness = new Thickness(1),
                    CornerRadius = Theme.R6,
                    Child = _gameUdpPorts
                };
                _gameUdpPorts.GotFocus += (s, e) => udpBox.BorderBrush = Theme.BrAccent;
                _gameUdpPorts.LostFocus += (s, e) =>
                {
                    udpBox.BorderBrush = Theme.BrStroke;
                    if (!_syncing)
                    {
                        string p = _gameUdpPorts.Text.Trim();
                        if (p != Core.GameFilterUdpPorts)
                        {
                            Core.GameFilterUdpPorts = p;
                            MarkDirty();
                        }
                    }
                };
                udpCol.Children.Add(udpBox);
                Grid.SetColumn(udpCol, 2);
                inputsGrid.Children.Add(udpCol);

                portsCard.Children.Add(inputsGrid);
            }

            Body.Children.Add(UI.Card(portsCard, new Thickness(16, 14, 16, 14)));
        }

        void BuildIpset()
        {
            Body.Children.Add(SectionLabel(Loc.T("filters.sec.ipset")));
            // 185 px не хватало подписи «Загруженный список» в Onest: текст
            // упирался в стрелку и обрезался на последней букве.
            _ipsetMode = Combo(215);
            _ipsetMode.Items.Add(Loc.T("filters.ipset.loaded"));
            _ipsetMode.Items.Add(Loc.T("filters.ipset.none"));
            _ipsetMode.Items.Add(Loc.T("filters.ipset.any"));
            _ipsetMode.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                Core.SetIpsetMode(_ipsetMode.SelectedIndex == 1 ? "none" : _ipsetMode.SelectedIndex == 2 ? "any" : "loaded");
                MarkDirty();
                ResyncCombos();
                UpdateGameIpsetWarning();
            };
            Body.Children.Add(Row(Loc.T("filters.ipset"), Loc.T("filters.ipset.on"), _ipsetMode));

            _updListsBtn = Ctl.Button(Loc.T("filters.updateLists"), Icons.Refresh, 1);
            _updListsBtn.HorizontalAlignment = HorizontalAlignment.Left;
            _updListsBtn.Margin = new Thickness(0, 10, 0, 0);
            _updListsBtn.Click += (s, e) => UpdateLists();
            Body.Children.Add(_updListsBtn);

            // Результат обновления висит под кнопкой: журнал виден не всегда, а без
            // строки состояние «обновилось / не обновилось / уже актуально»
            // невозможно определить, пока не перезапустил обход.
            _listsStatus = new TextBlock
            {
                Margin = new Thickness(0, 8, 0, 0),
                FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont,
                Foreground = Theme.BrMuted,
                TextWrapping = TextWrapping.Wrap,
                Visibility = Visibility.Collapsed
            };
            Body.Children.Add(_listsStatus);
        }

        void SetListsStatus(string text, Brush color)
        {
            if (_listsStatus == null) return;
            _listsStatus.Text = text;
            _listsStatus.Foreground = color;
            _listsStatus.Visibility = Visibility.Visible;
        }

        void UpdateLists()
        {
            if (_updListsBtn != null) _updListsBtn.IsEnabled = false;
            Core.Info(Loc.T("filters.updatingLists"));
            SetListsStatus(Loc.T("filters.updatingLists"), Theme.BrMuted);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string err = null;
                bool ok = false;
                Core.IpsetUpdateStat stat = null;
                try
                {
                    ok = Core.UpdateIpsetList(out err, out stat);
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                }
                try
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (_updListsBtn != null) _updListsBtn.IsEnabled = true;
                        if (ok)
                        {
                            string msg;
                            if (stat == null)
                                msg = string.Format(Loc.T("filters.listsUpdated"), Core.IpsetCount());
                            else if (!stat.Changed)
                                msg = string.Format(Loc.T("filters.listsUnchanged"), stat.After);
                            else
                                msg = string.Format(Loc.T("filters.listsDiff"),
                                    stat.Before, stat.After, stat.Added, stat.Removed);
                            SetListsStatus(msg, Theme.BrOk);
                            Core.Good(msg);
                            // Флаг «нужен перезапуск» ставим только когда список
                            // действительно поменялся — иначе панель перезапуска
                            // появляется на каждое нажатие кнопки.
                            if (stat == null || stat.Changed) MarkDirty();
                            ReloadList();
                        }
                        else
                        {
                            string msg = string.Format(Loc.T("filters.listsErr"),
                                !string.IsNullOrEmpty(err) ? err : "error");
                            SetListsStatus(msg, Theme.BrErr);
                            Core.Fail(msg);
                        }
                    });
                }
                catch { }
            });
        }

        // ---------- Визуальный редактор списков ----------
        static string[][] ListDefs()
        {
            return new string[][]
            {
                new[] { "list-general",     Path.Combine(Core.Lists, "list-general-user.txt") },
                new[] { "list-exclude",     Path.Combine(Core.Lists, "list-exclude-user.txt") },
                new[] { "ipset-exclude",    Path.Combine(Core.Lists, "ipset-exclude-user.txt") },
            };
        }

        void BuildListEditor()
        {
            Body.Children.Add(SectionLabel(Loc.T("filters.sec.lists")));

            var panel = new StackPanel();

            // Выбор списка. Grid, а не горизонтальный StackPanel: StackPanel
            // меряется бесконечной шириной и на узком окне уезжал за край карточки
            // (аудит разметки на 820 px это и ловил).
            var selRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _listSelector = Combo(220);
            foreach (var d in ListDefs())
                _listSelector.Items.Add(d[0]);
            _listSelector.SelectedIndex = 0;
            _listSelector.SelectionChanged += (s, e) => ReloadList();
            Grid.SetColumn(_listSelector, 0);
            selRow.Children.Add(_listSelector);

            // Счётчик записей — под списком (см. ниже): в строке с комбоксом и
            // кнопкой на 820 px он залезал под кнопку.
            _listCount = new TextBlock { Text = "", Foreground = Theme.BrFaint, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 6, 2, 0) };

            // Второстепенное действие — компактно справа в той же строке, а не
            // отдельной крупной кнопкой посреди карточки.
            var openMachineBtn = Ctl.Button(Loc.T("filters.openIpsetMachine"), Icons.Folder, 3);
            var omBd = Ctl.Surface(openMachineBtn);
            var omBox = Ctl.ContentHost(openMachineBtn);
            if (omBd != null) { omBd.Height = 32; omBd.CornerRadius = Theme.R8; if (omBox != null) omBox.Padding = new Thickness(10, 0, 10, 0); }
            openMachineBtn.VerticalAlignment = VerticalAlignment.Center;
            openMachineBtn.Margin = new Thickness(12, 0, 0, 0);
            openMachineBtn.ToolTip = Loc.T("filters.openIpsetWarn");
            openMachineBtn.Click += (s, e) =>
            {
                var res = MessageBox.Show(Loc.T("filters.openIpsetWarn"), Core.AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    string machinePath = Path.Combine(Core.Lists, "ipset-all.txt");
                    if (File.Exists(machinePath)) Core.OpenFile(machinePath);
                    else Core.Warn(string.Format(Loc.T("filters.fileNotFound"), machinePath));
                }
            };
            Grid.SetColumn(openMachineBtn, 2);
            selRow.Children.Add(openMachineBtn);

            panel.Children.Add(selRow);

            // Под списком — пояснение, за что он отвечает: три списка выглядят
            // одинаково, а путаница «куда вписать адрес» стоит пользователю
            // неработающего обхода.
            _listHint = new TextBlock
            {
                Text = "",
                Foreground = Theme.BrFaint,
                FontSize = Theme.FsTiny,
                FontFamily = Theme.UiFont,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            };
            panel.Children.Add(_listHint);

            // Добавление — сверху, над списком: поле с подсказкой-примером и одна
            // кнопка. Раньше поле и «Добавить/Удалить» стояли под списком, а
            // удаление требовало сначала выделить строку в голом ListBox.
            var editRow = new Grid();
            editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var inputHost = new Grid();
            _entryInput = new TextBox
            {
                MinWidth = 120, Height = 34, Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Foreground = Theme.BrText, CaretBrush = Theme.BrText,
                FontSize = Theme.FsBody, FontFamily = Theme.MonoFont,
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 0, 10, 0)
            };
            _entryInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddEntry(); };
            _entryPlaceholder = new TextBlock
            {
                Foreground = Theme.BrFaint, FontSize = Theme.FsBody, FontFamily = Theme.UiFont,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(13, 0, 10, 0),
                IsHitTestVisible = false, TextTrimming = TextTrimming.CharacterEllipsis
            };
            _entryInput.TextChanged += (s, e) =>
            {
                _entryPlaceholder.Visibility = string.IsNullOrEmpty(_entryInput.Text) ? Visibility.Visible : Visibility.Collapsed;
                SetEntryMsg(null, Sev.Neutral);
            };
            inputHost.Children.Add(_entryInput);
            inputHost.Children.Add(_entryPlaceholder);
            // Рамка со скруглением — снаружи: у штатного шаблона TextBox углы
            // прямые, и поле выглядело чужим рядом со скруглёнными кнопками.
            var inputFrame = new Border
            {
                Height = 36, Background = Theme.BrSurfaceAlt, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), CornerRadius = Theme.R8, Child = inputHost
            };
            _entryInput.GotFocus += (s, e) => inputFrame.BorderBrush = Theme.BrAccent;
            _entryInput.LostFocus += (s, e) => inputFrame.BorderBrush = Theme.BrStroke;
            Grid.SetColumn(inputFrame, 0);
            editRow.Children.Add(inputFrame);

            _addBtn = Ctl.Button(Loc.T("filters.list.add"), Icons.Check, 0);
            _addBtn.Margin = new Thickness(8, 0, 0, 0);
            _addBtn.VerticalAlignment = VerticalAlignment.Center;
            _addBtn.Click += (s, e) => AddEntry();
            Grid.SetColumn(_addBtn, 1);
            editRow.Children.Add(_addBtn);
            panel.Children.Add(editRow);

            // Отказ (дубликат, не IP, служебная запись) — прямо под полем, а не
            // только в журнале, где его никто не видел.
            _entryMsg = new TextBlock
            {
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 6, 0, 0), Visibility = Visibility.Collapsed
            };
            panel.Children.Add(_entryMsg);

            // Записи — строками с собственной кнопкой удаления.
            _listRows = new StackPanel();
            var listFrame = new Border
            {
                Margin = new Thickness(0, 10, 0, 0),
                Background = Theme.BrSurfaceAlt, BorderBrush = Theme.BrStroke, BorderThickness = new Thickness(1),
                CornerRadius = Theme.R8, Padding = new Thickness(4),
                Child = new ScrollViewer
                {
                    MaxHeight = 260, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = _listRows
                }
            };
            panel.Children.Add(listFrame);
            panel.Children.Add(_listCount);
            Body.Children.Add(UI.Card(panel, new Thickness(16, 14, 16, 14)));
        }

        void SetEntryMsg(string text, Sev sev)
        {
            if (_entryMsg == null) return;
            if (string.IsNullOrEmpty(text)) { _entryMsg.Visibility = Visibility.Collapsed; return; }
            _entryMsg.Text = text;
            _entryMsg.Foreground = sev == Sev.Err ? Theme.BrErr : (sev == Sev.Warn ? Theme.BrWarn : Theme.BrMuted);
            _entryMsg.Visibility = Visibility.Visible;
        }

        FrameworkElement EntryRow(string value)
        {
            var g = new Grid { MinHeight = 32 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var tb = new TextBlock
            {
                Text = value, Foreground = Theme.BrText, FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(8, 0, 8, 0), ToolTip = value
            };
            g.Children.Add(tb);

            var del = Ctl.Button(null, Icons.Trash, 1);
            var dBd = Ctl.Surface(del);
            var dBox = Ctl.ContentHost(del);
            if (dBd != null) { dBd.Height = 26; dBd.CornerRadius = Theme.R8; dBd.Background = Brushes.Transparent; dBd.BorderBrush = Brushes.Transparent; if (dBox != null) dBox.Padding = new Thickness(6, 0, 6, 0); }
            string tip = string.Format(Loc.T("filters.list.delTip"), value);
            del.ToolTip = tip;
            Ctl.AutomationSetName(del, tip);
            del.VerticalAlignment = VerticalAlignment.Center;
            del.Opacity = 0.45;
            del.Click += (s, e) => DelEntry(value);
            Grid.SetColumn(del, 1);
            g.Children.Add(del);

            var row = new Border { CornerRadius = Theme.R8, Background = Brushes.Transparent, Child = g, Padding = new Thickness(0, 1, 2, 1) };
            row.MouseEnter += (s, e) => { row.Background = Theme.Alpha(Theme.Text, 8); del.Opacity = 1; };
            row.MouseLeave += (s, e) => { row.Background = Brushes.Transparent; del.Opacity = 0.45; };
            return row;
        }

        string CurrentListPath()
        {
            int idx = _listSelector.SelectedIndex;
            if (idx < 0) idx = 0;
            return ListDefs()[idx][1];
        }

        // Подпись под комбоксом: за что отвечает выбранный список.
        void UpdateListHint()
        {
            if (_listHint == null) return;
            var defs = ListDefs();
            int i = _listSelector != null ? _listSelector.SelectedIndex : -1;
            _listHint.Text = (i >= 0 && i < defs.Length) ? Loc.T("filters.list.about." + defs[i][0]) : "";
            if (_entryPlaceholder != null)
            {
                bool ip = CurrentListPath().IndexOf("ipset", StringComparison.OrdinalIgnoreCase) >= 0;
                _entryPlaceholder.Text = Loc.T(ip ? "filters.list.placeholderIp" : "filters.list.placeholder");
            }
            SetEntryMsg(null, Sev.Neutral);
        }

        void ReloadList()
        {
            UpdateListHint();
            _listRows.Children.Clear();
            _moreItem = null;
            string path = CurrentListPath();
            int count = 0;
            const int MaxDisplay = 300;
            try
            {
                if (File.Exists(path))
                {
                    using (var reader = new StreamReader(path))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            string s = line.Trim();
                            if (s.Length == 0 || s.StartsWith("#")) continue;
                            count++;
                            if (count <= MaxDisplay)
                                _listRows.Children.Add(EntryRow(s));
                        }
                    }
                    if (count > MaxDisplay)
                    {
                        _moreItem = string.Format(Loc.T("filters.list.more"), count - MaxDisplay);
                        _listRows.Children.Add(new TextBlock { Text = _moreItem, Foreground = Theme.BrFaint,
                            FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(8, 6, 8, 6),
                            TextWrapping = TextWrapping.Wrap });
                    }
                }
            }
            catch { }
            if (count == 0)
                _listRows.Children.Add(new TextBlock { Text = Loc.T("filters.list.empty"), Foreground = Theme.BrFaint,
                    FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(8, 10, 8, 10),
                    TextWrapping = TextWrapping.Wrap });
            _listCount.Text = string.Format(Loc.T("filters.list.count"), count);
        }

        void AddEntry()
        {
            if (_listIoBusy) return;
            string raw = (_entryInput.Text ?? "").Trim();
            if (raw.Length == 0) return;
            string path = CurrentListPath();
            bool isIpSet = path.IndexOf("ipset", StringComparison.OrdinalIgnoreCase) >= 0;
            string val = Core.NormalizeHostEntry(raw, isIpSet);
            if (val.Length == 0) return;
            // IpsetStatus() ищет заглушку через Contains, поэтому и запрет
            // по Contains: иначе «1.2.3.4 203.0.113.113/32» прошёл бы фильтром и
            // перевёл бы режим в none.
            if (isIpSet && val.IndexOf(Core.IpsetSentinel, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Core.Warn(Loc.T("filters.list.sentinelRejected"));
                SetEntryMsg(Loc.T("filters.list.sentinelRejected"), Sev.Err);
                return;
            }
            if (isIpSet && !val.StartsWith("#") && !Core.IsValidIpsetEntry(val))
            {
                Core.Warn(string.Format(Loc.T("filters.list.badIp"), val));
                SetEntryMsg(string.Format(Loc.T("filters.list.badIp"), val), Sev.Err);
                return;
            }
            string noneWarn = null;
            if (isIpSet && Core.IpsetStatus() == "none")
            {
                noneWarn = Loc.T("filters.list.addedNoneWarn");
                Core.Warn(noneWarn);
            }
            try
            {
                bool dup = false;
                lock (_listWriteLock)
                {
                    string existing = File.Exists(path) ? File.ReadAllText(path) : "";
                    foreach (var line in existing.Split('\n'))
                    {
                        if (string.Equals(line.Trim(), val, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                    }
                    if (!dup)
                    {
                        // Если файл правили вручную и последняя строка без перевода
                        // строки, AppendAllText приклеивал запись к ней
                        // («example.comnew.site»).
                        string prefix = existing.Length > 0 && !existing.EndsWith("\n") ? Environment.NewLine : "";
                        File.AppendAllText(path, prefix + val + Environment.NewLine);
                    }
                }
                if (dup)
                {
                    Core.Warn(string.Format(Loc.T("filters.list.dup"), val));
                    SetEntryMsg(string.Format(Loc.T("filters.list.dup"), val), Sev.Warn);
                    return;
                }
                _entryInput.Text = "";
                ReloadList();
                MarkDirty();
                Core.Info(string.Format(Loc.T("filters.list.added"), val));
                if (noneWarn != null) SetEntryMsg(noneWarn, Sev.Warn);
            }
            catch (Exception ex) { Core.Fail(ex.Message); SetEntryMsg(ex.Message, Sev.Err); }
        }

        void DelEntry(string val)
        {
            if (_listIoBusy) return;
            if (string.IsNullOrEmpty(val)) return;
            string path = CurrentListPath();

            _listIoBusy = true;
            if (_addBtn != null) _addBtn.IsEnabled = false;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int removed = 0;
                Exception fail = null;
                try
                {
                    // Весь файл перезаписывается, поэтому под общим локом и
                    // атомарно: прежний File.WriteAllLines при обрыве оставлял пустой
                    // пользовательский список.
                    lock (_listWriteLock)
                    {
                        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
                        removed = lines.RemoveAll(delegate (string l) { return l.Trim() == val; });
                        if (removed > 0)
                            Core.WriteFileAtomic(path, string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine);
                    }
                }
                catch (Exception ex) { fail = ex; }
                try
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        _listIoBusy = false;
                        if (_addBtn != null) _addBtn.IsEnabled = true;
                        if (fail != null) { Core.Fail(fail.Message); return; }
                        if (removed == 0) return;
                        ReloadList();
                        MarkDirty();
                        // RemoveAll сносит все совпадения, включая скрытые ниже
                        // 300-строчного окна — пользователь обязан это видеть.
                        if (removed > 1)
                            Core.Info(string.Format(Loc.T("filters.list.removedCount"), val, removed));
                        else
                            Core.Info(string.Format(Loc.T("filters.list.removed"), val));
                    });
                }
                catch { _listIoBusy = false; }
            });
        }

        void BuildRestartBar()
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var l = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            l.Children.Add(UI.Icon(Icons.Refresh, 18, Theme.BrWarn, 1.8));
            l.Children.Add(new TextBlock { Text = Loc.T("filters.restart.note"),
                Foreground = Theme.BrText, FontSize = Theme.FsSmall, FontFamily = Theme.UiFont,
                Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(l, 0); g.Children.Add(l);
            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            var now = Ctl.Button(Loc.T("filters.restartNow"), Icons.Restart, 0);
            now.Margin = new Thickness(0, 0, 8, 0);
            now.Click += (s, e) => { _win.RestartAll(); ClearDirty(); };
            var later = Ctl.Button(Loc.T("common.later"), null, 3);
            later.Click += (s, e) => ClearDirty();
            btns.Children.Add(now); btns.Children.Add(later);
            Grid.SetColumn(btns, 1); g.Children.Add(btns);
            _restartBar = UI.Card(g, new Thickness(16, 12, 16, 12), Theme.R10, Theme.Alpha(Theme.Warn, 16));
            _restartBar.BorderBrush = Theme.Alpha(Theme.Warn, 70);
            _restartBar.Margin = new Thickness(0, 18, 0, 0);
            _restartBar.Visibility = Visibility.Collapsed;
            Body.Children.Add(_restartBar);
        }

        void Sync()
        {
            _syncing = true;
            try
            {
                _gameMode.SelectedIndex = Core.GameMode == "all" ? 1 : Core.GameMode == "tcp" ? 2 : Core.GameMode == "udp" ? 3 : 0;
                if (_gameTcpPorts != null) _gameTcpPorts.Text = Core.GameFilterTcpPorts;
                if (_gameUdpPorts != null) _gameUdpPorts.Text = Core.GameFilterUdpPorts;
                string ipset = Core.IpsetStatus();
                _ipsetMode.SelectedIndex = ipset == "none" ? 1 : ipset == "any" ? 2 : 0;
            }
            finally { _syncing = false; }
            UpdateGameIpsetWarning();
            ReloadList();
        }

        void ResyncCombos()
        {
            _syncing = true;
            try
            {
                string ipset = Core.IpsetStatus();
                _ipsetMode.SelectedIndex = ipset == "none" ? 1 : ipset == "any" ? 2 : 0;
            }
            finally { _syncing = false; }
            UpdateGameIpsetWarning();
        }

        void UpdateGameIpsetWarning()
        {
            if (_gameIpsetWarning != null)
            {
                bool hidden = !Core.IpsetEnabled && Core.GameMode != "off";
                _gameIpsetWarning.Visibility = hidden ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        void MarkDirty()
        {
            Core.SaveConfig();
            if (_restartBar != null) _restartBar.Visibility = Visibility.Visible;
        }
        void ClearDirty()
        {
            if (_restartBar != null) _restartBar.Visibility = Visibility.Collapsed;
        }
    }
}
