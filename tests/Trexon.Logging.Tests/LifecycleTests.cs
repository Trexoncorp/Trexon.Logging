namespace Trexon.Logging.Tests;

public sealed class LifecycleTests
{
    [Fact]
    public void Failing_sinks_and_error_handlers_do_not_block_healthy_sinks()
    {
        var sink = new MemorySink();
        var failures = new List<LoggingFailure>();
        using var logger = Logger.Create(c => c.OnError(failure =>
        {
            failures.Add(failure);
            throw new InvalidOperationException("handler failed");
        }).WriteTo.Sink(new ThrowingSink()).WriteTo.Sink(sink));
        logger.Info("first");
        logger.Info("second");
        Assert.Equal(2, sink.Events.Count);
        Assert.Equal(2, failures.Count);
        Assert.All(failures, f => Assert.IsType<IOException>(f.Exception));
    }

    [Fact]
    public void Failing_sink_predicate_does_not_block_other_sinks()
    {
        var failures = new List<LoggingFailure>();
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.OnError(failures.Add)
            .WriteTo.Sink(new MemorySink(), filter: _ => throw new InvalidOperationException("filter failed"))
            .WriteTo.Sink(sink));
        logger.Info("event");
        Assert.Single(sink.Events);
        Assert.Single(failures);
    }

    [Fact]
    public void Pipeline_failures_drop_the_event_and_recover_for_the_next_write()
    {
        var failures = new List<LoggingFailure>();
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.OnError(failures.Add).WriteTo.Sink(sink));
        logger.Info("{Number:Q}", 1.2m);
        logger.Info("healthy");
        Assert.Equal("healthy", Assert.Single(sink.Events).Message);
        Assert.Equal("Pipeline", Assert.Single(failures).Stage);
    }

    [Fact]
    public void Flush_and_dispose_have_explicit_ownership_and_are_idempotent()
    {
        var external = new MemorySink();
        var owned = new MemorySink();
        var logger = Logger.Create(c => c.WriteTo.Sink(external).WriteTo.Sink(owned, disposeWithLogger: true));
        logger.Info("event");
        logger.Flush();
        logger.Dispose();
        logger.Dispose();
        Assert.Equal(2, external.FlushCount);
        Assert.Equal(0, external.DisposeCount);
        Assert.Equal(2, owned.FlushCount);
        Assert.Equal(1, owned.DisposeCount);
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.Throws<ObjectDisposedException>(() => logger.Info("closed"));
        Assert.Throws<ObjectDisposedException>(logger.Flush);
    }

    [Fact]
    public void Disposing_a_bound_view_leaves_the_root_and_other_views_alive()
    {
        var sink = new MemorySink();
        var logger = Logger.Create(c => c.WriteTo.Sink(sink, disposeWithLogger: true));
        var bound = logger.Bind("User", "one");
        var other = logger.Bind("User", "two");
        bound.Dispose();
        Assert.Throws<ObjectDisposedException>(() => bound.Info("closed view"));
        logger.Info("root");
        other.Info("other");
        Assert.Equal(2, sink.Events.Count);
        Assert.Equal(0, sink.DisposeCount);
        logger.Dispose();
        Assert.Throws<ObjectDisposedException>(() => other.Info("closed root"));
        other.Dispose();
        Assert.Equal(1, sink.DisposeCount);
    }

    [Fact]
    public void Dispose_continues_to_other_owned_sinks_after_flush_and_dispose_failures()
    {
        var failures = new List<LoggingFailure>();
        var healthy = new MemorySink();
        var logger = Logger.Create(c => c.OnError(failures.Add)
            .WriteTo.Sink(new ThrowingSink(), disposeWithLogger: true)
            .WriteTo.Sink(healthy, disposeWithLogger: true));
        logger.Dispose();
        Assert.Equal(1, healthy.FlushCount);
        Assert.Equal(1, healthy.DisposeCount);
        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public void A_sink_registered_twice_is_disposed_only_once()
    {
        var sink = new MemorySink();
        var logger = Logger.Create(c => c.WriteTo.Sink(sink, disposeWithLogger: true).WriteTo.Sink(sink, disposeWithLogger: true));
        logger.Dispose();
        Assert.Equal(1, sink.FlushCount);
        Assert.Equal(1, sink.DisposeCount);
    }

    [Fact]
    public void Partial_construction_failure_releases_previously_created_owned_sinks()
    {
        using var directory = new TemporaryDirectory();
        var owned = new MemorySink();
        var path = directory.File("valid.log");
        Assert.ThrowsAny<IOException>(() => Logger.Create(c => c.WriteTo.Sink(owned, disposeWithLogger: true)
            .WriteTo.File(path).WriteTo.File(System.IO.Path.Combine(path, "child.log"))));
        Assert.Equal(1, owned.DisposeCount);
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Fact]
    public void Reentrant_logging_from_a_sink_does_not_recurse()
    {
        Logger? logger = null;
        var sink = new CallbackSink(() => logger!.Info("nested"));
        using (logger = Logger.Create(c => c.WriteTo.Sink(sink))) logger.Info("outer");
        Assert.Equal(1, sink.Calls);
    }

    [Fact]
    public void Reentrant_disposal_is_reported_without_breaking_the_logger()
    {
        Logger? logger = null;
        var failures = new List<LoggingFailure>();
        var sink = new MemorySink();
        using (logger = Logger.Create(c => c.OnError(failures.Add)
            .WriteTo.Sink(new CallbackSink(() => logger!.Dispose())).WriteTo.Sink(sink)))
        {
            logger.Info("one");
            logger.Info("two");
        }
        Assert.Equal(2, sink.Events.Count);
        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public void Parallel_writes_observe_the_same_event_order_in_all_sinks()
    {
        var first = new MemorySink();
        var second = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(first).WriteTo.Sink(second));
        Parallel.For(0, 1_000, id => logger.Info("{Id}", id));
        Assert.Equal(1_000, first.Events.Count);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(1_000, first.Events.Select(e => (int)e.Properties["Id"]!).Distinct().Count());
    }

    [Fact]
    public void Concurrent_shutdown_does_not_write_to_disposed_sinks()
    {
        var failures = new List<LoggingFailure>();
        var sink = new MemorySink();
        var logger = Logger.Create(c => c.OnError(failures.Add).WriteTo.Sink(sink, disposeWithLogger: true));
        var completed = 0;
        var rejected = 0;
        Parallel.Invoke(() => Parallel.For(0, 200, id =>
        {
            try { logger.Info("{Id}", id); Interlocked.Increment(ref completed); }
            catch (ObjectDisposedException) { Interlocked.Increment(ref rejected); }
        }), logger.Dispose);
        Assert.Equal(200, completed + rejected);
        Assert.Equal(completed, sink.Events.Count);
        Assert.Empty(failures);
        Assert.Equal(1, sink.DisposeCount);
    }

    [Fact]
    public void A_caller_owned_console_writer_remains_open_after_logger_disposal()
    {
        var writer = new StringWriter();
        using (var logger = Logger.Create(c => c.WriteTo.Console(o => o.Writer(writer)))) logger.Info("logged");
        writer.Write("still open");
        Assert.Contains("still open", writer.ToString());
    }

    private sealed class ThrowingSink : ILogSink
    {
        public void Emit(LogEvent logEvent) => throw new IOException("write failed");
        public void Flush() => throw new IOException("flush failed");
        public void Dispose() => throw new IOException("dispose failed");
    }

    private sealed class CallbackSink(Action callback) : ILogSink
    {
        public int Calls { get; private set; }
        public void Emit(LogEvent logEvent) { Calls++; callback(); }
        public void Flush() { }
        public void Dispose() { }
    }
}
