<div align="center">

# ⚡ SteamForge

**Next-Gen Steam Manifest & Library Management Suite**

[![License: MIT](https://img.shields.io/badge/License-MIT-38bdf8?style=for-the-badge)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows-6366f1?style=for-the-badge&logo=windows)](https://github.com/moskvol/steamforge)
[![Built with C#](https://img.shields.io/badge/.NET-WPF-0ea5e9?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)

<p align="center">
  <img src="icon.ico" width="128" alt="SteamForge Logo"/>
</p>

*Легковесный, быстрый и удобный инструмент для продвинутого управления манифестами, играми и библиотекой Steam.*

[Скачать установщик](https://github.com/moskvol/steamforge/releases) • [Сообщить о баге](https://github.com/moskvol/steamforge/issues)

---

</div>

## ✨ Основные возможности

- 🚀 **Управление манифестами Steam**: мгновенная генерация, валидация и исправление файлов `appmanifest_*.acf`.
- 🎨 **Современный UI**: стильный интерфейс с отзывчивым управлением.
- 🛠️ **Инструменты автоматизации**: автоматическое определение директорий установки игр и путей Steam.
- 📦 **Готовый инсталлятор**: бесшовная установка в систему через Inno Setup.

---

## 📥 Установка

### Быстрый запуск
1. Перейди в раздел [**Releases**](https://github.com/moskvol/steamforge/releases).
2. Скачай последний файл **`SteamForge_Setup_v1.0.0.exe`**.
3. Запусти установщик и следуй инструкциям мастера.

### Сборка из исходников
```bash
git clone [https://github.com/moskvol/steamforge.git](https://github.com/moskvol/steamforge.git)
cd steamforge
dotnet build -c Release
