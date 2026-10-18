@echo off
setlocal
cd /d "%~dp0"
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set FW=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319
rem №86: WPF-сборки лежат в подпапке WPF, поэтому путь к ним абсолютный.
rem В .rsp переменные окружения не раскрываются — эти ссылки задаются здесь,
rem а не в refs.rsp, иначе сборка молча зависит от буквы диска системы.
set WPFREFS=-reference:"%FW%\WPF\PresentationFramework.dll" -reference:"%FW%\WPF\PresentationCore.dll" -reference:"%FW%\WPF\WindowsBase.dll" -reference:"%FW%\System.Xaml.dll"
set SRCS=AssemblyInfo.cs Theme.cs Loc.cs Core.cs Core.Endpoints.cs Core.Strategies.cs Core.Process.cs Core.Net.cs Core.Presets.cs Core.Diag.cs Core.Config.cs Core.Download.cs Core.Fonts.cs Core.TgProxy.cs Core.Watchdog.cs Core.Doh.cs Core.Traffic.cs Core.Lists.cs View.Common.cs View.TrayWidget.cs View.Overview.cs View.Strategies.cs View.Check.cs View.Service.cs View.Filters.cs View.Log.cs View.Settings.cs View.Updates.cs View.About.cs View.Download.cs MainWindow.cs App.cs
rem -codepage:65001 обязателен: исходники в UTF-8 без BOM, без флага csc
rem читает их в системной ANSI-кодировке и портит кириллицу в строках.
"%CSC%" -nologo -codepage:65001 -target:winexe -out:"Lantern.exe" -win32manifest:app.manifest %WPFREFS% @refs.rsp %SRCS%
if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )
rem №85: Lantern.iss берёт исходник из корня (SrcDir = ..). Если копия не
rem прошла (файл запущен/залочен), установщик упакует предыдущую версию.
copy /y "Lantern.exe" "..\Lantern.exe"
if errorlevel 1 ( echo COPY FAILED: "..\Lantern.exe" locked or inaccessible & exit /b 1 )
echo BUILD OK: Lantern.exe updated
endlocal
