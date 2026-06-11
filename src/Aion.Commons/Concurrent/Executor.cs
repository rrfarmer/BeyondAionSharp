using System;
using Aion.Commons.Lang;

namespace Aion.Commons.Concurrent;

/// <summary>Java parity: java.util.concurrent.Executor — runs submitted Runnables.</summary>
public interface Executor
{
    void Execute(Runnable command);
}

/// <summary>Adapts a C# delegate to a Runnable (for Java method-reference call sites like <c>this::onDisconnect</c>).</summary>
public sealed class LambdaRunnable : Runnable
{
    private readonly Action action;
    public LambdaRunnable(Action action) { this.action = action; }
    public void Run() => action();
}

/// <summary>Default Executor: runs the Runnable on the runtime thread pool (used as the reactor's disconnect executor).</summary>
public sealed class ThreadPoolExecutor : Executor
{
    public void Execute(Runnable command) => System.Threading.ThreadPool.QueueUserWorkItem(_ => command.Run());
}
