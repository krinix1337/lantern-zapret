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
        ListBox _listBox;
        TextBox _entryInput;
        TextBlock _listCount;
        TextBlock _listHint;
        Button _addBtn, _delBtn;
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
            var selRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            selRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _listSelector = Combo(220);
            foreach (var d in ListDefs())
                _listSelector.Items.Add(d[0]);
            _listSelector.SelectedIndex = 0;
            _listSelector.SelectionChanged += (s, e) => ReloadList();
            Grid.SetColumn(_listSelector, 0);
            selRow.Children.Add(_listSelector);

            _listCount = new TextBlock { Text = "", Foreground = Theme.BrFaint, FontSize = Theme.FsSmall,
                FontFamily = Theme.MonoFont, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(_listCount, 1);
            selRow.Children.Add(_listCount);

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

            // Второстепенное действие — отдельной строкой: в одной строке с комбоксом
            // оно не оставляло места счётчику записей.
            var openMachineBtn = Ctl.Button(Loc.T("filters.openIpsetMachine"), Icons.Folder, 3);
            openMachineBtn.HorizontalAlignment = HorizontalAlignment.Left;
            openMachineBtn.Margin = new Thickness(0, 0, 0, 10);
            openMachineBtn.Click += (s, e) =>
            {
                var res = MessageBox.Show(Loc.T("filters.openIpsetWarn"), Core.AppName, MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    string machinePath = Path.Combine(Core.Lists, "ipset-all.txt");
                    if (File.Exists(machinePath)) Core.OpenFile(machinePath);
                    else Core.Warn("Файл не найден: " + machinePath);
                }
            };
            panel.Children.Add(openMachineBtn);

            // Список записей
            _listBox = new ListBox
            {
                Height = 180, Background = Theme.BrSurfaceAlt, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), FontFamily = Theme.MonoFont, FontSize = Theme.FsSmall,
                Foreground = Theme.BrText, Padding = new Thickness(4)
            };
            panel.Children.Add(_listBox);

            // Панель добавления/удаления: поле тянется (Star), кнопки фиксированные.
            // С Width=300 в горизонтальном StackPanel ряд на узком окне вылезал за
            // карточку вместе с кнопкой «Удалить».
            var editRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            editRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _entryInput = new TextBox
            {
                MinWidth = 120, Height = 34, Background = Theme.BrSurface, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), Foreground = Theme.BrText, CaretBrush = Theme.BrText,
                FontSize = Theme.FsBody, FontFamily = Theme.MonoFont,
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 0, 10, 0)
            };
            _entryInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddEntry(); };
            Grid.SetColumn(_entryInput, 0);
            editRow.Children.Add(_entryInput);

            _addBtn = Ctl.Button(Loc.T("filters.list.add"), Icons.Check, 0);
            _addBtn.Margin = new Thickness(8, 0, 0, 0);
            _addBtn.Click += (s, e) => AddEntry();
            Grid.SetColumn(_addBtn, 1);
            editRow.Children.Add(_addBtn);

            _delBtn = Ctl.Button(Loc.T("filters.list.del"), Icons.Cross, 2);
            _delBtn.Margin = new Thickness(8, 0, 0, 0);
            _delBtn.Click += (s, e) => DelEntry();
            Grid.SetColumn(_delBtn, 2);
            editRow.Children.Add(_delBtn);

            panel.Children.Add(editRow);
            Body.Children.Add(UI.Card(panel, new Thickness(16, 14, 16, 14)));
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
        }

        void ReloadList()
        {
            UpdateListHint();
            _listBox.Items.Clear();
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
                                _listBox.Items.Add(s);
                        }
                    }
                    if (count > MaxDisplay)
                    {
                        _moreItem = string.Format(Loc.T("filters.list.more"), count - MaxDisplay);
                        _listBox.Items.Add(_moreItem);
                    }
                }
            }
            catch { }
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
            // №105: IpsetStatus() ищет заглушку через Contains, поэтому и запрет
            // по Contains — иначе «1.2.3.4 203.0.113.113/32» прошёл бы фильтром и
            // перевёл бы режим в none.
            if (isIpSet && val.IndexOf(Core.IpsetSentinel, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Core.Warn(Loc.T("filters.list.sentinelRejected"));
                return;
            }
            if (isIpSet && Core.IpsetStatus() == "none")
            {
                Core.Warn(Loc.T("filters.list.addedNoneWarn"));
            }
            try
            {
                lock (_listWriteLock) File.AppendAllText(path, val + Environment.NewLine);
                _entryInput.Text = "";
                ReloadList();
                MarkDirty();
                Core.Info(string.Format(Loc.T("filters.list.added"), val));
            }
            catch (Exception ex) { Core.Fail(ex.Message); }
        }

        void DelEntry()
        {
            if (_listIoBusy) return;
            if (_listBox.SelectedIndex < 0) return;
            string val = _listBox.SelectedItem as string;
            if (val == null) return;
            if (_moreItem != null && val == _moreItem) return;
            string path = CurrentListPath();

            _listIoBusy = true;
            if (_addBtn != null) _addBtn.IsEnabled = false;
            if (_delBtn != null) _delBtn.IsEnabled = false;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                int removed = 0;
                Exception fail = null;
                try
                {
                    // №101: весь файл перезаписывается, поэтому под общим локом и
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
                        if (_delBtn != null) _delBtn.IsEnabled = true;
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
