using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Concurrent;
using Aion.Commons.Lang;
using Aion.GameServer.Commons.Network.Packet;

namespace Aion.GameServer.Commons.Network;

/// <summary>
/// Java parity: commons/network/PacketProcessor (-Nemesiss-). Executes client packets in receive order
/// with one-packet-per-client-at-a-time. &lt;T extends AConnection&lt;?&gt;&gt; -> where T : AConnection.
/// ReentrantLock+Condition -> Monitor (lock + Wait/Pulse); Thread.ofPlatform -> Thread; thread kill via
/// Interrupt()/ThreadInterruptedException. Platform-boundary threading adaptation.
/// </summary>
public class PacketProcessor<T> where T : AConnection
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("PacketProcessor");

    private readonly int threadSpawnThreshold;
    private readonly int threadKillThreshold;

    private readonly object lockObj = new object();
    private readonly LinkedList<BaseClientPacket<T>> packets = new LinkedList<BaseClientPacket<T>>();
    private readonly List<Thread> threads = new List<Thread>();

    private readonly int minThreads;
    private readonly int maxThreads;
    private readonly Executor executor;

    private sealed class DummyExecutor : Executor
    {
        public void Execute(Runnable command) => command.Run();
    }

    public PacketProcessor(int minThreads, int maxThreads, int threadSpawnThreshold, int threadKillThreshold)
        : this(minThreads, maxThreads, threadSpawnThreshold, threadKillThreshold, new DummyExecutor())
    {
    }

    public PacketProcessor(int minThreads, int maxThreads, int threadSpawnThreshold, int threadKillThreshold, Executor executor)
    {
        CheckArgument(minThreads > 0, "Min Threads must be positive");
        CheckArgument(maxThreads >= minThreads, "Max Threads must be >= Min Threads");
        CheckArgument(threadSpawnThreshold > 0, "Thread Spawn Threshold must be positive");
        CheckArgument(threadKillThreshold > 0, "Thread Kill Threshold must be positive");

        this.minThreads = minThreads;
        this.maxThreads = maxThreads;
        this.threadSpawnThreshold = threadSpawnThreshold;
        this.threadKillThreshold = threadKillThreshold;
        this.executor = executor;

        if (minThreads != maxThreads)
            StartCheckerThread();

        for (int i = 0; i < minThreads; i++)
            NewThread();
    }

    private void CheckArgument(bool condition, string errorMessage)
    {
        if (!condition)
            throw new ArgumentException(errorMessage);
    }

    private void StartCheckerThread()
    {
        Thread t = new Thread(CheckerTask) { Name = "PacketProcessor:Checker", IsBackground = true };
        t.Start();
    }

    private bool NewThread()
    {
        if (threads.Count >= maxThreads)
            return false;

        string name = "PacketProcessor:" + threads.Count;
        log.LogDebug("Creating new PacketProcessor Thread: " + name);

        Thread t = new Thread(PacketProcessorTask) { Name = name, IsBackground = true };
        threads.Add(t);
        t.Start();

        return true;
    }

    private void KillThread()
    {
        if (threads.Count > minThreads)
        {
            Thread t = threads[threads.Count - 1];
            threads.RemoveAt(threads.Count - 1);
            log.LogDebug("Killing PacketProcessor Thread: " + t.Name);
            t.Interrupt();
        }
    }

    /// <summary>Add a packet to the execution queue; it will be executed asap on another thread.</summary>
    public void ExecutePacket(BaseClientPacket<T> packet)
    {
        lock (lockObj)
        {
            packets.AddLast(packet);
            Monitor.Pulse(lockObj);
        }
    }

    /// <summary>First packet available for execution (1 packet/client at a time, in received order). Caller holds the lock.</summary>
    private BaseClientPacket<T> GetFirstAvailable()
    {
        for (;;)
        {
            while (packets.Count == 0)
                Monitor.Wait(lockObj);

            for (LinkedListNode<BaseClientPacket<T>>? node = packets.First; node != null; node = node.Next)
            {
                BaseClientPacket<T> packet = node.Value;
                if (packet.GetConnection().TryLockConnection())
                {
                    packets.Remove(node);
                    return packet;
                }
            }
            Monitor.Wait(lockObj);
        }
    }

    private void PacketProcessorTask()
    {
        BaseClientPacket<T>? packet = null;
        try
        {
            for (;;)
            {
                lock (lockObj)
                {
                    if (packet != null)
                        packet.GetConnection().UnlockConnection();

                    packet = GetFirstAvailable();
                }
                executor.Execute(packet);
            }
        }
        catch (ThreadInterruptedException)
        {
            // thread killed
        }
    }

    private void CheckerTask()
    {
        int previousPacketCount = 0;
        for (;;)
        {
            try
            {
                Thread.Sleep(TimeSpan.FromMinutes(1));
            }
            catch (ThreadInterruptedException)
            {
                return;
            }

            int packetsWaitingForExecution = packets.Count;
            if (packetsWaitingForExecution <= previousPacketCount && packetsWaitingForExecution <= threadKillThreshold)
            {
                KillThread();
            }
            else if (packetsWaitingForExecution > threadSpawnThreshold)
            {
                if (!NewThread() && packetsWaitingForExecution >= threadSpawnThreshold * 3)
                    log.LogWarning("Lag detected! [" + packetsWaitingForExecution
                        + " client packets are waiting for execution]. Consider increasing PacketProcessor maxThreads or a hardware upgrade.");
            }
            previousPacketCount = packetsWaitingForExecution;
        }
    }
}
