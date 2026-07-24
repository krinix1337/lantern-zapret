<div align="center">

# Lantern

<img src="https://img.shields.io/badge/Windows-10%2B-0078D6?style=for-the-badge&logo=windows" alt="Windows 10+"/>
<img src="https://img.shields.io/badge/.NET-4.0-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 4.0"/>
<img src="https://img.shields.io/badge/Language-C%23-239120?style=for-the-badge&logo=csharp" alt="C#"/>
<img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge" alt="MIT"/>

**WPF-приложение для управления обходом блокировок Discord и YouTube через zapret (winws)**

[Скачать установщик](https://github.com/krinix1337/lantern-zapret/releases) · [Исходный код](https://github.com/krinix1337/lantern-zapret/tree/master/_app)

</div>

---

## Что это

Lantern — лёгкий GUI-менеджер для [zapret](https://github.com/bol-van/zapret) (winws). Запускает обход блокировок в один клик, проверяет стратегии, обновляет компоненты автоматически. Без XAML — весь интерфейс написан кодом на C#.

## Возможности

**Обход блокировок** — запуск/остановка winws с выбором стратегии из 20+ готовых .bat-файлов. Установка как службы Windows для автозапуска.

**Проверка стратегий** — тестирование через curl с 3 протоколами (HTTP/1.1, TLS 1.2, TLS 1.3) и реальными целями из targets.txt. Полная аналогия с `test zapret.ps1`.

**Автообновление** — zapret и TG-Proxy скачиваются и обновляются прямо из приложения (GitHub Releases).

**Избранное** — отмечайте рабочие стратегии звёздочкой, они всегда сверху.

**Логи** — просмотр логов winws в реальном времени с цветовой индикацией.

## Установка

Скачайте `Lantern-Setup.exe` из [Releases](https://github.com/krinix1337/lantern-zapret/releases) и запустите.

При первом запуске приложение предложит скачать zapret автоматически. TG-Proxy (для ускорения Telegram) можно скачать в настройках.

> **Важно:** для работы winws требуются права администратора.

## Сборка из исходников

```bat
cd _app
build.cmd
```

На выходе: `zapret.exe` → переименовать в `Lantern.exe`.

Компилятор: csc.exe из .NET Framework 4.0 (входит в Windows).

## Структура

```
_app/           Исходный код (C#, .NET 4.0, без XAML)
  Core.*.cs     Ядро: процессы, сеть, конфиг, загрузка, TG-Proxy
  View.*.cs     Страницы: Обзор, Стратегии, Проверка, Настройки...
  MainWindow.cs Главное окно + навигация
  build.cmd     Сборка через csc.exe
_installer/     Inno Setup 6 скрипт
```

## Требования

- Windows 10 или новее
- .NET Framework 4.0 (встроен в Windows)
- Права администратора (WinDivert)

## Благодарности

- [bol-van/zapret](https://github.com/bol-van/zapret) — DPI bypass multiplatform
- [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) — стратегии и списки
- [Flowseal/tg-ws-proxy](https://github.com/Flowseal/tg-ws-proxy) — ускорение Telegram

---

<div align="center">

**MIT** — [LICENSE.txt](LICENSE.txt)

</div>
