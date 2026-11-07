using System.Collections.Concurrent;

namespace CoCaNgua.Server.Services;

public class WebLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public LogLevel Level { get; set; }
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
}

public class WebLogCollector : ILoggerProvider
{
    private static readonly ConcurrentQueue<WebLogEntry> _recentLogs = new();
    private const int MaxLogCount = 500;
    public static event Action<WebLogEntry>? OnLogReceived;

    public static IReadOnlyList<WebLogEntry> GetRecentLogs() => _recentLogs.ToArray();

    public static void AddLog(LogLevel level, string category, string message)
    {
        var entry = new WebLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Category = category,
            Message = message
        };

        _recentLogs.Enqueue(entry);
        while (_recentLogs.Count > MaxLogCount && _recentLogs.TryDequeue(out _)) { }

        try
        {
            OnLogReceived?.Invoke(entry);
        }
        catch { }
    }

    public ILogger CreateLogger(string categoryName) => new WebLogger(categoryName);
    public void Dispose() { }
}

public class WebLogger : ILogger
{
    private readonly string _category;

    public WebLogger(string category)
    {
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        string message = formatter(state, exception);
        if (exception != null)
        {
            message += "\n" + exception.Message;
        }

        // Simplify ASP.NET Core verbose noise
        if (_category.StartsWith("Microsoft.AspNetCore.Routing") ||
            _category.StartsWith("Microsoft.AspNetCore.StaticFiles") ||
            _category.StartsWith("Microsoft.AspNetCore.Hosting.Diagnostics"))
        {
            if (logLevel < LogLevel.Warning) return;
        }

        WebLogCollector.AddLog(logLevel, _category, message);
    }
}
