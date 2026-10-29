namespace Trexon.Logging.Tests;

internal class MemorySink : ILogSink
{
    public List<LogEvent> Events { get; } = [];
    public int FlushCount { get; private set; }
    public int DisposeCount { get; private set; }
    public virtual void Emit(LogEvent logEvent) => Events.Add(logEvent);
    public virtual void Flush() => FlushCount++;
    public virtual void Dispose() => DisposeCount++;
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class TemporaryDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "trexon-logging-tests-" + Guid.NewGuid().ToString("N"));
    public TemporaryDirectory() => Directory.CreateDirectory(Path);
    public string File(string name) => System.IO.Path.Combine(Path, name);
    public void Dispose() => Directory.Delete(Path, true);
}

[CollectionDefinition("Process globals", DisableParallelization = true)]
public sealed class ProcessGlobalsCollection;
