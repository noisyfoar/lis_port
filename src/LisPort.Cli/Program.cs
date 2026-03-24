using System;
using LisPort.Common;
using LisPort.Lis;

namespace LisPort.Cli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                PrintUsage();
                return 1;
            }

            var logger = new ConsoleLisLogger();
            var errorHandler = new ErrorHandler(
                LisErrorRules.WithLogger(logger, throwOnCritical: true));
            var options = LisApi.DefaultOptions(errorHandler);

            try
            {
                var command = args[0];
                if (string.Equals(command, "summary", StringComparison.OrdinalIgnoreCase))
                {
                    var summary = LisApi.LoadSummary(args[1], options);
                    Console.WriteLine(summary.SummaryJson);
                    return 0;
                }

                if (string.Equals(command, "write-raw", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Для команды write-raw нужен путь назначения.");
                        return 1;
                    }

                    var outputPath = args[2];
                    var result = LisApi.WriteRawCopy(args[1], outputPath, options);
                    Console.WriteLine("Готово: создана raw-копия LIS файла: " + outputPath);
                    Console.WriteLine("SHA-256 input : " + result.InputSha256);
                    Console.WriteLine("SHA-256 output: " + result.OutputSha256);
                    Console.WriteLine("Байт записано : " + result.Bytes);
                    return 0;
                }

                if (string.Equals(command, "write-from-summary", StringComparison.OrdinalIgnoreCase))
                {
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Для команды write-from-summary нужны summary.json и путь назначения.");
                        return 1;
                    }

                    var summaryPath = args[1];
                    var outputPath = args[2];
                    var result = LisApi.WriteFromSummary(summaryPath, outputPath, options);
                    Console.WriteLine("Готово: файл записан из summary: " + outputPath);
                    Console.WriteLine("SHA-256 output: " + result.OutputSha256);
                    Console.WriteLine("Байт записано : " + result.Bytes);
                    return 0;
                }

                if (string.Equals(command, "smoke", StringComparison.OrdinalIgnoreCase))
                {
                    var root = args.Length >= 3
                        ? args[2]
                        : Environment.CurrentDirectory;
                    var code = LisApi.RunSmokeParity(args[1], root, options);
                    Console.WriteLine("Smoke parity код выхода: " + code);
                    return code;
                }

                Console.Error.WriteLine("Неизвестная команда: " + command);
                PrintUsage();
                return 1;
            }
            catch (Exception ex)
            {
                errorHandler.Log(ErrorSeverity.Critical, "LisPort.Cli", ex.Message, "Прерывание выполнения.");
                Console.Error.WriteLine(ex);
                return 2;
            }
        }

        private static void PrintUsage()
        {
            Console.Error.WriteLine("Использование:");
            Console.Error.WriteLine("  LisPort.Cli.exe summary <path-to-lis>");
            Console.Error.WriteLine("  LisPort.Cli.exe write-raw <input-lis> <output-lis>");
            Console.Error.WriteLine("  LisPort.Cli.exe write-from-summary <summary-json> <output-lis>");
            Console.Error.WriteLine("  LisPort.Cli.exe smoke <path-to-lis> [repo-root]");
        }
    }

    internal sealed class ConsoleLisLogger : ILisLogger
    {
        public void Debug(string message)
        {
            Console.Error.WriteLine("[DEBUG] " + message);
        }

        public void Info(string message)
        {
            Console.Error.WriteLine("[INFO] " + message);
        }

        public void Warning(string message)
        {
            Console.Error.WriteLine("[WARN] " + message);
        }

        public void Error(string message)
        {
            Console.Error.WriteLine("[ERROR] " + message);
        }
    }
}
