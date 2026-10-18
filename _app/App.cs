using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ZapretStudio
{
    class App : Application
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmNup;
            public int dmDisplayFrequency;
        }

        [DllImport("user32.dll")]
        static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int RegisterWindowMessage(string message);

        [DllImport("user32.dll")]
        static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        internal const string ShowWindowMessageName = "LANTERN_RESTORE_WINDOW_MSG";
        internal static readonly int WmShowWindow = RegisterWindowMessage(ShowWindowMessageName);

        static void OptimizeRendering()
        {
            try
            {
                // Включаем прямое аппаратное ускорение WPF через DirectX
                RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
                TextOptions.TextFormattingModeProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata(TextFormattingMode.Display));
                RenderOptions.ClearTypeHintProperty.OverrideMetadata(typeof(Window), new FrameworkPropertyMetadata(ClearTypeHint.Enabled));
            }
            catch { }
        }

        // №14: ServicePointManager.SecurityProtocol — статика на весь процесс.
        // Раньше её писали из дюжины мест в четырёх разных комбинациях из разных
        // потоков, поэтому протокол запроса зависел от того, чья запись была
        // последней. Настраиваем один раз до любых обращений к сети.
        static void ConfigureTls()
        {
            try
            {
                // 12288 == Tls13; флага нет в .NET 4.5/4.6, но значение валидно.
                System.Net.ServicePointManager.SecurityProtocol =
                    System.Net.SecurityProtocolType.Tls12 | (System.Net.SecurityProtocolType)12288;
            }
            catch
            {
                try { System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12; }
                catch { }
            }
        }

        internal static System.Threading.Mutex AppMutex;

        // Ставится перед Application.Current.Shutdown(): окно не должно в этот момент
        // отменять закрытие ради сворачивания в трей (№61).
        internal static bool ForceShutdown;

        internal static void ReleaseMutexForRestart()
        {
            try
            {
                if (AppMutex != null)
                {
                    AppMutex.ReleaseMutex();
                    AppMutex.Dispose();
                    AppMutex = null;
                }
            }
            catch { }
        }

        [STAThread]
        static void Main()
        {
            OptimizeRendering();
            ConfigureTls();
            try { System.Threading.ThreadPool.SetMinThreads(32, 32); } catch { }
#if SELFTEST
            SelfTest.Run();
            return;
#pragma warning disable 0162
#endif
            bool createdNew = false;
            System.Threading.Mutex mutex = null;
            // №62: перезапущенный с UAC экземпляр приходит раньше, чем прежний отпустит
            // мьютекс (диалог UAC может висеть долго). Без --tookover второй процесс
            // «огонял и забывал» сообщение первому окну, которое ещё даже не создано,
            // и выходил. С флагом — ждём освобождения мьютекса с разумным тайм-аутом.
            bool tookOver = false;
            foreach (var a in Environment.GetCommandLineArgs())
                if (string.Equals(a, "--tookover", StringComparison.OrdinalIgnoreCase)) tookOver = true;
            int takeoverWaitMs = tookOver ? 20000 : 1500;
            try
            {
                mutex = new System.Threading.Mutex(true, "Lantern_SingleInstance_Mutex", out createdNew);
                if (!createdNew)
                {
                    try
                    {
                        if (mutex.WaitOne(takeoverWaitMs, false))
                            createdNew = true;
                    }
                    catch (System.Threading.AbandonedMutexException)
                    {
                        createdNew = true;
                    }
                }
            }
            catch (System.Threading.AbandonedMutexException)
            {
                createdNew = true;
            }
            catch (Exception ex)
            {
                try { WriteCrashLog("Mutex error: " + ex); } catch { }
                createdNew = true;
            }

            if (!createdNew)
            {
                // Сообщаем первому экземпляру развернуться и выйти на передний план
                PostMessage(HWND_BROADCAST, WmShowWindow, IntPtr.Zero, IntPtr.Zero);
                if (mutex != null) { try { mutex.Dispose(); } catch { } }
                return;
            }

            AppMutex = mutex;
            try
            {
                var app = new App();
                Theme.InstallScrollBarStyle();
                int exCount = 0;
                DateTime lastExAt = DateTime.MinValue;
                string lastExMsg = null;

                app.DispatcherUnhandledException += (s, e) =>
                {
                    try { Core.Fail(string.Format(Loc.T("app.errToast"), e.Exception.Message)); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Fail in DispatcherUnhandledException: " + ex); }

                    DateTime now = DateTime.UtcNow;
                    string msg = e.Exception != null ? e.Exception.Message : "";
                    if ((now - lastExAt).TotalSeconds < 10 && msg == lastExMsg) exCount++;
                    else { exCount = 1; lastExAt = now; lastExMsg = msg; }

                    if (exCount >= 3)
                    {
                        var res = MessageBox.Show(
                            string.Format(Loc.T("app.errDlg"), msg) + "\n\n" + Loc.T("app.crashLoopDlg"),
                            Core.AppName,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Error);
                        if (res == MessageBoxResult.Yes) Environment.Exit(1);
                        exCount = 0;
                    }
                    else
                    {
                        MessageBox.Show(string.Format(Loc.T("app.errDlg"), msg), Core.AppName,
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    e.Handled = true;
                };

                // Исключение в потоке пула (пробы, бенчмарк, watchdog, загрузки) не
                // проходит через DispatcherUnhandledException: раньше такое падение
                // закрывало приложение молча, без записи в журнал. Перехватить и
                // продолжить работу здесь нельзя — среда всё равно завершает процесс, —
                // но причину сохраняем в файл и показываем пользователю.
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    string path = null;
                    var ex = e.ExceptionObject as Exception;
                    string text = ex != null ? ex.ToString() : Convert.ToString(e.ExceptionObject);
                    try { path = WriteCrashLog(text); } catch { }
                    try { Core.Fail("FATAL: " + text); } catch { }
                    try
                    {
                        MessageBox.Show(string.Format(Loc.T("app.crashDlg"),
                            ex != null ? ex.Message : text, path ?? "-"),
                            Core.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    catch { }
                };

                // Язык и тема читаются из локального конфига рядом с exe ещё до поиска корня.
                Loc.Load();
                ApplySavedTheme();

                if (!Core.LocateRoot())
                {
                    // Компоненты не найдены — предлагаем скачать и настроить (по подтверждению).
                    var dl = new DownloadWindow();
                    dl.ShowDialog();
                    if (!dl.Succeeded || !Core.LocateRoot())
                        return;
                }
                Core.LoadConfig();
                // Подключаем кэш шрифтов Google Sans до применения темы, чтобы семейство подхватилось сразу.
                Core.ConfigureUiFontsFromCache();
                // После загрузки конфига из папки zapret — повторно применяем сохранённые язык и тему.
                Loc.Load();
                ApplySavedTheme();
                // Незнакомый ui_font (правка руками или убранное семейство) заменяем
                // разово и записываем — без тихой подстановки при каждом запуске.
                Core.NormalizeSavedUiFont();
                // То же для акцентного цвета.
                Theme.NormalizeSavedAccent();
                Core.Info(string.Format(Loc.T("app.startedLog"), Core.Root));
                Core.Info(Core.IsAdmin() ? Loc.T("app.adminYes") : Loc.T("app.adminNo"));

                // Обычный запуск: главное окно открывается сразу, без заставки и
                // каких-либо стартовых переходов.
                var win = new MainWindow();
                win.Loaded += (s, e) => Core.EnsureUiFontsInBackground();
                app.Run(win);
            }
            finally
            {
                ReleaseMutexForRestart();
            }
        }

        // Сохранить причину падения рядом с профилем пользователя: папка установки
        // может быть недоступна для записи, поэтому LocalAppData — основной вариант,
        // а %TEMP% — резервный. Возвращает путь к файлу или null.
        static string WriteCrashLog(string text)
        {
            string[] roots =
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Core.AppName),
                System.IO.Path.GetTempPath()
            };
            string stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            foreach (var root in roots)
            {
                try
                {
                    if (string.IsNullOrEmpty(root)) continue;
                    if (!System.IO.Directory.Exists(root)) System.IO.Directory.CreateDirectory(root);
                    string file = System.IO.Path.Combine(root, "crash-" + stamp + ".log");
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine(Core.AppName + " " + Core.AppVersion);
                    sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine("OS: " + Environment.OSVersion + " / CLR " + Environment.Version);
                    sb.AppendLine("Base: " + AppDomain.CurrentDomain.BaseDirectory);
                    sb.AppendLine();
                    sb.AppendLine(text);
                    System.IO.File.WriteAllText(file, sb.ToString(), System.Text.Encoding.UTF8);
                    return file;
                }
                catch { }
            }
            return null;
        }

        // internal: сквозной тест миграции конфига живёт в SelfTest (ThemeMigration).
        internal static void ApplySavedTheme()
        {
            string t = Core.Get("theme", null);
            if (string.IsNullOrEmpty(t))
            {
                // Первый запуск: подстраиваемся под тему Windows
                Theme.Apply(IsWindowsLightTheme() ? ThemeMode.Light : ThemeMode.Dark);
                return;
            }
            ThemeMode mode;
            if (Theme.TryParse(t, out mode))
            {
                Theme.Apply(mode);
                return;
            }

            // Конфиг приехал из старой сборки: темы «Северное сияние», «Питер Грифин»
            // и «Полночная» из неё удалены, а значение theme = осталось. Молча
            // откатываться на Dark на каждом запуске нельзя — пользователь терял бы
            // выбор и не понимал почему, а любое сохранение конфига переносило бы
            // мёртвое имя в новый gui-config.ini. Поэтому выбранную замену сразу
            // пишем в конфиг: тема сохраняется ровно так же, как при выборе вручную.
            bool removed = Theme.IsRemovedTheme(t);
            ThemeMode fallback = removed
                ? Theme.RemovedThemeFallback(t)
                : (IsWindowsLightTheme() ? ThemeMode.Light : ThemeMode.Dark);
            Theme.Apply(fallback);
            Core.Set("theme", Theme.Name(fallback));
            // Пишем только когда корень zapret уже определён: до LocateRoot ConfigFile
            // указывает на папку с Lantern.exe, и создание gui-config.ini рядом с ним
            // оставило бы осиротевший конфиг в Program Files.
            if (!string.IsNullOrEmpty(Core.Root))
            {
                try { Core.SaveConfig(); } catch { }
            }
            try
            {
                Core.Warn(string.Format(
                    Loc.T(removed ? "theme.migrated" : "theme.unknownValue"),
                    t.Trim(), Theme.Label(fallback)));
            }
            catch { }
        }

        static bool IsWindowsLightTheme()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int) return ((int)val) == 1;
                    }
                }
            }
            catch { }
            return false; // fallback — тёмная тема
        }
    }
}
