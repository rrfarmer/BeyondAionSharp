using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Concurrent;
using Aion.Commons.Nio.Channels;

namespace Aion.GameServer.Commons.Network;

/// <summary>Java parity: commons/network/NioServer (-Nemesiss-). Handles connections on the configured addresses.</summary>
public class NioServer
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(NioServer));

    private readonly List<SelectionKey> serverChannelKeys = new List<SelectionKey>();
    private Dispatcher acceptDispatcher = null!;
    private int currentReadWriteDispatcher;
    private Dispatcher[]? readWriteDispatchers;

    private readonly int readWriteThreads;
    private readonly ServerCfg[] cfgs;

    public NioServer(int readWriteThreads, params ServerCfg[] cfgs)
    {
        this.readWriteThreads = readWriteThreads;
        this.cfgs = cfgs;
    }

    public void Connect(Executor dcExecutor)
    {
        try
        {
            InitDispatchers(readWriteThreads, dcExecutor);

            foreach (ServerCfg cfg in cfgs)
            {
                ServerSocketChannel serverChannel = ServerSocketChannel.Open();
                serverChannel.ConfigureBlocking(false);

                serverChannel.Socket().Bind(cfg.Address);
                log.LogInformation("Listening on " + cfg.GetAddressInfo() + " for " + cfg.ClientDescription);

                SelectionKey acceptKey = GetAcceptDispatcher().Register(serverChannel, SelectionKey.OP_ACCEPT, new Acceptor(cfg.ConnectionFactory, this));
                serverChannelKeys.Add(acceptKey);
            }
        }
        catch (Exception e)
        {
            throw new Exception("Could not open server socket: " + e.Message, e);
        }
    }

    public Dispatcher GetAcceptDispatcher() => acceptDispatcher;

    /// <summary>One of the ReadWrite dispatchers, or the Accept dispatcher if readWriteThreads was 0.</summary>
    public Dispatcher GetReadWriteDispatcher()
    {
        if (readWriteDispatchers == null)
            return acceptDispatcher;

        if (readWriteDispatchers.Length == 1)
            return readWriteDispatchers[0];

        if (currentReadWriteDispatcher >= readWriteDispatchers.Length)
            currentReadWriteDispatcher = 0;
        return readWriteDispatchers[currentReadWriteDispatcher++];
    }

    private void InitDispatchers(int readWriteThreads, Executor dcExecutor)
    {
        if (readWriteThreads < 1)
        {
            acceptDispatcher = new AcceptReadWriteDispatcherImpl("AcceptReadWrite Dispatcher", dcExecutor);
            acceptDispatcher.Start();
        }
        else
        {
            acceptDispatcher = new AcceptDispatcherImpl("Accept Dispatcher", dcExecutor);
            acceptDispatcher.Start();

            readWriteDispatchers = new Dispatcher[readWriteThreads];
            for (int i = 0; i < readWriteDispatchers.Length; i++)
            {
                readWriteDispatchers[i] = new AcceptReadWriteDispatcherImpl("ReadWrite-" + i + " Dispatcher", dcExecutor);
                readWriteDispatchers[i].Start();
            }
        }
    }

    public void Shutdown()
    {
        log.LogInformation("Closing ServerChannels...");
        serverChannelKeys.ForEach(k => k.Cancel());
        log.LogInformation("ServerChannels closed.");

        HashSet<AConnection> activeConnections = FindAllConnections();
        if (activeConnections.Count != 0)
        {
            foreach (AConnection con in activeConnections)
                con.OnServerClose();

            long timeout = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 5000;
            while (IsAnyConnectionClosePending(activeConnections))
            {
                System.Threading.Thread.Sleep(100);
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > timeout)
                {
                    activeConnections.RemoveWhere(c => c.IsClosed());
                    foreach (AConnection con in activeConnections)
                        con.Close();
                    break;
                }
            }
            activeConnections.RemoveWhere(c => c.IsClosed());
        }
    }

    private HashSet<AConnection> FindAllConnections()
    {
        HashSet<AConnection> activeConnections = new HashSet<AConnection>();
        if (readWriteDispatchers != null)
        {
            foreach (Dispatcher d in readWriteDispatchers)
                foreach (SelectionKey key in d.Selector().Keys())
                    if (key.Attachment() is AConnection connection)
                        activeConnections.Add(connection);
        }
        foreach (SelectionKey key in acceptDispatcher.Selector().Keys())
            if (key.Attachment() is AConnection connection)
                activeConnections.Add(connection);
        return activeConnections;
    }

    private bool IsAnyConnectionClosePending(IEnumerable<AConnection> connections)
    {
        foreach (AConnection c in connections)
            if (c.IsPendingClose())
                return true;
        return false;
    }
}
