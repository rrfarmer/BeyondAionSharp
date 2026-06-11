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
