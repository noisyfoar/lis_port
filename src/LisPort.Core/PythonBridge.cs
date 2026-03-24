using System;
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

            if (string.IsNullOrWhiteSpace(request.LisInputPath))
            {
                throw new ArgumentException("Не указан входной LIS-файл.", nameof(request));
            }

            var args = BuildArguments(request);
            return Execute(options.PythonExecutablePath, options.BridgeScriptPath, args, options.WorkingDirectory);
        }

        public static string Execute(string pythonExePath, string scriptPath, string arguments, string workingDirectory = null)
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

            var psi = new ProcessStartInfo
            {
                FileName = pythonExePath,
                Arguments = Quote(scriptPath) + " " + (arguments ?? string.Empty),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory
            };

            using (var proc = new Process { StartInfo = psi })
            {
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        stdout.AppendLine(e.Data);
                    }
                };
                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        stderr.AppendLine(e.Data);
                    }
                };

                if (!proc.Start())
                {
                    throw new InvalidOperationException("Не удалось запустить python-процесс");
                }

                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                {
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
    }
}
