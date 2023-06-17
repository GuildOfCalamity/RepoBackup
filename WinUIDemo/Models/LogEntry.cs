using System;

namespace WinUIDemo.Models;

public class LogEntry
{
    public LogLevel Severity { get; set; }
    public string Message { get; set; }
    public string Method { get; set; }
    public DateTime Time { get; set; }
    public LogEntry() { }
    public override string ToString()
    {
        return $"[{Time.ToString("hh:mm:ss.fff tt")}] {Message}";
    }
}
