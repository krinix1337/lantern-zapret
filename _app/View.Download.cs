using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZapretStudio
{
    // Окно первичной загрузки: если компоненты zapret не найдены, предлагаем
    // скачать релиз с GitHub, распаковать и настроить. Всё - по подтверждению.
    class DownloadWindow : Window
    {
        Button _btnDownload, _btnPage, _btnChoose, _btnExit;
        TextBlock _status, _stat1, _stat2, _stat3, _stat4;
        Border _progWrap, _progBar;
        volatile bool _busy;
        public bool Succeeded;
        public string ResultRoot;

        public DownloadWindow()
        {
            Title = "zapret";
            Width = 640; Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            Background = Theme.BrBgDeep;
            UseLayoutRounding = true;
            Build();
        }

        void Build()
        {
            var root = new Grid { Margin = new Thickness(28) };
            var sp = new StackPanel();

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            var logo = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9),
                Background = Theme.BrAccent, VerticalAlignment = VerticalAlignment.Center };
            logo.Child = UI.Icon(Icons.Download, 20, Theme.BrOnAccent, 1.8);
            titleRow.Children.Add(logo);
            var t = UI.T(Loc.T("dl.title"), Theme.FsH1, Theme.BrText, FontWeights.SemiBold);
            t.Margin = new Thickness(12, 0, 0, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            titleRow.Children.Add(t);
            sp.Children.Add(titleRow);

            var desc = new TextBlock { Text = Loc.T("dl.desc"), Foreground = Theme.BrMuted,
                FontSize = Theme.FsBody, FontFamily = Theme.UiFont, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 14, 0, 0) };
            sp.Children.Add(desc);

            var wrap = new WrapPanel { Margin = new Thickness(0, 18, 0, 0) };
            _btnDownload = Ctl.Button(Loc.T("dl.download"), Icons.Download, 0);
            _btnDownload.Margin = new Thickness(0, 0, 10, 10);
            _btnDownload.Click += (s, e) => StartDownload();
            _btnPage = Ctl.Button(Loc.T("dl.openPage"), Icons.External, 1);
            _btnPage.Margin = new Thickness(0, 0, 10, 10);
            _btnPage.Click += (s, e) => Core.OpenUrl(Core.ReleaseUrl);
            _btnChoose = Ctl.Button(Loc.T("dl.choose"), Icons.Folder, 3);
            _btnChoose.Margin = new Thickness(0, 0, 10, 10);
            _btnChoose.Click += (s, e) => ChooseFolder();
            _btnExit = Ctl.Button(Loc.T("dl.exit"), null, 3);
            _btnExit.Margin = new Thickness(0, 0, 10, 10);
            _btnExit.Click += (s, e) => { Succeeded = false; Close(); };
            wrap.Children.Add(_btnDownload);
            wrap.Children.Add(_btnPage);
            wrap.Children.Add(_btnChoose);
            wrap.Children.Add(_btnExit);
            sp.Children.Add(wrap);

            // Прогресс-бар
            _progWrap = new Border { Height = 8, CornerRadius = Theme.Rpill, Background = Theme.BrSurfaceAlt,
                Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            _progBar = new Border { CornerRadius = Theme.Rpill, Background = Theme.BrAccent,
                HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
            _progWrap.Child = _progBar;
            _progWrap.Visibility = Visibility.Collapsed;
            sp.Children.Add(_progWrap);

            _status = new TextBlock { Text = "", Foreground = Theme.BrText, FontSize = Theme.FsBody,
                FontFamily = Theme.UiFont, Margin = new Thickness(0, 14, 0, 0), TextWrapping = TextWrapping.Wrap };
            sp.Children.Add(_status);

            var statGrid = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            _stat1 = StatLine();
            _stat2 = StatLine();
            _stat3 = StatLine();
            _stat4 = StatLine();
            statGrid.Children.Add(_stat1);
            statGrid.Children.Add(_stat2);
            statGrid.Children.Add(_stat3);
            statGrid.Children.Add(_stat4);
            sp.Children.Add(statGrid);

            root.Children.Add(sp);
            Content = root;
        }

        static TextBlock StatLine()
        {
            return new TextBlock { Text = "", Foreground = Theme.BrMuted, FontSize = Theme.FsSmall,
                FontFamily = Theme.MonoFont, Margin = new Thickness(0, 2, 0, 0) };
        }

        string TargetDir()
        {
            // Ставим в подпапку рядом с exe.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Path.Combine(baseDir, "zapret");
        }

        void ChooseFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = Loc.T("dl.pickFolder");
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string p = dlg.SelectedPath;
                if (File.Exists(Path.Combine(p, "bin", "winws.exe")))
                {
                    Core.SetRoot(p);
                    Core.RememberRoot(p);
                    Succeeded = true; ResultRoot = p;
                    Close();
                }
                else
                {
                    MessageBox.Show(Loc.T("dl.noWinws"), "zapret",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        void SetButtons(bool enabled)
        {
            _btnDownload.IsEnabled = enabled;
            _btnPage.IsEnabled = enabled;
            _btnChoose.IsEnabled = enabled;
            _btnExit.IsEnabled = enabled;
        }

        void StartDownload()
        {
            if (_busy) return;
            string target = TargetDir();
            var r = MessageBox.Show(
                string.Format(Loc.T("dl.confirm"), Core.ZapretZipUrl, target),
                Loc.T("dl.confirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            _busy = true; SetButtons(false);
            _progWrap.Visibility = Visibility.Visible;
            _status.Text = Loc.T("dl.progress");
            string zip = Path.Combine(Path.GetTempPath(), "zapret_download.zip");

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                bool ok = Core.DownloadFile(Core.ZapretZipUrl, zip, delegate (DlProgress p)
                {
                    Dispatcher.Invoke((Action)delegate { Render(p); });
                }, null);

                if (!ok)
                {
                    Dispatcher.Invoke((Action)delegate
                    {
                        _status.Text = Loc.T("dl.failZip");
                        _busy = false; SetButtons(true);
                    });
                    return;
                }

                Dispatcher.Invoke((Action)delegate { _status.Text = Loc.T("dl.extracting"); });
                string err;
                bool ex = Core.ExtractZapretZip(zip, target, out err);
                try { File.Delete(zip); } catch { }

                Dispatcher.Invoke((Action)delegate
                {
                    _busy = false;
                    if (ex && File.Exists(Path.Combine(target, "bin", "winws.exe")))
                    {
                        Core.SetRoot(target);
                        Core.RememberRoot(target);
                        _status.Text = Loc.T("dl.done");
                        Core.Good(string.Format(Loc.T("dl.installed"), target));
                        Succeeded = true; ResultRoot = target;
                        var mr = MessageBox.Show(Loc.T("dl.done"), "zapret", MessageBoxButton.OK, MessageBoxImage.Information);
                        Close();
                    }
                    else
                    {
                        _status.Text = string.Format(Loc.T("dl.failExtract"), err != null ? ": " + err : "");
                        SetButtons(true);
                    }
                });
            });
        }

        void Render(DlProgress p)
        {
            if (p.Failed) { _status.Text = string.Format(Loc.T("dl.dlErr"), p.Error); return; }
            if (p.Total > 0)
            {
                double frac = (double)p.BytesRead / p.Total;
                if (frac < 0) frac = 0; if (frac > 1) frac = 1;
                double w = _progWrap.ActualWidth * frac;
                _progBar.Width = w > 0 ? w : 0;
            }
            _stat1.Text = Loc.T("dl.downloaded") + ": " + Core.HumanSize(p.BytesRead) +
                (p.Total > 0 ? " / " + Core.HumanSize(p.Total) : "");
            _stat2.Text = Loc.T("dl.speed") + ": " + Core.HumanSpeed(p.SpeedBps);
            _stat3.Text = Loc.T("dl.elapsed") + ": " + FmtTime(p.Elapsed);
            _stat4.Text = Loc.T("dl.when") + ": " + DateTime.Now.ToString("HH:mm:ss");
        }

        static string FmtTime(TimeSpan t)
        {
            if (t.TotalMinutes >= 1) return (int)t.TotalMinutes + " min " + t.Seconds + " s";
            return t.Seconds + "," + (t.Milliseconds / 100) + " s";
        }
    }
}
