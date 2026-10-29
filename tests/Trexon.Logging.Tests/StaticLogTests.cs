namespace Trexon.Logging.Tests;

[Collection("Process globals")]
public sealed class StaticLogTests
{
    [Fact]
    public void Default_console_logger_works_without_configuration()
    {
        var previous = Console.Error;
        var writer = new StringWriter();
        try
        {
            Console.SetError(writer);
            using var logger = Logger.Create();
            logger.Info("default");
            Assert.Contains("[INF] default", writer.ToString());
        }
        finally { Console.SetError(previous); }
    }

    [Fact]
    public void Static_facade_supports_configuration_context_and_shutdown()
    {
        var sink = new MemorySink();
        try
        {
            Log.Configure(c => c.WriteTo.Sink(sink, disposeWithLogger: true));
            using (Log.PushScope("RequestId", 9)) Log.Info("hello {Name}", "world");
            Log.Flush();
            Assert.Equal("hello world", Assert.Single(sink.Events).Message);
            Assert.Equal(9, sink.Events[0].Properties["RequestId"]);
            Log.Shutdown();
            Assert.Equal(1, sink.DisposeCount);
            Assert.False(Log.IsEnabled(LogLevel.Information));
        }
        finally { Log.Configure(c => c.MinimumLevel(LogLevel.None)); }
    }

    [Fact]
    public void Static_reconfiguration_disposes_the_old_root_and_invalidates_old_views()
    {
        var first = new MemorySink();
        var second = new MemorySink();
        try
        {
            Log.Configure(c => c.WriteTo.Sink(first, disposeWithLogger: true));
            using var oldView = Log.Bind("Old", true);
            Log.Configure(c => c.WriteTo.Sink(second));
            Log.Info("new");
            Assert.Equal(1, first.DisposeCount);
            Assert.Empty(first.Events);
            Assert.Equal("new", Assert.Single(second.Events).Message);
            Assert.Throws<ObjectDisposedException>(() => oldView.Info("old"));
        }
        finally { Log.Configure(c => c.MinimumLevel(LogLevel.None)); }
    }

    [Fact]
    public void Failed_static_configuration_preserves_the_previous_logger()
    {
        var sink = new MemorySink();
        try
        {
            Log.Configure(c => c.WriteTo.Sink(sink));
            Assert.ThrowsAny<ArgumentException>(() => Log.Configure(c => c.Redact("[")));
            Log.Info("still working");
            Assert.Single(sink.Events);
        }
        finally { Log.Configure(c => c.MinimumLevel(LogLevel.None)); }
    }
}
