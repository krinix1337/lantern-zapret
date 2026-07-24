# Lantern

WPF-приложение (C#, .NET 4.0) для управления обходом блокировок Discord и YouTube через zapret (winws).

## Возможности

- Запуск/остановка winws с выбором стратегии
- Проверка стратегий (curl, 3 протокола: HTTP/1.1, TLS 1.2, TLS 1.3)
- Автообновление zapret и TG-Proxy из GitHub
- Установка winws как службы Windows
- Избранное для стратегий
- Логи в реальном времени
- Русский интерфейс

## Сборка

```
cd _app
build.cmd
```

Результат: `zapret.exe` (переименовать в `Lantern.exe`).

## Установщик

Inno Setup 6 скрипт: `_installer/Lantern.iss`

Установщик содержит только Lantern.exe — zapret и TG-Proxy скачиваются внутри приложения.

## Требования

- Windows 10+
- .NET Framework 4.0
- Права администратора (для winws/WinDivert)

## Лицензия

MIT — см. [LICENSE.txt](LICENSE.txt)
