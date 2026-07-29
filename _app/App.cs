using System;
using System.Windows;
using System.Windows.Threading;

namespace ZapretStudio
{
    class App : Application
    {
        [STAThread]
        static void Main()
        {
#if SELFTEST
            SelfTest.Run();
            return;
#pragma warning disable 0162
#endif
            var app = new App();
            Theme.InstallScrollBarStyle();
            app.DispatcherUnhandledException += (s, e) =>
            {
                try { Core.Fail(string.Format(Loc.T("app.errToast"), e.Exception.Message)); } catch { }
                MessageBox.Show(string.Format(Loc.T("app.errDlg"), e.Exception.Message), "zapret",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
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
            // После загрузки конфига из папки zapret — повторно применяем сохранённые язык и тему.
            Loc.Load();
            ApplySavedTheme();
            // Первый запуск скачивает проверенные шрифты в utils\\fonts. При офлайне
            // приложение запускается с системным fallback и повторит попытку позже.
            Core.EnsureUiFonts();
            Core.Info(string.Format(Loc.T("app.startedLog"), Core.Root));
            Core.Info(Core.IsAdmin() ? Loc.T("app.adminYes") : Loc.T("app.adminNo"));

            var win = new MainWindow();
            app.Run(win);
        }

        static void ApplySavedTheme()
        {
            string t = Core.Get("theme", "dark");
            Theme.Apply(t == "light" ? ThemeMode.Light : t == "amoled" ? ThemeMode.Amoled : ThemeMode.Dark);
        }
    }
}
