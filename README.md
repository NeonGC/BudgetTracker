# BudgetTracker
[![Docker Pulls](https://img.shields.io/docker/pulls/neongc/budgettracker.svg)](https://hub.docker.com/r/neongc/budgettracker)
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2FNeonGC%2FBudgetTracker.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2FNeonGC%2FBudgetTracker?ref=badge_shield)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=NeonGC_BudgetTracker&metric=alert_status)](https://sonarcloud.io/dashboard?id=NeonGC_BudgetTracker)
## Описание
BudgetTracker - это персональное self-hosted решение для управления личными финансами и инвестициями. 
Основная задачу, которую решает BT - это ежедневный автоматический сбор данных и построение отчетности.
## Как запустить
Рекомендуемый способ запуска - Docker. Пример docker-compose файла:
#### docker-compose.yml
``` 
version: "3.3"
services:
  budgettracker:
    image: neongc/budgettracker:master
    restart: unless-stopped
    environment:
      Properties__IsProduction: 'true' # true если необходимо сохранять изменения в базу. false для локального запуска/отладки.
      Properties__ChromeDriver: 'http://chrome:4444' # строка подключения к контейнеру с Google Chrome
      Properties__Downloads: '/data' # путь к папке с загрузками - должна быть общая папка с Chrome
      ConnectionStrings__LiteDb: '/data/budgettracker.db' # Строка подключения к LiteDb, если используется локальный файл базы
    volumes:
      - /root/bt:/data # Путь монтирования папки /data, если используется локальный файл базы
    depends_on:
     - chrome
    ports:
      - "80:80"
    networks:
      public: {}
  chrome:
    image: selenium/standalone-chrome:latest
    restart: unless-stopped
    hostname: chrome
    volumes:
      - /dev/shm:/dev/shm
      - /root/bt:/data
    ports:
      - "4444:4444"
    networks:
      public: {}
networks:
  public:
    driver: bridge
```
## Источники данных:
На данный момент поддерживаются следующие источники данных:
- **АльфаБанк**
- **АльфаДирект**
- **АльфаКапитал** _требуется SMS-интеграция_
- **Долги**: создание балансов из заведенных вручную долгов
- **Райффайзен банк**
- **Тинькофф-банк**
- **Тинькофф-инвестиции**
- **FX**: Биржевые курсы валют USD/RUB, EUR/RUB, индекса S&P 500
- **API** (POST-endpoint)
  Пример запроса:
  ```
  # Статус на сегодня:
  POST /post-data
  name=Название+счета&value=1000.0&ccy=RUB
  # Статус на 31 февраля 2019:
  POST /post-data
  name=Название+счета&value=1000.0&ccy=RUB&when=31.02.2019
  # Добавление транзакций:
  POST /post-payment
  Content-Type: application/json
  [
    {
      "id": "123abc",
      "account": "test-account",
      "when": "01.10.2019 12:00:00",
      "amount": "5000",
      "currency": "RUB",
      "what": "Вернули долг"
    },
    {
      "id": "124abc",
      "account": "test-account",
      "when": "02.10.2019 12:00:00",
      "amount": "100",
      "currency": "RUB",
      "what": "Купил сникерс"
    }
  ]
  ```
## Табличное представление (история)
Из каждого источника данных ежедневно собираются данные в общее табличное представление.
Способ сбора данных - Selenium + ChromeWebDriver.
![Пример](https://github.com/NeonGC/BudgetTracker/raw/master/docs/images/history.jpg)
В случае нехватки каких-то данных(например - неуспешный парсинг) соответствующая ячейка таблицы подсвечивается черным фоном, а в подсказке видно каких данных не хватает.
Каждое значение характеризуется провайдером и названием счёта через знак ```/```. Например - _Альфа-Банк/Блиц-доход-USD_ или _FX/USD/RUB_
На основе этих данных можно строить свои вычисляемые столбцы - например посчитать сумму всего капитала из разных источников данных с конвертацией курса валют.
Примеры таких функций:
```
[Альфа-Банк/Блиц-доход-USD] * [FX/USD/RUB] + [Альфа-Банк/Блиц-доход-EUR] * [FX/EUR/RUB]
```
![Вычисляемые столбцы](https://github.com/NeonGC/BudgetTracker/raw/master/docs/images/computed-columns.jpg)
## Дашборд (отчёт)
На основной странице доступна система виджетов, которые берут свои значения из табличного представления.
![Пример](https://github.com/NeonGC/BudgetTracker/raw/master/docs/images/dashboard.jpg)
## Долги
Возможно вручную заносить долги (тех кто должен вам - с положительным знаком, и те которые вы должны - с отрицательным знаком баланса.
Возможно также указать шаблон описания переводов для автоматического учета их в долгах 
![Пример](https://github.com/NeonGC/BudgetTracker/raw/master/docs/images/debt.jpg)
## Интеграция с SMS:
В настоящее время возможна интеграция только с Android телефонами с использованием IFTTT и Tasker.
Для IFTTT используется простой рецепт с получением Android SMS и отправкой на **/sms**.
Для Tasker используется отправка на **/sms-tasker**. Подробнее см. код _ApiController.cs_.
Для смс есть различные правила - на текущий момент они описываются регулярными выражениями для того чтобы автоматически скрывать ненужные смс (например с кодами подтверждений) и учитывать траты из SMS, полученных от банка.
![Скриншот](https://github.com/NeonGC/BudgetTracker/raw/master/docs/images/sms.jpg)
## Учет расходов
### Разбор входящих СМС
Одним из вариантов учета расходов является парсинг SMS, полученных с телефона.
Для этого надо в раздел с SMS добавить правило обработки вида "траты" с регулярным выражением для разбора текста сообщений, например для Raiffeisen:
```
Karta \*3436;\s+Pokupka:(?<sum>[\d.]*) (?<ccy>[A-Z]{3});\s+(?<what>.*);\s+
```
Обязательно наличие именованных групп **sum**, **ccy**, **what** с суммой траты, валютой, и описанием траты соответственно.
Для дружелюбного представления и группировки трат есть понятие категорий. Категории также используя регулярное выражение группируют траты по описанию трат. Пример использования категорий - группировка трат на такси:
```
| Категория | Щаблон           |
| --------- | ---------------- | 
| Taxi	    | .*GETT.*         |
| Taxi      | .*UBER.*	       |
| Taxi	    | .*Yandex\.Taxi.* |
```
### Импорт выписки с онлайн банка
Для поддерживаемых провайдеров (Райффайзен, Альфабанк, Модульбанк) производится автоматом импорт выписок с приходом и расходом и создаются проводки в учёте.
### Генерация выписки по изменению баланса
Для некоторых случаев (пример - изменение стоимости акций) удобно иметь "виртуальные" проводки, просто чтобы изменение баланса совпадало с движением денежных средств.
Для этого в свойствах соответствующей колонки можно включить автоматическую генерацию проводок по балансу.
### Особенности учета расходов
Бывает три категории расходов - доход, расход и перевод.
В чем их отличие? Доход и расход учитываются на графиках, в оценках динамики капитала, в виджете трат.
Перевод - специальный вид проводки, который задуман для обозначения переводов денег со счёта на счёт. 
За счёт переводов сглаживаются графики и становится валидным прогноз динамики капитала (особенно актуально для инвестиций).
## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2FNeonGC%2FBudgetTracker.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2FNeonGC%2FBudgetTracker?ref=badge_large)
