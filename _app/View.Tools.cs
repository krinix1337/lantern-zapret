using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZapretStudio
{
    // Инструменты: внешний Telegram-прокси (Flowseal/tg-ws-proxy).
    class ToolsPage : Page
    {
        public override string Title { get { return Loc.T("tools.title"); } }
        public override string Subtitle { get { return Loc.T("tools.sub"); } }

        readonly MainWindow _win;
        Border _statusPill;
        Button _btnDownload, _btnFolder;
        Toggle _tgToggle;
        TextBlock _detail;
        volatile bool _busy;
        bool _syncing;

        public ToolsPage(MainWindow win)
        {
            _win = win;
            BuildTg();
        }

        public override void OnShow() { Sync(); }

        void BuildTg()
        {
            Body.Children.Add(SectionLabel("Telegram"));

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(UI.Icon(Icons.Telegram, 20, Theme.BrAccent, 1.8));
            var tt = UI.T(Loc.T("tg.title"), Theme.FsH2, Theme.BrText, FontWeights.SemiBold);
            tt.Margin = new Thickness(10, 0, 0, 0);
            tt.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(tt);
            left.Children.Add(titleRow);
            var desc = new TextBlock { Text = Loc.T("tg.desc"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsSmall, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0) };
            left.Children.Add(desc);
            Grid.SetColumn(left, 0); head.Children.Add(left);

            _statusPill = Pill.Make(Sev.Neutral, Loc.T("tg.notInstalled"));
            _statusPill.VerticalAlignment = VerticalAlignment.Top;
            _statusPill.Margin = new Thickness(14, 2, 0, 0);
            Grid.SetColumn(_statusPill, 1); head.Children.Add(_statusPill);

            Body.Children.Add(UI.Card(head, new Thickness(18, 16, 18, 16)));
            Body.Children.Add(new Border { Height = 12 });

            // Переключатель включения прокси — работает так же, как основной обход.
            _tgToggle = new Toggle(Loc.T("tg.enable"));
            _tgToggle.Checked += (s, e) => { if (!_syncing) StartProxy(); };
            _tgToggle.Unchecked += (s, e) => { if (!_syncing) StopProxy(); };
            Body.Children.Add(Row(Loc.T("tg.enable"), Loc.T("tg.enabledDesc"), _tgToggle));
            Body.Children.Add(new Border { Height = 6 });

            var wrap = new WrapPanel();
            _btnDownload = Ctl.Button(Loc.T("tg.download"), Icons.Download, 0);
            _btnDownload.Margin = new Thickness(0, 0, 10, 10);
            _btnDownload.Click += (s, e) => DownloadProxy();
            _btnFolder = Ctl.Button(Loc.T("common.open"), Icons.Folder, 3);
            _btnFolder.Margin = new Thickness(0, 0, 10, 10);
            _btnFolder.Click += (s, e) => { try { System.IO.Directory.CreateDirectory(Core.TgToolsDir); Core.OpenFolder(Core.TgToolsDir); } catch { } };
            var repo = Ctl.Button("GitHub", Icons.Github, 1);
            repo.Margin = new Thickness(0, 0, 10, 10);
            repo.Click += (s, e) => Core.OpenUrl(Core.TgProxyRepo);
            wrap.Children.Add(_btnDownload);
            wrap.Children.Add(_btnFolder);
            wrap.Children.Add(repo);
            Body.Children.Add(wrap);

            _detail = new TextBlock { Text = "", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 6, 0, 0) };
            Body.Children.Add(_detail);

            var noteCard = NoteCard(Icons.Info, Theme.BrAccent, Loc.T("tg.note"), Sev.Info);
            noteCard.Margin = new Thickness(0, 14, 0, 0);
            Body.Children.Add(noteCard);
        }

        void SetStatus(Sev sev, string text)
        {
            var np = Pill.Make(sev, text);
            np.VerticalAlignment = VerticalAlignment.Top;
            np.Margin = new Thickness(14, 2, 0, 0);
            var g = _statusPill.Parent as Grid;
            if (g != null)
            {
                int idx = g.Children.IndexOf(_statusPill);
                if (idx >= 0)
                {
                    Grid.SetColumn(np, 1);
                    g.Children.RemoveAt(idx);
                    g.Children.Insert(idx, np);
                    _statusPill = np;
                }
            }
        }

        void Sync()
        {
            bool installed = Core.TgProxyInstalled();
            bool running = Core.TgProxyRunning();
            if (running) SetStatus(Sev.Ok, Loc.T("common.running"));
            else if (installed) SetStatus(Sev.Neutral, Loc.T("tg.installed"));
            else SetStatus(Sev.Warn, Loc.T("tg.notInstalled"));
            _btnDownload.IsEnabled = !installed && !_busy;
            // Синхронизируем переключатель, не вызывая обработчики.
            _syncing = true;
            _tgToggle.IsChecked = running;
            _tgToggle.IsEnabled = installed && !_busy;
            _syncing = false;
        }

        void DownloadProxy()
        {
            if (_busy) return;
            var r = MessageBox.Show(
                string.Format(Loc.T("tg.dlDlg"), Core.TgToolsDir),
                Loc.T("tg.dlTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            _busy = true; Sync();
            _detail.Text = Loc.T("tg.dlProgress");
            string url = Core.TgProxyDownloadUrl();
            string dest = Core.TgProxyExe;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { System.IO.Directory.CreateDirectory(Core.TgToolsDir); } catch { }
                bool ok = Core.DownloadFile(url, dest, delegate (DlProgress p)
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        if (p.Failed) return;
                        string size = Core.HumanSize(p.BytesRead) + (p.Total > 0 ? " / " + Core.HumanSize(p.Total) : "");
                        _detail.Text = size + "      " + Core.HumanSpeed(p.SpeedBps) + "      " + FmtTime(p.Elapsed);
                    });
                }, null);
                Dispatcher.Invoke((Action)delegate
                {
                    _busy = false;
                    if (ok)
                    {
                        _detail.Text = string.Format(Loc.T("tg.dlDone"), dest, DateTime.Now.ToString("HH:mm:ss"));
                        Core.Good(Loc.T("tg.dlOk"));
                    }
                    else { _detail.Text = Loc.T("tg.dlFailMsg"); Core.Fail(Loc.T("tg.dlFail")); }
                    Sync();
                });
            });
        }

        void StartProxy()
        {
            if (!Core.TgProxyInstalled())
            {
                Core.Warn(Loc.T("tg.needDownload"));
                _detail.Text = Loc.T("tg.needDownload");
                Sync();
                return;
            }
            string err;
            if (Core.TgProxyStart(out err)) { Core.Good(Loc.T("tg.startedOk")); _detail.Text = Loc.T("tg.startedDetail"); }
            else { Core.Fail(string.Format(Loc.T("tg.startErr"), err)); _detail.Text = string.Format(Loc.T("tg.startErrDetail"), err); }
            Sync();
        }

        void StopProxy()
        {
            Core.TgProxyStop();
            Core.Info(Loc.T("tg.stoppedOk"));
            _detail.Text = "";
            Sync();
        }

        static string FmtTime(TimeSpan t)
        {
            if (t.TotalMinutes >= 1) return (int)t.TotalMinutes + " min " + t.Seconds + " s";
            return t.Seconds + "," + (t.Milliseconds / 100) + " s";
        }
    }
}
