# lis_port

LIS-only порт библиотеки `dlisio` на C#.

## Платформа

- целевая платформа: **.NET Framework 4.8**
- целевая ОС: **Windows**
- текущая реализация запускает Python bridge (`dlisio`) и предназначена для
  достижения максимально точного parity поведения в LIS-сценариях

## Обязательное требование совместимости

Для этого проекта действует жесткий контракт:

- любые LIS-файлы должны **читаться** с поведением, совпадающим с upstream
  `dlisio` (структуры данных, ошибки, обработка edge-cases);
- любые LIS-файлы должны **записываться** так, чтобы результат был максимально
  эквивалентен поведению/ограничениям upstream `dlisio` и не ломал round-trip
  сценарии.

Практический критерий: parity-тесты с `dlisio` являются блокирующими для
слияния изменений.

## Текущий scope

- Основа для переноса: **upstream `dlisio`** (архитектура, поведение, тестовые
  фикстуры и контракты API).
- В текущей итерации переносим только **LIS часть**:
  - `dlisio.lis.*`
  - необходимые общие части (`dlisio.common`, часть `core`, которая нужна LIS)
- **DLIS часть (`dlisio.dlis.*`) исключена** из текущего scope и будет
  добавляться отдельно позже, если потребуется.

## Документация

- Детальный план LIS-only переноса: `docs/LIS_ONLY_PORT_PLAN.md`.

## Структура решения

- `lis_port.sln`
- `src/LisPort.Common` — общие типы ошибок/настроек
- `src/LisPort.Core` — bridge к `dlisio` через Python
- `src/LisPort.Lis` — публичный LIS API
- `src/LisPort.Cli` — консольный пример использования
- `tests/LisPort.Tests` — тестовый проект (net48)
- `tools/python_bridge` — Python-скрипты для чтения/записи LIS и smoke parity

## Быстрый старт (Windows)

1. Установить:
   - Visual Studio Build Tools / Visual Studio с поддержкой .NET Framework 4.8
   - Python 3.10+ (доступен как `python`)
2. Установить зависимости bridge:

   ```powershell
   python -m pip install -r tools/python_bridge/requirements.txt
   ```

3. Собрать solution:

   ```powershell
   msbuild lis_port.sln /p:Configuration=Release
   ```

4. Пример запуска CLI:

   ```powershell
   .\src\LisPort.Cli\bin\Release\LisPort.Cli.exe summary path\to\file.lis
   .\src\LisPort.Cli\bin\Release\LisPort.Cli.exe write-raw in.lis out.lis
   .\src\LisPort.Cli\bin\Release\LisPort.Cli.exe write-from-summary summary.json out.lis
   .\src\LisPort.Cli\bin\Release\LisPort.Cli.exe smoke path\to\file.lis C:\path\to\repo
   python tools\python_bridge\smoke_parity.py C:\path\to\repo path\to\file.lis
   ```

## Важное ограничение текущей итерации

`upstream dlisio` пока не содержит собственного LIS writer API. Поэтому на
этом этапе реализованы:

- строгий parity read через `dlisio.lis`;
- raw-copy запись для бинарного round-trip без изменений;
- write-from-summary как контролируемый сценарий воспроизведения через bridge.

Полный нативный LIS writer для C# будет развиваться отдельно как следующий шаг.
