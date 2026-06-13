using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace CamaraPortuaria.UI.Logging;

internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logDir;
    private readonly int _maxFiles;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _writeLock = new();
    private StreamWriter? _writer;
    private DateOnly _currentDate;

    public FileLoggerProvider(string logDir, int maxFiles = 30)
    {
        _logDir = logDir;
        _maxFiles = maxFiles;
        Directory.CreateDirectory(logDir);
        _currentDate = DateOnly.FromDateTime(DateTime.Now);
        _writer = OpenWriter(_currentDate);
        PurgeOldFiles();
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));

    internal void Write(string categoryName, LogLevel level, string message, Exception? exception)
    {
        lock (_writeLock)
        {
            var now = DateTime.Now;
            var today = DateOnly.FromDateTime(now);
            if (today != _currentDate)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _currentDate = today;
                _writer = OpenWriter(today);
                PurgeOldFiles();
            }

            var levelStr = level switch
            {
                LogLevel.Trace       => "TRC",
                LogLevel.Debug       => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning     => "WRN",
                LogLevel.Error       => "ERR",
                _                    => "CRT",
            };

            _writer?.WriteLine($"{now:yyyy-MM-dd HH:mm:ss} [{levelStr}] {categoryName}: {message}");
            if (exception is not null)
                _writer?.WriteLine(exception.ToString());
            _writer?.Flush();
        }
    }

    private StreamWriter OpenWriter(DateOnly date)
    {
        var path = Path.Combine(_logDir, $"app-{date:yyyyMMdd}.log");
        return new StreamWriter(path, append: true, System.Text.Encoding.UTF8) { AutoFlush = false };
    }

    private void PurgeOldFiles()
    {
        var old = Directory.GetFiles(_logDir, "app-????????.log")
            .OrderByDescending(f => f)
            .Skip(_maxFiles);
        foreach (var f in old)
            try { File.Delete(f); } catch { }
    }

    public void Dispose()
    {
        _loggers.Clear();
        lock (_writeLock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }
}

internal sealed class FileLogger(string categoryName, FileLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // N-4: Warning+ se escribe siempre (rechazos AFIP, etc. llegan sin Exception);
        // Information/Debug solo si traen excepción (logs chicos).
        if (exception is null && logLevel < LogLevel.Warning) return;
        provider.Write(categoryName, logLevel, formatter(state, exception), exception);
    }
}
