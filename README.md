﻿## 1. Терминология

### Common

- **Currency (Валюта)** — валюта, тип денег. Например, доллар США или биткоин.
- **Currency pair (Валютная пара)** — пара валют, означающая отношение одной валюты к другой. Порядок имеет значение. Например, доллар США к биткоину.
- **Symbol (Символ)** — символьное обозначение валюты или валютной пары. Например, USD или USDBTC.
- **Platform (Платформа)** — система, являющаяся основой для различных видов деятельности. Программная платформа — это программная система, обычно со своим API.
- **Exchange (Биржа)** — учреждение для заключения финансовых и коммерческих сделок. Часто реализуется как программная платформа, доступная через интернет.

### Development

- **Connector (Коннектор)** — класс или группа классов, через которые происходит подключение к API платформ. Коннекторы делятся на:
    - Клиенты, которые приводят REST и WS-интерфейсы платформ к единому интерфейсу на уровне кода.
    - Коннекторы в узком смысле, использующие клиентов и реализующие логику API-платформ (реконнекты, обработка ошибок, фиксация скачанных и пропущенных данных).
- **Client (Клиент)** — класс, обеспечивающий взаимодействие с внешними API (REST и WS). Преобразует внешний API в API на уровне классов языка программирования, позволяя работать с разными платформами через единый интерфейс.

### Интерфейс ITestConnector

Интерфейс и модели Trade и Candle доступны по [ссылке](https://drive.google.com/file/d/1RuY1PQs2esq_7hxsalORC-4-st1MdY8y/view?usp=drive_link). Это упрощенный вариант интерфейса. Если недостаточно входных параметров, их можно дополнить с пояснением. Также допускается удаление лишних параметров с объяснением (например, в комментариях кода).

---

## 2. Тестовое задание

### Требования

Реализовать коннектор под исходный интерфейс (см. пункт 3) на **C# (Class Library)**, а также покрыть его интеграционными тестами или реализовать GUI на **WPF** по паттерну **MVVM** (не WinForms).

### Функциональность коннектора

#### **Класс клиента для REST API биржи Bitfinex:**
- Получение трейдов (trades)
- Получение свечей (candles)
- Получение информации о тикере (Ticker)

#### **Класс клиента для Websocket API биржи Bitfinex:**
- Получение трейдов (trades)
- Получение свечей (candles)

**API Bitfinex**: [ссылка на API](https://docs.bitfinex.com/v2/) (использовать версию API v2).

### Дополнительное задание

Реализовать расчет общего баланса портфеля в каждой из перечисленных валют: **USDT, BTC, XRP, XMR и DASH**, исходя из следующего баланса:
- **1 BTC**
- **15,000 XRP**
- **50 XMR**
- **30 DASH**

Результаты должны быть выведены в **DataGrid WPF**.

### Допущения

- Можно использовать API любой известной биржи (**Binance, Bybit и т. д.**).
- Можно вместо WPF использовать **ASP.NET** (любой удобный фреймворк).
- **WinForms и консольные приложения не допускаются**.
- Будет проведена проверка на использование **ИИ**: при полном копировании задание не засчитывается.

---

## 3. Критерии оценки

- **Простота и понятность реализации**.
- **Качество кода**.
- **Возможность легкого масштабирования системы**.
- **Работоспособность коннектора** (выполнение всех функций).
- **Желательно вести историю коммитов в репозитории** для оценки последовательности выполнения задачи.

# Notes:
- Реализация `ITestConnector` для биржи Bitfinex в виде библиотеки классов, а также проект с графическим интерфейсом на базе WPF.