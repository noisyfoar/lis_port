using System;
using System.IO;
using System.Linq;
using LisPort.Core;
using LisPort.Lis;

namespace LisPort.Tests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var ok = true;
            Console.WriteLine("LisPort.Tests (self-check) запущен.");

            ok &= Run("PythonBridge.Run отклоняет неподдерживаемый режим", TestUnsupportedModeRejected);
            ok &= Run("PythonBridge.Execute валидирует рабочую директорию", TestExecuteRejectsMissingWorkingDirectory);
            ok &= Run("LisApi.LoadSummary отклоняет пустой путь", TestLoadSummaryRejectsEmptyPath);
            ok &= Run("LisApi.WriteRawCopy отклоняет пустой output", TestWriteRawCopyRejectsEmptyOutput);
            ok &= Run("LisApi.WriteFromSummary отклоняет отсутствующий summary", TestWriteFromSummaryRejectsMissingSummary);
            ok &= Run("Интеграция: summary на LIS-фикстурах", TestSummaryOnFixtureFiles);
            ok &= Run("Интеграция: raw-copy round-trip на LIS-фикстурах", TestRawCopyRoundTripOnFixtures);
            ok &= Run("Интеграция: smoke parity скрипт на LIS-фикстурах", TestSmokeParityOnFixtures);

            if (args.Length >= 1)
            {
                ok &= Run("LisApi.LoadSummary smoke по реальному LIS", () =>
                {
                    var summary = LisApi.LoadSummary(args[0]);
                    if (string.IsNullOrWhiteSpace(summary.SummaryJson))
                    {
                        throw new InvalidOperationException("Получен пустой summary JSON.");
                    }
                });
            }

            Console.WriteLine(ok ? "OK: все тесты прошли." : "FAIL: есть упавшие тесты.");
            return ok ? 0 : 2;
        }

        private static bool Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("[PASS] " + name);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] " + name);
                Console.WriteLine(ex);
                return false;
            }
        }

        private static void TestUnsupportedModeRejected()
        {
            var request = new BridgeRequest
            {
                Mode = "drop-database",
                LisInputPath = "dummy.lis"
            };
            var options = new BridgeRunOptions
            {
                PythonExecutablePath = "python",
                BridgeScriptPath = "dummy.py",
                WorkingDirectory = Environment.CurrentDirectory
            };
            ExpectThrows<ArgumentException>(() => PythonBridge.Run(request, options));
        }

        private static void TestExecuteRejectsMissingWorkingDirectory()
        {
            var tempScript = Path.Combine(Path.GetTempPath(), "lis_port_dummy_script.py");
            File.WriteAllText(tempScript, "print('ok')");
            try
            {
                ExpectThrows<DirectoryNotFoundException>(() =>
                    PythonBridge.Execute(
                        "python",
                        tempScript,
                        string.Empty,
                        Path.Combine(Path.GetTempPath(), "definitely-missing-" + Guid.NewGuid())));
            }
            finally
            {
                if (File.Exists(tempScript)) File.Delete(tempScript);
            }
        }

        private static void TestLoadSummaryRejectsEmptyPath()
        {
            ExpectThrows<ArgumentException>(() => LisApi.LoadSummary(""));
        }

        private static void TestWriteRawCopyRejectsEmptyOutput()
        {
            var fakeInput = Path.Combine(Path.GetTempPath(), "lis_port_fake_input_" + Guid.NewGuid() + ".lis");
            File.WriteAllBytes(fakeInput, new byte[] { 0x00 });
            try
            {
                ExpectThrows<ArgumentException>(() => LisApi.WriteRawCopy(fakeInput, ""));
            }
            finally
            {
                if (File.Exists(fakeInput)) File.Delete(fakeInput);
            }
        }

        private static void TestWriteFromSummaryRejectsMissingSummary()
        {
            var missing = Path.Combine(Path.GetTempPath(), "lis_port_missing_" + Guid.NewGuid() + ".json");
            var outPath = Path.Combine(Path.GetTempPath(), "lis_port_out_" + Guid.NewGuid() + ".lis");
            ExpectThrows<FileNotFoundException>(() => LisApi.WriteFromSummary(missing, outPath));
        }

        private static void TestSummaryOnFixtureFiles()
        {
            foreach (var lisPath in EnumerateFixtureLisFiles())
            {
                var summary = LisApi.LoadSummary(lisPath);
                if (string.IsNullOrWhiteSpace(summary.SummaryJson))
                {
                    throw new InvalidOperationException("Пустой summary JSON для фикстуры: " + lisPath);
                }
            }
        }

        private static void TestRawCopyRoundTripOnFixtures()
        {
            foreach (var lisPath in EnumerateFixtureLisFiles())
            {
                var tmp = Path.Combine(Path.GetTempPath(), "lis_port_roundtrip_" + Guid.NewGuid() + ".lis");
                try
                {
                    var write = LisApi.WriteRawCopy(lisPath, tmp);
                    if (!File.Exists(tmp))
                    {
                        throw new InvalidOperationException("Round-trip файл не создан: " + lisPath);
                    }
                    if (write.Bytes <= 0)
                    {
                        throw new InvalidOperationException("Round-trip вернул некорректный размер байт: " + lisPath);
                    }

                    var summary = LisApi.LoadSummary(tmp);
                    if (string.IsNullOrWhiteSpace(summary.SummaryJson))
                    {
                        throw new InvalidOperationException("Пустой summary JSON после round-trip: " + lisPath);
                    }
                }
                finally
                {
                    if (File.Exists(tmp)) File.Delete(tmp);
                }
            }
        }

        private static void TestSmokeParityOnFixtures()
        {
            var root = ResolveRepoRoot();
            var options = LisApi.DefaultOptions();
            foreach (var lisPath in EnumerateFixtureLisFiles())
            {
                var code = LisApi.RunSmokeParity(lisPath, root, options);
                if (code != 0)
                {
                    throw new InvalidOperationException("Smoke parity завершился с кодом " + code + " для: " + lisPath);
                }
            }
        }

        private static string[] EnumerateFixtureLisFiles()
        {
            var fixturesRoot = Path.Combine(ResolveRepoRoot(), "tests", "fixtures", "lis");
            if (!Directory.Exists(fixturesRoot))
            {
                throw new DirectoryNotFoundException("Каталог фикстур не найден: " + fixturesRoot);
            }

            var files = Directory.GetFiles(fixturesRoot, "*.lis", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(fixturesRoot, "*.LIS", SearchOption.AllDirectories))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length == 0)
            {
                throw new InvalidOperationException("Не найдено ни одного LIS-фикстура в: " + fixturesRoot);
            }

            return files;
        }

        private static string ResolveRepoRoot()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            for (var i = 0; i < 8; i++)
            {
                var candidate = Path.GetFullPath(Path.Combine(current, string.Join(Path.DirectorySeparatorChar.ToString(), Enumerable.Repeat("..", i))));
                if (File.Exists(Path.Combine(candidate, "lis_port.sln")))
                {
                    return candidate;
                }
            }
            return Environment.CurrentDirectory;
        }

        private static void ExpectThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("Ожидалось исключение типа " + typeof(T).Name);
        }
    }

}
