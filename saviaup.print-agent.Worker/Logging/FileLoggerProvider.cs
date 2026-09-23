using SaviaUp.PrintAgent.Infrastructure.Device;

namespace SaviaUp.PrintAgent.Worker.Logging;

public sealed class FileLoggerProvider(AgentPathResolver paths) : ILoggerProvider
{
    private readonly object _gate = new();
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, paths.LogDirectory, _gate);
    public void Dispose() { }

    private sealed class FileLogger(string category, string logDirectory, object gate) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            Directory.CreateDirectory(logDirectory);
            var path = Path.Combine(logDirectory, $"agent-{DateTime.UtcNow:yyyyMMdd}.log");
            var line = $"{DateTimeOffset.UtcNow:O} [{logLevel}] {category}: {formatter(state, exception)}";
            if (exception is not null) line += $" | {exception.GetType().Name}: {exception.Message}";
            lock (gate) File.AppendAllText(path, line + Environment.NewLine);
        }
    }
}
