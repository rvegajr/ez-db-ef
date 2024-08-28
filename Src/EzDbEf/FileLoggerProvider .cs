using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace EzDbEf
{
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _path;

        public FileLoggerProvider(string path)
        {
            _path = path;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(_path);
        }

        public void Dispose() { }

        class FileLogger : ILogger
        {
            private readonly string _path;

            public FileLogger(string path)
            {
                _path = path;
            }

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => default!;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                File.AppendAllText(_path, formatter(state, exception) + Environment.NewLine);
            }
        }
    }
}