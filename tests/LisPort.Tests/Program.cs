using System;
using System.IO;
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
