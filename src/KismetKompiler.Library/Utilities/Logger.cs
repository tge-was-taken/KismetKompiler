namespace KismetKompiler.Library.Utilities;

public abstract class LogListener
{
    public string ChannelName { get; }

    public LogLevel Filter { get; set; } = LogLevel.All;

    public LogListener()
    {

    }

    public LogListener(LogLevel filter)
    {
        Filter = filter;
    }

    public LogListener(string channelName)
    {
        ChannelName = channelName;
    }

    public void Subscribe(Logger logger)
    {
        logger.LogEvent += OnLog;
    }

    public void Unsubscribe(Logger logger)
    {
        logger.LogEvent -= OnLog;
    }

    protected void OnLog(object sender, LogEventArgs e)
    {
        if (Filter.HasFlag(e.Level))
            OnLogCore(sender, e);
    }

    protected abstract void OnLogCore(object sender, LogEventArgs e);
}

public class Logger
{
    public event LogEventHandler LogEvent;

    public string Name { get; }

    public Logger(string name)
    {
        Name = name;
    }

    public void Log(LogLevel level, string message)
    {
        LogEvent?.Invoke(this, new LogEventArgs(Name, level, message));
    }

    public void Debug(string message)
    {
        Log(LogLevel.Debug, message);
    }

    public void Trace(string message)
    {
        Log(LogLevel.Trace, message);
    }

    public void Info(string message)
    {
        Log(LogLevel.Info, message);
    }

    public void Warning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    public void Error(string message)
    {
        Log(LogLevel.Error, message);
    }

    public void Fatal(string message)
    {
        Log(LogLevel.Fatal, message);
    }
}

public delegate void LogEventHandler(object sender, LogEventArgs e);

[Flags]
public enum LogLevel
{
    Debug = 1 << 1,
    Trace = 1 << 2,
    Info = 1 << 3,
    Warning = 1 << 4,
    Error = 1 << 5,
    Fatal = 1 << 6,
    All = Debug | Trace | Info | Warning | Error | Fatal
}

public class LogEventArgs : EventArgs
{
    public string ChannelName { get; }

    public LogLevel Level { get; }

    public string Message { get; }

    public LogEventArgs(string channelName, LogLevel level, string message)
    {
        Level = level;
        Message = message;
    }
}
