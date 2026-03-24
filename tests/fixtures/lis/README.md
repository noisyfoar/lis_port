Тестовые LIS-фикстуры для `LisPort.Tests`.

Источник: upstream `equinor/dlisio` (`python/data/lis`).

В набор включены файлы, используемые для интеграционных проверок:
- `MUD_LOG_1.LIS`
- `layouts/layout_00.lis`
- `layouts/layout_01.lis`
- `layouts/layout_tif_01.lis`
- `layouts/truncated_15.lis`
- `layouts/wrong_06.lis`
- `records/inforec_01.lis`
- `records/inforec_02.lis`

Назначение:
- проверка `LoadSummary`
- round-trip через `WriteRawCopy`
- smoke parity через `tools/python_bridge/smoke_parity.py`
