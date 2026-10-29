namespace Trexon.Logging.Tests;

public sealed class ContextTests
{
    [Fact]
    public void Nested_scopes_restore_the_previous_context()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        using (logger.PushScope("RequestId", "outer"))
        {
            logger.Info("outer");
            using (logger.PushScope("RequestId", "inner")) logger.Info("inner");
            logger.Info("restored");
        }
        logger.Info("outside");
        Assert.Equal("outer", sink.Events[0].Properties["RequestId"]);
        Assert.Equal("inner", sink.Events[1].Properties["RequestId"]);
        Assert.Equal("outer", sink.Events[2].Properties["RequestId"]);
        Assert.False(sink.Events[3].Properties.ContainsKey("RequestId"));
    }

    [Fact]
    public async Task Concurrent_async_flows_keep_their_own_request_properties()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async request =>
        {
            using var scope = logger.PushScope("RequestId", request);
            await Task.Yield();
            logger.Info("Request {Expected}", request);
            await Task.Yield();
            logger.Info("Request {Expected}", request);
        }));
        logger.Info("outside");
        Assert.Equal(41, sink.Events.Count);
        Assert.All(sink.Events.Take(40), e => Assert.Equal(e.Properties["Expected"], e.Properties["RequestId"]));
        Assert.False(sink.Events[^1].Properties.ContainsKey("RequestId"));
    }

    [Fact]
    public async Task Disposing_an_inherited_scope_in_a_child_does_not_prevent_parent_cleanup()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        var scope = logger.PushScope("Parent", true);
        await Task.Run(() => { scope.Dispose(); logger.Info("child"); }, TestContext.Current.CancellationToken);
        logger.Info("parent");
        scope.Dispose();
        scope.Dispose();
        logger.Info("outside");
        Assert.False(sink.Events[0].Properties.ContainsKey("Parent"));
        Assert.Equal(true, sink.Events[1].Properties["Parent"]);
        Assert.False(sink.Events[2].Properties.ContainsKey("Parent"));
    }

    [Fact]
    public void Out_of_order_disposal_fails_without_corrupting_the_scope_stack()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        var outer = logger.PushScope("Id", 1);
        var inner = logger.PushScope("Id", 2);
        Assert.Throws<InvalidOperationException>(outer.Dispose);
        logger.Info("inner");
        inner.Dispose();
        outer.Dispose();
        logger.Info("outside");
        Assert.Equal(2, sink.Events[0].Properties["Id"]);
        Assert.False(sink.Events[1].Properties.ContainsKey("Id"));
    }

    [Fact]
    public void Bound_views_share_scopes_but_not_bound_properties()
    {
        var sink = new MemorySink();
        using var logger = Logger.Create(c => c.WriteTo.Sink(sink));
        using var bound = logger.Bind("User", "alice");
        using var scope = bound.PushScope("RequestId", 42);
        logger.Info("root");
        bound.Info("bound");
        Assert.Equal(42, sink.Events[0].Properties["RequestId"]);
        Assert.False(sink.Events[0].Properties.ContainsKey("User"));
        Assert.Equal("alice", sink.Events[1].Properties["User"]);
    }
}
