using Aion.GameServer.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aion.GameServer.Tests;

/// <summary>
/// Regression for the "cannot enter instance (Haramel)" bug: animated (FADE_OUT_BEAM) teleports must DEFER the
/// spawn-in until the client reports the fade-out animation finished (CM_TELEPORT_ANIMATION_DONE), mirroring Java's
/// stored-but-unsubmitted <c>new FutureTask&lt;&gt;(spawnTask, null)</c>. Previously the spawn was scheduled on the
/// pool with zero delay, so it ran immediately and pushed the new-map packets before the client was ready.
/// </summary>
public sealed class ThreadPoolManagerDeferredTaskTests
{
    [Fact]
    public async Task Deferred_DoesNotRunBodyUntilRunIsCalled()
    {
        await using var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
        int ran = 0;

        var task = pool.Deferred(() => Interlocked.Increment(ref ran));

        // Give an eager (buggy) schedule a chance to fire — it must NOT.
        await Task.Delay(50);
        Assert.Equal(0, ran);
        Assert.False(task.IsDone());

        task.Run(); // CM_TELEPORT_ANIMATION_DONE path

        Assert.Equal(1, ran);
        Assert.True(task.IsDone());
        task.Get(); // must not throw for a successful body
    }

    [Fact]
    public void Deferred_Run_IsIdempotent()
    {
        var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);
        int ran = 0;

        var task = pool.Deferred(() => Interlocked.Increment(ref ran));
        task.Run();
        task.Run(); // a second Run() (e.g. stray packet) must not re-execute or throw

        Assert.Equal(1, ran);
    }

    [Fact]
    public void Deferred_Get_SurfacesBodyException()
    {
        var pool = new ThreadPoolManager(NullLogger<ThreadPoolManager>.Instance);

        var task = pool.Deferred(() => throw new InvalidOperationException("spawn failed"));
        task.Run(); // RunSynchronously stores the exception; Run() itself does not throw

        // Mirrors CM_TELEPORT_ANIMATION_DONE: Get() re-throws so the recovery branch (re-spawn) can fire.
        var ex = Assert.Throws<InvalidOperationException>(() => task.Get());
        Assert.Equal("spawn failed", ex.Message);
        Assert.True(task.IsDone());
    }
}
