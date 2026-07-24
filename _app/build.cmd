@echo off
setlocal
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set SRCS=Theme.cs Loc.cs Core.cs Core.Strategies.cs Core.Process.cs Core.Net.cs Core.Presets.cs Core.Diag.cs Core.Config.cs Core.Download.cs Core.TgProxy.cs Core.Watchdog.cs Core.Doh.cs Core.Traffic.cs Core.Profiles.cs Core.Lists.cs View.Common.cs View.Overview.cs View.Strategies.cs View.Check.cs View.Service.cs View.Filters.cs View.Log.cs View.Settings.cs View.About.cs View.Download.cs MainWindow.cs App.cs
"%CSC%" -nologo -target:winexe -out:"zapret.exe" -win32manifest:app.manifest @refs.rsp %SRCS%
if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )
echo BUILD OK: zapret.exe
endlocal
