using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace QuantResearchAgent
{
    public class SimpleFileLoggerProvider : ILoggerProvider
    {
        private readonly string _filePath;
        private StreamWriter? _writer;

        public SimpleFileLoggerProvider(string filePath)
        {
            _filePath = filePath;
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            _writer = new StreamWriter(new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new SimpleFileLogger(_writer, categoryName);
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }

    public class SimpleFileLogger : ILogger
    {
        private readonly StreamWriter? _writer;
        private readonly string _category;

    // Explicit interface implementation
    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        return BeginScopeImplementation(state);
    }

    private IDisposable BeginScopeImplementation<TState>(TState state) where TState : notnull
    {
        // Implementation for beginning a logging scope
        return null!;
    }

    public SimpleFileLogger(StreamWriter? writer, string category)
    {
        _writer = writer;
        _category = category;
    }

        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (_writer == null) return;
            var msg = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{logLevel}] {_category}: {formatter(state, exception)}";
            if (exception != null)
                msg += $"\nException: {exception}";
            _writer.WriteLine(msg);
        }
    }
}
