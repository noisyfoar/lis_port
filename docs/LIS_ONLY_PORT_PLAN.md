# LIS-only план переноса `dlisio` на C#

Этот документ фиксирует текущую стратегию: переносим только LIS-функциональность
из upstream `dlisio`, опираясь на реальное поведение `dlisio`, а не на «чистую»
трактовку спецификации LIS79.

## 1) Scope текущей итерации

### Входит в scope

- `dlisio.lis.*`:
  - загрузка/индексация LIS-файла;
  - разбиение на Logical Files;
  - парсинг явных/фиксированных LIS records, используемых публичным API;
  - чтение curve-данных через DFSR;
  - обработка delimiter-records (reel/tape headers/trailers, logical EOF);
  - high-level API уровня `PhysicalFile`, `LogicalFile`, `curves`, `curves_metadata`.
- `dlisio.common.*` (минимум, нужный для LIS):
  - `ErrorHandler` и действия по severity;
  - настройка строковых encoding fallback.
- LIS-специфичная часть `core` (порт C++ ядра, которое реально используется LIS).

### Не входит в scope (пока)

- Любой функционал `dlisio.dlis.*`.
- DLIS object model (`FRAME`, `CHANNEL`, `pool`, `object_set` и т.д.).
- DLIS-specific IO steps (`findsul`, `findvrl`, `open_rp66`, `findfdata`, ...).

## 2) Принцип совместимости (жесткое требование)

Цель: максимально точное воспроизведение поведения upstream `dlisio` для LIS:

- любые поддержанные LIS-файлы должны **читаться** с результатом, эквивалентным
  `dlisio`;
- любые поддержанные операции записи должны **создавать LIS-файлы**, которые при
  чтении в `dlisio` дают эквивалентный результат;
- в неоднозначных случаях приоритет у фактического поведения `dlisio`,
  а не у «теоретически правильной» интерпретации стандарта.

Что считаем «совместимо»:

- одинаковое разбиение физического файла на logical files;
- одинаковая логика обработки delimiter-records;
- одинаковые parsed структуры для поддержанных типов records;
- одинаковое поведение при некорректных файлах (severity, остановка/продолжение);
- одинаковые значения curves (по каналам, типам, порядку и форме результата);
- для записанных файлов: round-trip parity (`C# write -> dlisio read`) и
  (`dlisio read -> C# write -> dlisio read`) без потери семантики в поддержанном scope.

## 3) Архитектура C# (LIS-only)

Предлагаемая структура решения:

- `src/LisPort.Core`
  - бинарные примитивы LIS;
  - stream/offset abstractions;
  - индексация logical records;
  - low-level parsers.
- `src/LisPort.Common`
  - error handling abstractions;
  - encoding configuration;
  - shared utility types.
- `src/LisPort.Lis`
  - `Load(...)`;
  - `PhysicalFile`, `LogicalFile`, `HeaderTrailer`;
  - high-level parsers/dispatch (`parse_record` аналог).
- `tests/LisPort.Tests`
  - unit + integration + parity tests.

## 4) Порядок реализации

### Этап A — каркас и контракты

- создать solution/проекты;
- описать интерфейсы error handler и severity actions;
- добавить контрактные тесты на поведение API (пока на заглушках).

### Этап B — low-level LIS core

- порт `lis/protocol`, `lis/types`, `lis/pack` частей, нужных для парсинга;
- порт `lis/io` логики:
  - `openlis` аналог;
  - `index_records` аналог;
  - доступ к explicit/implicit индексам;
  - детекция incomplete index.

### Этап C — high-level LIS API

- `load` + `FileIndexer` аналог;
- `HeaderTrailer`, `LogicalFile`, `PhysicalFile`;
- `parse_record` dispatch для поддерживаемых record types;
- `data_format_specs()`, `job_identification()`, `wellsite_data()`,
  `tool_string_info()`, text-record endpoints.

### Этап D — curves

- порт чтения data records для DFSR;
- формирование табличного результата и metadata curves;
- обработка fast channels/индекс-канала аналогично upstream.

### Этап E — LIS writing parity

- спроектировать writer только для поддержанного LIS subset;
- реализовать бинарную сериализацию records с учетом layout-ожиданий `dlisio`;
- гарантировать, что записанные файлы стабильно читаются `dlisio` без
  деградации данных/метаданных в пределах поддержанного scope.

### Этап F — parity/устойчивость

- подключить фикстуры upstream (LIS subset);
- добавить golden-tests C# vs upstream `dlisio` output;
- стабилизировать edge-cases и error escape-hatch режимы.

## 5) Тестовая стратегия (обязательная)

- Базовые unit-тесты на бинарные конвертации и record parsing.
- Integration-тесты на целых LIS-файлах.
- Parity-тесты:
  - запуск эталона (`dlisio` Python) на тех же входах;
  - сравнение структурированных результатов и ошибок.
- Write/round-trip parity:
  - `C# read -> C# write -> dlisio read` сравнение результатов;
  - `dlisio read -> C# write -> dlisio read` сравнение результатов;
  - контроль стабильности сериализации на проблемных и edge-case фикстурах.

## 6) Риски LIS-only переноса

- Тонкости оффсетов/EOF при tape image layouts.
- Неполные/частично некорректные файлы: важно повторить поведение ErrorHandler.
- Совместимость curves-вывода (типы/shape/порядок полей).

## 7) Практическое правило scope-контроля

Если модуль относится к DLIS и не нужен для работоспособности LIS-сценариев —
не переносим его в текущей итерации.

