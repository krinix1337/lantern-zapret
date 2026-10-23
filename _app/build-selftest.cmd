@echo off
setlocal
cd /d "%~dp0"
set CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set FW=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319
rem См. build.cmd. WPF-ссылки задаются через %SystemRoot%, а не в refs.rsp.
set WPFREFS=-reference:"%FW%\WPF\PresentationFramework.dll" -reference:"%FW%\WPF\PresentationCore.dll" -reference:"%FW%\WPF\WindowsBase.dll" -reference:"%FW%\System.Xaml.dll"
set SRCS=AssemblyInfo.cs Theme.cs Loc.cs Core.cs Core.Endpoints.cs Core.Strategies.cs Core.Process.cs Core.Net.cs Core.Presets.cs Core.Diag.cs Core.Config.cs Core.Download.cs Core.Fonts.cs Core.TgProxy.cs Core.Watchdog.cs Core.Doh.cs Core.Traffic.cs Core.Lists.cs View.Common.cs View.TrayWidget.cs View.Overview.cs View.Strategies.cs View.Check.cs View.Service.cs View.Filters.cs View.Log.cs View.Settings.cs View.Updates.cs View.About.cs View.Download.cs MainWindow.cs App.cs
"%CSC%" -nologo -codepage:65001 -define:SELFTEST -target:exe -out:"selftest.exe" -win32manifest:selftest.manifest %WPFREFS% @refs.rsp %SRCS% SelfTest.cs
if errorlevel 1 ( echo BUILD FAILED & exit /b 1 )
rem CI запускает ..\selftest.exe, при неудачной копии тестировался бы
rem прошлый билд, а шаг остался зелёным.
copy /y "selftest.exe" "..\selftest.exe"
if errorlevel 1 ( echo COPY FAILED: "..\selftest.exe" locked or inaccessible & exit /b 1 )
echo BUILD OK: selftest.exe updated
endlocal
