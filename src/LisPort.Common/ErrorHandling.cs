using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LisPort.Common
{
    public enum ErrorSeverity
    {
        Info = 1,
        Minor = 2,
        Major = 3,
        Critical = 4
    }

    public enum ErrorAction
    {
        Ignore = 0,
        Log = 1,
        Throw = 2
    }

    public interface ILisLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warning(string message);
        void Error(string message);
    }

    public sealed class NullLisLogger : ILisLogger
    {
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
    }

    public sealed class LisError
    {
        public LisError(
            ErrorSeverity severity,
            string context,
            string problem,
            string specification,
            string action,
            string debug)
        {
            Severity = severity;
            Context = context ?? string.Empty;
            Problem = problem ?? string.Empty;
            Specification = specification ?? string.Empty;
            Action = action ?? string.Empty;
            Debug = debug ?? string.Empty;
        }

        public ErrorSeverity Severity { get; private set; }
        public string Context { get; private set; }
        public string Problem { get; private set; }
        public string Specification { get; private set; }
        public string Action { get; private set; }
        public string Debug { get; private set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Проблема: {0}", Problem));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Где: {0}", Context));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Критичность: {0}", Severity));
            if (!string.IsNullOrWhiteSpace(Specification))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Ссылка на спецификацию: {0}", Specification));
            }

            if (!string.IsNullOrWhiteSpace(Action))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Действие: {0}", Action));
            }

            if (!string.IsNullOrWhiteSpace(Debug))
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "Отладка: {0}", Debug));
            }

            return sb.ToString().TrimEnd();
        }
    }

    public sealed class LisErrorRules
    {
        public ErrorAction Info { get; set; }
        public ErrorAction Minor { get; set; }
        public ErrorAction Major { get; set; }
        public ErrorAction Critical { get; set; }

        public static LisErrorRules Strict()
        {
            return new LisErrorRules
            {
                Info = ErrorAction.Log,
                Minor = ErrorAction.Log,
                Major = ErrorAction.Log,
                Critical = ErrorAction.Throw
            };
        }

        public static LisErrorRules WithLogger(ILisLogger logger, bool throwOnCritical)
        {
            _ = logger;
            return new LisErrorRules
            {
                Info = ErrorAction.Log,
                Minor = ErrorAction.Log,
                Major = ErrorAction.Log,
                Critical = throwOnCritical ? ErrorAction.Throw : ErrorAction.Log
            };
        }
    }

    public sealed class ErrorHandler : ILisErrorHandler
    {
        private readonly LisErrorHandler _inner;

        public ErrorHandler()
            : this(LisErrorRules.Strict())
        {
        }

        public ErrorHandler(LisErrorRules rules)
        {
            _inner = new LisErrorHandler(new NullLisLogger(), rules);
        }

        public IReadOnlyList<LisError> Entries
        {
            get { return _inner.Entries; }
        }

        public void Log(
            ErrorSeverity severity,
            string context,
            string problem,
            string specification = "",
            string action = "",
            string debug = "")
        {
            _inner.Log(severity, context, problem, specification, action, debug);
        }
    }

    public interface ILisErrorHandler
    {
        IReadOnlyList<LisError> Entries { get; }

        void Log(
            ErrorSeverity severity,
            string context,
            string problem,
            string specification = "",
            string action = "",
            string debug = "");
    }

    public sealed class LisErrorHandler : ILisErrorHandler
    {
        private readonly List<LisError> _entries = new List<LisError>();
        private readonly ILisLogger _logger;

        public LisErrorHandler()
            : this(new NullLisLogger(), LisErrorRules.Strict())
        {
        }

        public LisErrorHandler(ILisLogger logger)
            : this(logger, LisErrorRules.Strict())
        {
        }

        public LisErrorHandler(ILisLogger logger, LisErrorRules rules)
        {
            _logger = logger ?? new NullLisLogger();
            Rules = rules ?? LisErrorRules.Strict();
        }

        public LisErrorRules Rules { get; private set; }

        public IReadOnlyList<LisError> Entries
        {
            get { return _entries.AsReadOnly(); }
        }

        public void Log(
            ErrorSeverity severity,
            string context,
            string problem,
            string specification = "",
            string action = "",
            string debug = "")
        {
            var entry = new LisError(severity, context, problem, specification, action, debug);
            _entries.Add(entry);

            var mode = ResolveAction(severity);
            if (mode == ErrorAction.Ignore)
            {
                return;
            }

            if (mode == ErrorAction.Log)
            {
                LogToLogger(entry);
                return;
            }

            throw new InvalidOperationException(entry.ToString());
        }

        private ErrorAction ResolveAction(ErrorSeverity severity)
        {
            switch (severity)
            {
                case ErrorSeverity.Info:
                    return Rules.Info;
                case ErrorSeverity.Minor:
                    return Rules.Minor;
                case ErrorSeverity.Major:
                    return Rules.Major;
                case ErrorSeverity.Critical:
                    return Rules.Critical;
                default:
                    return ErrorAction.Throw;
            }
        }

        private void LogToLogger(LisError entry)
        {
            var text = entry.ToString();
            switch (entry.Severity)
            {
                case ErrorSeverity.Info:
                    _logger.Debug(text);
                    break;
                case ErrorSeverity.Minor:
                    _logger.Info(text);
                    break;
                case ErrorSeverity.Major:
                    _logger.Warning(text);
                    break;
                case ErrorSeverity.Critical:
                    _logger.Error(text);
                    break;
            }
        }
    }
}
