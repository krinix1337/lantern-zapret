@echo off
setlocal
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set SRCS=AssemblyInfo.cs Theme.cs Loc.cs Core.cs Core.Endpoints.cs Core.Strategies.cs Core.Process.cs Core.Net.cs Core.Presets.cs Core.Diag.cs Core.Config.cs Core.Download.cs Core.Fonts.cs Core.TgProxy.cs Core.Watchdog.cs Core.Doh.cs Core.Traffic.cs Core.Profiles.cs Core.Lists.cs View.Common.cs View.TrayWidget.cs View.MusicPlayer.cs View.Overview.cs View.Strategies.cs View.Check.cs View.Service.cs View.Filters.cs View.Log.cs View.Settings.cs View.About.cs View.Download.cs MainWindow.cs App.cs
rem -codepage:65001 обязателен: исходники в UTF-8 без BOM, без флага csc
rem читает их в системной ANSI-кодировке и портит кириллицу в строках.
"%CSC%" -nologo -codepage:65001 -target:winexe -out:"zapret.exe" -win32manifest:app.manifest -resource:assets\peter-griffin.png,ZapretStudio.Assets.PeterGriffin @refs.rsp %SRCS%
if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )
echo BUILD OK: zapret.exe
endlocal
