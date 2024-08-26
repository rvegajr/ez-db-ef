namespace EzDbEf;

class FileLoggerProvider : ILoggerProvider
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
}

class FileLogger : ILogger
{
    private readonly string _path;

    public FileLogger(string path)
    {
        _path = path;
    }

    public IDisposable BeginScope<TState>(TState state) => null!;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {message}";

        if (exception != null)
        {
            logEntry += $"\nException: {exception}";
        }

        File.AppendAllText(_path, logEntry + Environment.NewLine);
    }
}