using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using LisPort.Common;
namespace LisPort.Core
{
    public sealed class BridgeRequest
    {
        public string Mode { get; set; }
        public string LisInputPath { get; set; }
        public string OutputPath { get; set; }
        public string SourceSummaryPath { get; set; }
        public bool IncludeCurves { get; set; }
    }

    public sealed class BridgeRunOptions
    {
        public string PythonExecutablePath { get; set; } = "python";
        public string BridgeScriptPath { get; set; }
        public string WorkingDirectory { get; set; }
        public ILisErrorHandler ErrorHandler { get; set; }
        public int TimeoutMilliseconds { get; set; } = 120000;
        public int MaxCapturedOutputChars { get; set; } = 200000;
    }

    public static class PythonBridge
    {
        public static string Run(BridgeRequest request, BridgeRunOptions options)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (string.IsNullOrWhiteSpace(request.Mode))
            {
                throw new ArgumentException("Не указан режим работы bridge.", nameof(request));
            }
            if (!IsSupportedMode(request.Mode))
            {
                throw new ArgumentException("Неподдерживаемый режим bridge: " + request.Mode, nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.LisInputPath))
            {
                throw new ArgumentException("Не указан входной LIS-файл.", nameof(request));
            }

            var args = BuildArguments(request);
            return Execute(
                options.PythonExecutablePath,
                options.BridgeScriptPath,
                args,
                options.WorkingDirectory,
                options.TimeoutMilliseconds,
                options.MaxCapturedOutputChars);
        }

        public static string Execute(
            string pythonExePath,
            string scriptPath,
            string arguments,
            string workingDirectory = null,
            int timeoutMilliseconds = 120000,
            int maxCapturedOutputChars = 200000)
        {
            if (string.IsNullOrWhiteSpace(pythonExePath))
            {
                throw new ArgumentException("Не указан путь к python.exe", nameof(pythonExePath));
            }

            if (string.IsNullOrWhiteSpace(scriptPath))
            {
                throw new ArgumentException("Не указан путь к python-скрипту", nameof(scriptPath));
            }

            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("Python-скрипт не найден", scriptPath);
            }
            if (timeoutMilliseconds <= 0)
            {
                timeoutMilliseconds = 120000;
            }
            if (maxCapturedOutputChars <= 0)
            {
                maxCapturedOutputChars = 200000;
            }

            var effectiveWorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(workingDirectory);
            if (!Directory.Exists(effectiveWorkingDirectory))
            {
                throw new DirectoryNotFoundException(
                    "Рабочая директория для python bridge не найдена: " + effectiveWorkingDirectory);
            }

            var psi = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = Quote(scriptPath) + " " + (arguments ?? string.Empty),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = effectiveWorkingDirectory
            };

            using (var proc = new Process { StartInfo = psi })
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                var stdoutTruncated = false;
                var stderrTruncated = false;

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        AppendWithLimit(stdout, e.Data, maxCapturedOutputChars, ref stdoutTruncated);
                    }
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        AppendWithLimit(stderr, e.Data, maxCapturedOutputChars, ref stderrTruncated);
                    }
                };

                if (!proc.Start())
                {
                    throw new InvalidOperationException("Не удалось запустить python-процесс");
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                if (!proc.WaitForExit(timeoutMilliseconds))
                {
                    try
                    {
                        proc.Kill();
                    }
                    catch
                    {
                        // Ignore errors while terminating timed out process.
                    }
                    throw new TimeoutException(
                        "Python bridge превысил таймаут " + timeoutMilliseconds + " мс.");
                }
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
                    if (stdoutTruncated)
                    {
                        stdout.AppendLine("[stdout truncated]");
                    }
                    if (stderrTruncated)
                    {
                        stderr.AppendLine("[stderr truncated]");
                    }
                    throw new InvalidOperationException(
                        "Python bridge завершился с ошибкой. Код: " + proc.ExitCode + Environment.NewLine +
                        "stderr:" + Environment.NewLine + stderr + Environment.NewLine +
                        "stdout:" + Environment.NewLine + stdout);
                }

                return stdout.ToString();
            }
        }

        private static string BuildArguments(BridgeRequest request)
        {
            var builder = new StringBuilder();
            builder.Append("--mode ").Append(Quote(request.Mode)).Append(' ');
            builder.Append("--input ").Append(Quote(request.LisInputPath)).Append(' ');

            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                builder.Append("--output ").Append(Quote(request.OutputPath)).Append(' ');
            }

            if (!string.IsNullOrWhiteSpace(request.SourceSummaryPath))
            {
                builder.Append("--source-summary ").Append(Quote(request.SourceSummaryPath)).Append(' ');
            }

            if (request.IncludeCurves)
            {
                builder.Append("--include-curves ");
            }

            return builder.ToString().Trim();
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

        private static bool IsSupportedMode(string mode)
        {
            var supported = new HashSet<string>(StringComparer.Ordinal)
            {
                "read-summary",
                "write-raw-copy",
                "write-from-summary"
            };
            return supported.Contains(mode);
        }

        private static void AppendWithLimit(StringBuilder target, string line, int maxChars, ref bool truncated)
        {
            if (truncated)
            {
                return;
            }

            var toAppend = line + Environment.NewLine;
            if (target.Length + toAppend.Length <= maxChars)
            {
                target.Append(toAppend);
                return;
            }

            var remaining = maxChars - target.Length;
            if (remaining > 0)
            {
                target.Append(toAppend.Substring(0, remaining));
            }
            truncated = true;
        }
    }
}
