<div align="center">

![MorseCore](docs/media/morsecore.gif)

**Напиши сообщение. Послушай Морзе. Учись на практике.**

[![Скачать Windows](https://img.shields.io/badge/%D0%A1%D0%BA%D0%B0%D1%87%D0%B0%D1%82%D1%8C-Windows-5e6a42?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/mustafamertakceylan/MorseCore/releases/download/v1.0.1/MorseCore-1.0.1-Windows-x64-Setup.exe)
[![Скачать macOS](https://img.shields.io/badge/macOS-Apple_Silicon-5e6a42?style=for-the-badge&logo=apple&logoColor=white)](https://github.com/mustafamertakceylan/MorseCore/releases/download/v1.0.1/MorseCore-1.0.1-macOS-arm64.dmg)
[![Скачать Linux](https://img.shields.io/badge/Linux-x64-5e6a42?style=for-the-badge&logo=linux&logoColor=white)](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v1.0.1)
[![1.0.1](https://img.shields.io/badge/v1.0.1-%D0%A0%D0%B5%D0%BB%D0%B8%D0%B7-c58c59?style=for-the-badge)](https://github.com/mustafamertakceylan/MorseCore/releases/tag/v1.0.1)

[English](README.md) · [Türkçe](README.tr.md) · [Deutsch](README.de.md) · **Русский** · [العربية](README.ar.md)

[**Wiki (EN/TR)**](https://github.com/mustafamertakceylan/MorseCore/wiki/Home)

</div>

MorseCore — бесплатное приложение для Windows, macOS и Linux: преобразуй текст в код Морзе, слушай сигналы и тренируйся. Твои сообщения остаются на твоём компьютере.

![MorseCore на русском: текст слева, код Морзе справа](docs/media/converter.ru.png)

**macOS:** открой [DMG](https://github.com/mustafamertakceylan/MorseCore/releases/download/v1.0.1/MorseCore-1.0.1-macOS-arm64.dmg) и перетащи MorseCore в «Программы». Эта версия не нотарифицирована Apple; может потребоваться разрешение в разделе «Конфиденциальность и безопасность». [Установка на Mac (EN/TR)](docs/macos.md).

**Linux:** `.deb` для Ubuntu, Debian и Pardus; переносной архив и `PKGBUILD` для Arch. [Установка Linux (EN)](docs/linux.md).

### Начало работы

1. В Windows: **Запусти Setup**, затем открой MorseCore через меню «Пуск».
2. Введи сообщение в **Конвертере** и запусти преобразование. Для прослушивания открой **Звук**.
3. В разделе **Обучение** находятся алфавит, упражнения и радиолюбительские сокращения.

Без установки? Распакуй [переносной ZIP](https://github.com/mustafamertakceylan/MorseCore/releases/download/v1.0.1/MorseCore-1.0.1-Windows-x64-Desktop.zip) и открой **MorseCore.Desktop.exe**. Эта сборка для Windows не подписана; Windows может показать предупреждение об издателе.

### Что можно делать?

- Преобразовывать текст и текстовые файлы в обе стороны, в том числе пакетами.
- Слушать Морзе, сохранять WAV и тренировать собственный ритм передачи.
- Распознавать чистые однотональные сигналы Морзе из WAV-файла или микрофона.
- В разделе **Любительское радио** управлять станциями, готовить сообщения и вести журнал QSO с импортом ADIF и офлайн-картой. Подключение оборудования запланировано; приложение не передаёт радиосигнал.
- Озвучивать текст офлайн с EMA Lightning, Antalia-2 Mini и Piper. Голос один раз скачивается через интернет, затем работает локально.

### Твой язык, твоя тема

**Türkçe · English · Deutsch · Русский · العربية**

Меню и дополнительные голоса доступны на всех пяти языках. Преобразование текста поддерживает латиницу/турецкий, русский и арабский алфавиты. В арабском интерфейсе меню зеркальное; Морзе остаётся слева направо.

В настройках можно выбрать светлую кремовую или тёмную зелёную тему. Плавное движение фона останавливается в неактивном окне и отключается настройкой уменьшения анимации.

<details>
<summary><b>Тёмная тема и карта связей</b></summary>

![Русский интерфейс в тёмной зелёной теме](docs/media/dark.ru.png)

![Русский журнал QSO и офлайн-карта](docs/media/map.ru.png)

</details>

**Доступно для Windows x64, macOS Apple Silicon (M1 и новее) и Linux x64.** [Установка Linux (EN)](docs/linux.md): Ubuntu, Debian, Arch и Pardus.

### Благодарности и лицензия

Спасибо **Canberk Aslan** за [EMA Lightning](https://github.com/canberk7/ema-lightning), **PatientDesk AI** за [Antalia-2 Mini](https://huggingface.co/cloud0day3/antalia-mini) и участникам **Piper** за офлайн-озвучивание.

[Apache 2.0](LICENSE) · [Благодарности и лицензии](NOTICE) · [Изменения](CHANGELOG.md) · [Настройка голосов (EN)](docs/offline-speech.md) · [Сборка из исходников (EN)](docs/desktop.md)
