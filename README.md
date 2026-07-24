<div align="center">

<img src="https://raw.githubusercontent.com/krinix1337/lantern-zapret/master/_app/app.ico" width="96" height="96" alt="Lantern"/>

# Lantern

### Обход блокировок Discord и YouTube в один клик

<br/>

![Windows](https://img.shields.io/badge/Windows_10+-0078D6?style=flat-square&logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET_4.0-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![C#](https://img.shields.io/badge/C%23-WPF-239120?style=flat-square&logo=csharp&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)
![Size](https://img.shields.io/badge/Installer-2.2_MB-orange?style=flat-square)

<br/>

[**Скачать**](https://github.com/krinix1337/lantern-zapret/releases/latest) · [Исходный код](https://github.com/krinix1337/lantern-zapret/tree/master/_app) · [Releases](https://github.com/krinix1337/lantern-zapret/releases)

</div>

<br/>

Lantern — лёгкий GUI-менеджер для [zapret](https://github.com/bol-van/zapret) (winws). Запускает обход DPI-блокировок в один клик, проверяет стратегии, обновляет компоненты автоматически. Весь интерфейс написан кодом на C# — без XAML.

---

## Возможности

| | |
|---|---|
| **Запуск в один клик** | Старт/стоп winws с выбором стратегии из 20+ готовых .bat |
| **Проверка стратегий** | curl с 3 протоколами (HTTP/1.1, TLS 1.2, TLS 1.3), реальные цели |
| **Автообновление** | zapret и TG-Proxy скачиваются из GitHub прямо в приложении |
| **Служба Windows** | Установка winws как службы для автозапуска при старте системы |
| **Избранное** | Рабочие стратегии — звёздочкой, всегда сверху списка |
| **Логи** | Просмотр логов winws в реальном времени с цветовой индикацией |

---

## Установка

1. Скачайте **`Lantern-Setup.exe`** из [Releases](https://github.com/krinix1337/lantern-zapret/releases/latest)
2. Запустите установщик
3. При первом запуске приложение скачает zapret автоматически

> **Примечание:** TG-Proxy (ускорение Telegram) можно скачать в разделе «Настройки».

> **Важно:** для работы winws требуются права администратора (WinDivert).

---

## Сборка из исходников

```bat
cd _app
build.cmd
```

На выходе — `zapret.exe` (переименовать в `Lantern.exe`).

Компилятор: `csc.exe` из .NET Framework 4.0 (встроен в Windows, ничего ставить не нужно).

---

## Структура проекта

```
_app/               Исходный код (C#, .NET 4.0, без XAML)
├── Core.*.cs       Ядро: процессы, сеть, конфиг, загрузка, TG-Proxy
├── View.*.cs       Страницы: Обзор, Стратегии, Проверка, Настройки...
├── MainWindow.cs   Главное окно + навигация
├── build.cmd       Сборка через csc.exe
└── app.ico         Иконка приложения

_installer/         Inno Setup 6
└── Lantern.iss     Скрипт установщика
```

---

## Требования

- **Windows 10** или новее
- **.NET Framework 4.0** (встроен в Windows)
- **Права администратора** (для WinDivert)

---

## Благодарности

| Проект | Описание |
|--------|----------|
| [bol-van/zapret](https://github.com/bol-van/zapret) | DPI bypass multiplatform |
| [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) | Стратегии и списки для Discord/YouTube |
| [Flowseal/tg-ws-proxy](https://github.com/Flowseal/tg-ws-proxy) | Ускорение Telegram Desktop |

---

<div align="center">

**MIT License** — [LICENSE.txt](LICENSE.txt)

</div>
