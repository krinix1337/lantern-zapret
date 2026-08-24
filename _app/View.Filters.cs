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
        Toggle _doh;
        ComboBox _gameMode, _ipsetMode;
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
            BuildDoh();
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
            };
            Body.Children.Add(Row(Loc.T("filters.game"),
                Loc.T("filters.game.desc"),
                _gameMode));
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
                // Фактическое состояние могло не измениться (например, «Загруженный»
                // без резервной копии) — показываем реальное значение, а не выбранное.
                ResyncCombos();
            };
            Body.Children.Add(Row(Loc.T("filters.ipset"), Loc.T("filters.ipset.on"), _ipsetMode));

            // Кнопка обновления списков с GitHub
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

        void BuildDoh()
        {
            Body.Children.Add(SectionLabel(Loc.T("filters.sec.doh")));
            _doh = new Toggle(Loc.T("filters.doh"));
            _doh.Checked += (s, e) => { if (_syncing) return; Core.DohMode = 1; Core.Info(Loc.T("doh.enabled")); };
            _doh.Unchecked += (s, e) => { if (_syncing) return; Core.DohMode = 0; Core.Info(Loc.T("doh.disabled")); };
            Body.Children.Add(Row(Loc.T("filters.doh"),
                Loc.T("filters.doh.desc"),
                _doh));
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
            try
            {
                if (File.Exists(path))
                {
                    foreach (var line in File.ReadAllLines(path))
                    {
                        string s = line.Trim();
                        if (s.Length == 0 || s.StartsWith("#")) continue;
                        _listBox.Items.Add(s);
                        count++;
                    }
                }
            }
            catch { }
            _listCount.Text = string.Format(Loc.T("filters.list.count"), count);
        }

        void AddEntry()
        {
            string val = (_entryInput.Text ?? "").Trim();
            if (val.Length == 0) return;
            string path = CurrentListPath();
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
            if (val == null) return;
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
                string ipset = Core.IpsetStatus();
                _ipsetMode.SelectedIndex = ipset == "none" ? 1 : ipset == "any" ? 2 : 0;
                // Тумблер DoH выставляем ТОЛЬКО под _syncing: программная установка
                // IsChecked поднимает Checked/Unchecked, а те пишут реестр и дёргают
                // flushdns — побочный эффект при простом открытии страницы.
                _doh.IsChecked = Core.DohMode > 0;
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
