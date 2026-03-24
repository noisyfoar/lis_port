using System;
using System.Diagnostics;
using System.IO;
using LisPort.Common;
using LisPort.Core;

namespace LisPort.Lis
{
    public sealed class LisLoadOptions
    {
        public LisLoadOptions()
        {
            PythonExecutablePath = "python";
            BridgeScriptPath = Path.Combine("tools", "python_bridge", "dlisio_lis_bridge.py");
            OutputDirectory = Path.Combine(Path.GetTempPath(), "lis_port");
            ErrorHandler = new ErrorHandler();
            IncludeCurves = true;
        }

        public string PythonExecutablePath { get; set; }
        public string BridgeScriptPath { get; set; }
        public string OutputDirectory { get; set; }
        public ErrorHandler ErrorHandler { get; set; }
        public bool IncludeCurves { get; set; }
    }

    public sealed class LisSummaryResult
    {
        public LisSummaryResult(string sourcePath, string summaryJson)
        {
            SourcePath = sourcePath;
            SummaryJson = summaryJson;
        }

        public string SourcePath { get; private set; }
        public string SummaryJson { get; private set; }
    }

    public static class LisApi
    {
        public static LisLoadOptions DefaultOptions(ErrorHandler errorHandler = null)
        {
            var options = new LisLoadOptions();
            options.ErrorHandler = errorHandler ?? new ErrorHandler();
            return options;
        }

        public static LisSummaryResult LoadSummary(string lisPath, LisLoadOptions options = null)
        {
            ValidateInputPath(lisPath);

            var effectiveOptions = options ?? new LisLoadOptions();
            Directory.CreateDirectory(effectiveOptions.OutputDirectory);

            var request = new BridgeRequest
            {
                Mode = "read-summary",
                LisInputPath = Path.GetFullPath(lisPath),
                IncludeCurves = effectiveOptions.IncludeCurves
            };

            var runOptions = ToRunOptions(effectiveOptions);
            var json = PythonBridge.Run(request, runOptions);
            return new LisSummaryResult(lisPath, json);
        }

        public static void WriteRawCopy(string inputLisPath, string outputLisPath, LisLoadOptions options = null)
        {
            ValidateInputPath(inputLisPath);
            if (string.IsNullOrWhiteSpace(outputLisPath))
            {
                throw new ArgumentException("Путь назначения не должен быть пустым.", "outputLisPath");
            }

            var effectiveOptions = options ?? new LisLoadOptions();
            var outputFullPath = Path.GetFullPath(outputLisPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? ".");

            var request = new BridgeRequest
            {
                Mode = "write-raw-copy",
                LisInputPath = Path.GetFullPath(inputLisPath),
                OutputPath = outputFullPath
            };

            var runOptions = ToRunOptions(effectiveOptions);
            PythonBridge.Run(request, runOptions);
        }

        public static void WriteFromSummary(string summaryJsonPath, string outputLisPath, LisLoadOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(summaryJsonPath))
            {
                throw new ArgumentException("Путь к summary не должен быть пустым.", "summaryJsonPath");
            }

            if (!File.Exists(summaryJsonPath))
            {
                throw new FileNotFoundException("Summary JSON не найден.", summaryJsonPath);
            }

            if (string.IsNullOrWhiteSpace(outputLisPath))
            {
                throw new ArgumentException("Путь назначения не должен быть пустым.", "outputLisPath");
            }

            var effectiveOptions = options ?? new LisLoadOptions();
            var outputFullPath = Path.GetFullPath(outputLisPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFullPath) ?? ".");

            var request = new BridgeRequest
            {
                Mode = "write-from-summary",
                LisInputPath = Path.GetFullPath(summaryJsonPath),
                OutputPath = outputFullPath
            };

            var runOptions = ToRunOptions(effectiveOptions);
            PythonBridge.Run(request, runOptions);
        }

        public static int RunSmokeParity(string lisPath, string repoRoot = null, LisLoadOptions options = null)
        {
            ValidateInputPath(lisPath);
            var effectiveOptions = options ?? new LisLoadOptions();
            var root = string.IsNullOrWhiteSpace(repoRoot) ? Environment.CurrentDirectory : repoRoot;
            var script = Path.Combine(root, "tools", "python_bridge", "smoke_parity.py");
            if (!File.Exists(script))
            {
                throw new FileNotFoundException("Скрипт smoke parity не найден.", script);
            }

            var psi = new ProcessStartInfo
            {
                FileName = effectiveOptions.PythonExecutablePath,
                Arguments = Quote(script) + " " + Quote(root) + " " + Quote(Path.GetFullPath(lisPath)),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = root
            };

            using (var process = Process.Start(psi))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Не удалось запустить smoke parity.");
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(stdout))
                {
                    Console.WriteLine(stdout.Trim());
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    Console.Error.WriteLine(stderr.Trim());
                }

                return process.ExitCode;
            }
        }

        private static void ValidateInputPath(string lisPath)
        {
            if (string.IsNullOrWhiteSpace(lisPath))
            {
                throw new ArgumentException("Путь к LIS-файлу не должен быть пустым.", "lisPath");
            }

            if (!File.Exists(lisPath))
            {
                throw new FileNotFoundException("LIS-файл не найден.", lisPath);
            }
        }

        private static BridgeRunOptions ToRunOptions(LisLoadOptions options)
        {
            return new BridgeRunOptions
            {
                PythonExecutablePath = options.PythonExecutablePath,
                BridgeScriptPath = options.BridgeScriptPath,
                ErrorHandler = options.ErrorHandler
            };
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            if (value.IndexOf(' ') >= 0 || value.IndexOf('\t') >= 0 || value.IndexOf('"') >= 0)
            {
                return "\"" + value.Replace("\"", "\\\"") + "\"";
            }

            return value;
        }
    }
}
