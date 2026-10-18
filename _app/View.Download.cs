using System;
using System.IO;
using System.Runtime.InteropServices;
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
        [DllImport("dwmapi.dll", PreserveSig = true)]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        Button _btnDownload, _btnPage, _btnChoose, _btnExit;
        TextBlock _status, _stat1, _stat2, _stat3, _stat4;
        Border _progWrap, _progBar;
        volatile bool _busy;
        volatile bool _cancelled;
        public bool Succeeded;
        public string ResultRoot;

        public DownloadWindow()
        {
            Title = Core.AppName;
            Width = 640; Height = 460;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            Background = Theme.BrBgDeep;
            BorderBrush = Theme.BrStroke;
            BorderThickness = new Thickness(1);
            UseLayoutRounding = true;

            Closing += (s, e) =>
            {
                if (_busy)
                {
                    var res = MessageBox.Show(Loc.T("dl.cancelConfirm"), Core.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes)
                    {
                        e.Cancel = true;
                        return;
                    }
                    _cancelled = true;
                }
            };

            var chrome = new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 48, ResizeBorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0), GlassFrameThickness = new Thickness(0)
            };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);

            SourceInitialized += (s, e) =>
            {
                try
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (hwnd == IntPtr.Zero) return;
                    int v = 2; // DWMWCP_ROUND
                    DwmSetWindowAttribute(hwnd, 33, ref v, sizeof(int));
                }
                catch { }
            };

            Build();
        }

        void Build()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var topbar = new Grid { Background = Brushes.Transparent };
            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
            var topLogo = new Border { Width = 22, Height = 22, CornerRadius = Theme.R6, Background = Theme.BrAccent, VerticalAlignment = VerticalAlignment.Center };
            topLogo.Child = UI.Icon(Icons.Lantern, 14, Theme.BrOnAccent, 1.8);
            brand.Children.Add(topLogo);
            brand.Children.Add(new TextBlock { Text = Core.AppName, Foreground = Theme.BrText, FontSize = Theme.FsBody, FontWeight = FontWeights.Bold, FontFamily = Theme.UiFont, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(9, 0, 0, 0) });
            topbar.Children.Add(brand);

            var closeBtn = new Button { Cursor = System.Windows.Input.Cursors.Hand, Width = 44, Height = 48, HorizontalAlignment = HorizontalAlignment.Right };
            Ctl.StripChrome(closeBtn);
            var closeBd = new Border { Background = Brushes.Transparent };
            var closeIc = UI.Icon(Icons.Cross, 14, Theme.BrMuted, 1.6);
            closeBd.Child = closeIc;
            closeBtn.Content = closeBd;
            closeBtn.MouseEnter += (s, e) => { closeBd.Background = Theme.Frozen(Theme.Err); if (closeIc.Stroke != null) closeIc.Stroke = Brushes.White; if (closeIc.Fill != null && closeIc.Fill != Brushes.Transparent) closeIc.Fill = Brushes.White; };
            closeBtn.MouseLeave += (s, e) => { closeBd.Background = Brushes.Transparent; if (closeIc.Stroke != null) closeIc.Stroke = Theme.BrMuted; if (closeIc.Fill != null && closeIc.Fill != Brushes.Transparent) closeIc.Fill = Theme.BrMuted; };
            closeBtn.Click += (s, e) =>
            {
                if (_busy)
                {
                    var res = MessageBox.Show(Loc.T("dl.cancelConfirm"), Core.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                    _cancelled = true;
                }
                Succeeded = false;
                Close();
            };
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(closeBtn, true);
            topbar.Children.Add(closeBtn);
            
            Grid.SetRow(topbar, 0);
            root.Children.Add(topbar);

            var contentGrid = new Grid { Margin = new Thickness(28, 0, 28, 28) };
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

            _btnDownload = Ctl.Button(Loc.T("dl.download"), Icons.Download, 0);
            _btnDownload.Click += (s, e) => StartDownload();
            _btnPage = Ctl.Button(Loc.T("dl.openPage"), Icons.External, 1);
            _btnPage.Click += (s, e) => Core.OpenUrl(Core.ReleaseUrl);
            _btnChoose = Ctl.Button(Loc.T("dl.choose"), Icons.Folder, 3);
            _btnChoose.Click += (s, e) => ChooseFolder();
            _btnExit = Ctl.Button(Loc.T("dl.exit"), null, 3);
            _btnExit.Click += (s, e) =>
            {
                if (_busy)
                {
                    var res = MessageBox.Show(Loc.T("dl.cancelConfirm"), Core.AppName, MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) return;
                    _cancelled = true;
                }
                Succeeded = false;
                Close();
            };
            // Один ряд равных кнопок вместо WrapPanel: раньше «Скачать», «Страница
            // релизов», «Указать папку» и «Выход» переносились и имели свою ширину.
            var btnRow = Ctl.ButtonRow(_btnDownload, _btnPage, _btnChoose, _btnExit);
            btnRow.Margin = new Thickness(0, 18, 0, 0);
            sp.Children.Add(btnRow);

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

            contentGrid.Children.Add(sp);
            Grid.SetRow(contentGrid, 1);
            root.Children.Add(contentGrid);
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
                    MessageBox.Show(Loc.T("dl.noWinws"), Core.AppName,
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
                string.Format(Loc.T("dl.confirm"), Endpoints.ZapretReleaseUrl, target),
                Loc.T("dl.confirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            _busy = true;
            _cancelled = false;
            SetButtons(false);
            _progWrap.Visibility = Visibility.Visible;
            _status.Text = Loc.T("dl.progress");
            string tmpDir = Path.Combine(Path.GetTempPath(), "lantern_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            string zip = Path.Combine(tmpDir, "zapret_download.zip");

            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // №33: имя архива и его SHA-256 приходят из GitHub API; без
                    // контрольной суммы установка отменяется.
                    string url, zipSha;
                    if (!Core.ZapretZipAssets(out url, out zipSha))
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                _status.Text = Loc.T("dl.hashMissing");
                                _busy = false; SetButtons(true);
                            });
                        }
                        catch { }
                        Core.Fail(Loc.T("dl.hashMissing"));
                        return;
                    }
                    if (_cancelled) return;
                    Core.Info(string.Format(Loc.T("upd.zipSource"), url));
                    string dlErr;
                    bool ok = Core.DownloadVerified(url, zipSha, zip, delegate (DlProgress p)
                    {
                        // Окно могло закрыться: Invoke в никуда бросит исключение.
                        try { Dispatcher.Invoke((Action)delegate { Render(p); }); } catch { }
                    }, delegate { return _cancelled; }, out dlErr);

                    if (_cancelled)
                    {
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                _status.Text = Loc.T("dl.cancelled");
                                _busy = false; SetButtons(true);
                            });
                        }
                        catch { }
                        return;
                    }

                    if (!ok)
                    {
                        string failMsg = string.IsNullOrEmpty(dlErr) ? Loc.T("dl.failZip") : dlErr;
                        try
                        {
                            Dispatcher.Invoke((Action)delegate
                            {
                                _status.Text = failMsg;
                                _busy = false; SetButtons(true);
                            });
                        }
                        catch { }
                        Core.Fail(failMsg);
                        return;
                    }

                    try { Dispatcher.Invoke((Action)delegate { _status.Text = Loc.T("dl.extracting"); }); } catch { }
                    string err;
                    bool ex = Core.ExtractZapretZip(zip, target, out err);

                    try
                    {
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
                                var mr = MessageBox.Show(Loc.T("dl.done"), Core.AppName, MessageBoxButton.OK, MessageBoxImage.Information);
                                Close();
                            }
                            else
                            {
                                _status.Text = string.Format(Loc.T("dl.failExtract"), err != null ? ": " + err : "");
                                SetButtons(true);
                            }
                        });
                    }
                    catch { }
                }
                catch { }
                finally
                {
                    Core.DeleteDirSafe(tmpDir);
                }
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
            _stat3.Text = Loc.T("dl.elapsed") + ": " + Core.FmtTime(p.Elapsed);
            _stat4.Text = Loc.T("dl.when") + ": " + DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
