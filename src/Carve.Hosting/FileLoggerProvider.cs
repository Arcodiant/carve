using Microsoft.Extensions.Logging;

namespace Carve.Hosting;

/// <summary>Writes one log file for this process. A file that cannot be opened only loses the file.</summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter? _writer;

    public FileLoggerProvider(string path, string serverName)
    {
        try
        {
            _writer = new StreamWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"{serverName}: no log file at {path}: {e.Message}");
        }
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        lock (this)
        {
            _writer?.Dispose();
        }
    }

    private void Write(string line)
    {
        lock (this)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch (ObjectDisposedException)
            {
                // A late line after the host has gone.
            }
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var line = $"{DateTime.UtcNow:O} {logLevel} {category}: {formatter(state, exception)}";
            provider.Write(exception is null ? line : $"{line}{Environment.NewLine}{exception}");
        }
    }
}
