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

        // Визуальный редактор списков
        ComboBox _listSelector;
        ListBox _listBox;
        TextBox _entryInput;
        TextBlock _listCount;

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

            var cardPanel = new StackPanel();

            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(UI.T(Loc.T("filters.game"), Theme.FsBody, Theme.BrText, FontWeights.SemiBold));
            left.Children.Add(new TextBlock { Text = Loc.T("filters.game.desc"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 3, 0, 0),
                TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(left, 0); topRow.Children.Add(left);

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
            };
            var modeWrap = new ContentControl { Content = _gameMode, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            Grid.SetColumn(modeWrap, 1); topRow.Children.Add(modeWrap);
            cardPanel.Children.Add(topRow);

            // Порты и версионный гейтинг (zapret 1.10.3+)
            bool isGated = SettingsPage.CompareVersions(Core.ZapretVersion(), "1.10.3") < 0;

            var portsSection = new StackPanel { Margin = new Thickness(0, 16, 0, 0) };
            if (isGated)
            {
                var lockSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
                lockSp.Children.Add(new TextBlock
                {
                    Text = Loc.T("filters.game.locked"),
                    Foreground = Theme.BrWarn,
                    FontSize = Theme.FsSmall,
                    FontFamily = Theme.UiFont,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                var updBtn = Ctl.Button(Loc.T("filters.game.updateEngine"), Icons.Download, 1);
                updBtn.Margin = new Thickness(10, 0, 0, 0);
                updBtn.Click += (s, e) => _win.Navigate("updates");
                lockSp.Children.Add(updBtn);
                portsSection.Children.Add(lockSp);
            }

            var portsGrid = new Grid { Opacity = isGated ? 0.5 : 1.0, IsEnabled = !isGated };
            portsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            portsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var tcpSp = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
            tcpSp.Children.Add(new TextBlock { Text = Loc.T("filters.game.portsTcp"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 0, 0, 4) });
            _gameTcpPorts = new TextBox
            {
                Height = 32, Background = Theme.BrSurfaceAlt, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), Foreground = Theme.BrText, CaretBrush = Theme.BrText,
                FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 0, 8, 0),
                Text = Core.GameFilterTcpPorts
            };
            _gameTcpPorts.TextChanged += (s, e) => { if (!_syncing) { Core.GameFilterTcpPorts = _gameTcpPorts.Text.Trim(); MarkDirty(); } };
            tcpSp.Children.Add(_gameTcpPorts);
            Grid.SetColumn(tcpSp, 0); portsGrid.Children.Add(tcpSp);

            var udpSp = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            udpSp.Children.Add(new TextBlock { Text = Loc.T("filters.game.portsUdp"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, Margin = new Thickness(0, 0, 0, 4) });
            _gameUdpPorts = new TextBox
            {
                Height = 32, Background = Theme.BrSurfaceAlt, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), Foreground = Theme.BrText, CaretBrush = Theme.BrText,
                FontSize = Theme.FsSmall, FontFamily = Theme.MonoFont,
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(8, 0, 8, 0),
                Text = Core.GameFilterUdpPorts
            };
            _gameUdpPorts.TextChanged += (s, e) => { if (!_syncing) { Core.GameFilterUdpPorts = _gameUdpPorts.Text.Trim(); MarkDirty(); } };
            udpSp.Children.Add(_gameUdpPorts);
            Grid.SetColumn(udpSp, 1); portsGrid.Children.Add(udpSp);

            portsSection.Children.Add(portsGrid);

            var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0),
                Opacity = isGated ? 0.5 : 1.0, IsEnabled = !isGated };
            var rtmpBtn = Ctl.Button(Loc.T("filters.game.excludeRtmp"), Icons.Filter, 3);
            rtmpBtn.Click += (s, e) =>
            {
                _gameTcpPorts.Text = "1024-1934,1936-65535";
                Core.GameFilterTcpPorts = "1024-1934,1936-65535";
                MarkDirty();
            };
            presetRow.Children.Add(rtmpBtn);
            portsSection.Children.Add(presetRow);

            cardPanel.Children.Add(portsSection);
            Body.Children.Add(UI.Card(cardPanel, new Thickness(16, 14, 16, 14)));
        }

        void BuildIpset()
        {
            Body.Children.Add(SectionLabel(Loc.T("filters.sec.ipset")));
            _ipsetMode = Combo(185);
            _ipsetMode.Items.Add(Loc.T("filters.ipset.loaded"));
            _ipsetMode.Items.Add(Loc.T("filters.ipset.none"));
            _ipsetMode.Items.Add(Loc.T("filters.ipset.any"));
            _ipsetMode.SelectionChanged += (s, e) =>
            {
                if (_syncing) return;
                Core.SetIpsetMode(_ipsetMode.SelectedIndex == 1 ? "none" : _ipsetMode.SelectedIndex == 2 ? "any" : "loaded");
                MarkDirty();
                ResyncCombos();
            };
            Body.Children.Add(Row(Loc.T("filters.ipset"), Loc.T("filters.ipset.on"), _ipsetMode));

            var updBtn = Ctl.Button(Loc.T("filters.updateLists"), Icons.Refresh, 1);
            updBtn.HorizontalAlignment = HorizontalAlignment.Left;
            updBtn.Margin = new Thickness(0, 10, 0, 0);
            updBtn.Click += (s, e) => UpdateLists();
            Body.Children.Add(updBtn);
        }

        void UpdateLists()
        {
            Core.Info(Loc.T("filters.updatingLists"));
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
              try {
                string err;
                bool ok = Core.UpdateIpsetList(out err);
                Dispatcher.Invoke((Action)delegate
                {
                    if (ok)
                    {
                        Core.Good(string.Format(Loc.T("filters.listsUpdated"), Core.IpsetCount()));
                        MarkDirty();
                    }
                    else Core.Fail(string.Format(Loc.T("filters.listsErr"), err));
                });
              } catch { }
            });
        }

        // ---------- Визуальный редактор списков ----------
        static string[][] ListDefs()
        {
            return new string[][]
            {
                new[] { "ipset-all",        Path.Combine(Core.Lists, "ipset-all.txt") },
                new[] { "ipset-exclude",    Path.Combine(Core.Lists, "ipset-exclude-user.txt") },
                new[] { "list-general",     Path.Combine(Core.Lists, "list-general-user.txt") },
                new[] { "list-exclude",     Path.Combine(Core.Lists, "list-exclude-user.txt") },
            };
        }

        void BuildListEditor()
        {
            Body.Children.Add(SectionLabel(Loc.T("filters.sec.lists")));

            var panel = new StackPanel();

            // Выбор списка
            var selRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            _listSelector = Combo(220);
            foreach (var d in ListDefs())
                _listSelector.Items.Add(d[0]);
            _listSelector.SelectedIndex = 0;
            _listSelector.SelectionChanged += (s, e) => ReloadList();
            selRow.Children.Add(_listSelector);

            _listCount = new TextBlock { Text = "", Foreground = Theme.BrFaint, FontSize = Theme.FsSmall,
                FontFamily = Theme.MonoFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            selRow.Children.Add(_listCount);
            panel.Children.Add(selRow);

            // Список записей
            _listBox = new ListBox
            {
                Height = 180, Background = Theme.BrSurfaceAlt, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), FontFamily = Theme.MonoFont, FontSize = Theme.FsSmall,
                Foreground = Theme.BrText, Padding = new Thickness(4)
            };
            panel.Children.Add(_listBox);

            // Панель добавления/удаления
            var editRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            _entryInput = new TextBox
            {
                Width = 300, Height = 34, Background = Theme.BrSurface, BorderBrush = Theme.BrStroke,
                BorderThickness = new Thickness(1), Foreground = Theme.BrText, CaretBrush = Theme.BrText,
                FontSize = Theme.FsBody, FontFamily = Theme.MonoFont,
                VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 0, 10, 0)
            };
            _entryInput.KeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddEntry(); };
            editRow.Children.Add(_entryInput);

            var addBtn = Ctl.Button(Loc.T("filters.list.add"), Icons.Check, 0);
            addBtn.Margin = new Thickness(8, 0, 0, 0);
            addBtn.Click += (s, e) => AddEntry();
            editRow.Children.Add(addBtn);

            var delBtn = Ctl.Button(Loc.T("filters.list.del"), Icons.Cross, 2);
            delBtn.Margin = new Thickness(8, 0, 0, 0);
            delBtn.Click += (s, e) => DelEntry();
            editRow.Children.Add(delBtn);

            panel.Children.Add(editRow);
            Body.Children.Add(UI.Card(panel, new Thickness(16, 14, 16, 14)));
        }

        string CurrentListPath()
        {
            int idx = _listSelector.SelectedIndex;
            if (idx < 0) idx = 0;
            return ListDefs()[idx][1];
        }

        void ReloadList()
        {
            _listBox.Items.Clear();
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
                        _listBox.Items.Add(string.Format(Loc.T("filters.list.more"), count - MaxDisplay));
                    }
                }
            }
            catch { }
            _listCount.Text = string.Format(Loc.T("filters.list.count"), count);
        }

        void AddEntry()
        {
            string raw = (_entryInput.Text ?? "").Trim();
            if (raw.Length == 0) return;
            string path = CurrentListPath();
            bool isIpSet = path.IndexOf("ipset", StringComparison.OrdinalIgnoreCase) >= 0;
            string val = Core.NormalizeHostEntry(raw, isIpSet);
            if (val.Length == 0) return;
            try
            {
                File.AppendAllText(path, val + Environment.NewLine);
                _entryInput.Text = "";
                ReloadList();
                MarkDirty();
                Core.Info(string.Format(Loc.T("filters.list.added"), val));
            }
            catch (Exception ex) { Core.Fail(ex.Message); }
        }

        void DelEntry()
        {
            if (_listBox.SelectedIndex < 0) return;
            string val = _listBox.SelectedItem as string;
            if (val == null || val.StartsWith("... +")) return;
            string path = CurrentListPath();
            try
            {
                var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
                lines.RemoveAll(l => l.Trim() == val);
                File.WriteAllLines(path, lines.ToArray());
                ReloadList();
                MarkDirty();
                Core.Info(string.Format(Loc.T("filters.list.removed"), val));
            }
            catch (Exception ex) { Core.Fail(ex.Message); }
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
            now.Click += (s, e) => { _win.RestartCurrent(); ClearDirty(); };
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
            ReloadList();
            ClearDirty();
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
