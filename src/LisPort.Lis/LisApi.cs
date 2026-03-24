using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
            BridgeTimeoutMilliseconds = 120000;
            MaxBridgeOutputChars = 200000;
        }

        public string PythonExecutablePath { get; set; }
        public string BridgeScriptPath { get; set; }
        public string OutputDirectory { get; set; }
        public ErrorHandler ErrorHandler { get; set; }
        public bool IncludeCurves { get; set; }
        public int BridgeTimeoutMilliseconds { get; set; }
        public int MaxBridgeOutputChars { get; set; }
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

    public sealed class LisWriteResult
    {
        public LisWriteResult(string inputPath, string outputPath, string inputSha256, string outputSha256, long bytes)
        {
            InputPath = inputPath;
            OutputPath = outputPath;
            InputSha256 = inputSha256;
            OutputSha256 = outputSha256;
            Bytes = bytes;
        }

        public string InputPath { get; private set; }
        public string OutputPath { get; private set; }
        public string InputSha256 { get; private set; }
        public string OutputSha256 { get; private set; }
        public long Bytes { get; private set; }
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

        public static LisWriteResult WriteRawCopy(string inputLisPath, string outputLisPath, LisLoadOptions options = null)
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
            return ValidateWrittenCopy(inputLisPath, outputFullPath);
        }

        public static LisWriteResult WriteFromSummary(string summaryJsonPath, string outputLisPath, LisLoadOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(summaryJsonPath))
            {
                throw new ArgumentException("Путь к summary не должен быть пустым.", "summaryJsonPath");
            }
            var summaryFullPath = Path.GetFullPath(summaryJsonPath);
            if (!File.Exists(summaryFullPath))
            {
                throw new FileNotFoundException("Summary JSON не найден.", summaryFullPath);
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
                LisInputPath = summaryFullPath,
                OutputPath = outputFullPath
            };

            var runOptions = ToRunOptions(effectiveOptions);
            PythonBridge.Run(request, runOptions);
            return ValidateWrittenCopy(summaryJsonPath, outputFullPath, isSummaryInput: true);
        }

        public static int RunSmokeParity(string lisPath, string repoRoot = null, LisLoadOptions options = null)
        {
            ValidateInputPath(lisPath);
            var effectiveOptions = options ?? new LisLoadOptions();
            var root = string.IsNullOrWhiteSpace(repoRoot) ? Environment.CurrentDirectory : repoRoot;
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException("Корневая директория репозитория не найдена: " + root);
            }
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
                ErrorHandler = options.ErrorHandler,
                TimeoutMilliseconds = options.BridgeTimeoutMilliseconds,
                MaxCapturedOutputChars = options.MaxBridgeOutputChars
            };
        }

        private static LisWriteResult ValidateWrittenCopy(string inputPath, string outputPath, bool isSummaryInput = false)
        {
            if (!File.Exists(outputPath))
            {
                throw new InvalidOperationException("После записи output-файл не найден: " + outputPath);
            }

            var outInfo = new FileInfo(outputPath);
            var outputSha = ComputeSha256(outputPath);

            if (!isSummaryInput)
            {
                var inputSha = ComputeSha256(inputPath);
                if (!string.Equals(inputSha, outputSha, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Контроль целостности не пройден: SHA-256 input и output отличаются.");
                }

                return new LisWriteResult(
                    Path.GetFullPath(inputPath),
                    Path.GetFullPath(outputPath),
                    inputSha,
                    outputSha,
                    outInfo.Length);
            }

            return new LisWriteResult(
                Path.GetFullPath(inputPath),
                Path.GetFullPath(outputPath),
                inputSha256: string.Empty,
                outputSha256: outputSha,
                bytes: outInfo.Length);
        }

        private static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
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
