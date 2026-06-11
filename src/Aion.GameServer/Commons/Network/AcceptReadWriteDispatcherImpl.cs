using System;
using System.Collections.Generic;
using Aion.Commons.Concurrent;
using Aion.Commons.Nio.Channels;

namespace Aion.GameServer.Commons.Network;

/// <summary>Java parity: commons/network/AcceptReadWriteDispatcherImpl (-Nemesiss-). Accepts, reads and writes.</summary>
public class AcceptReadWriteDispatcherImpl : Dispatcher
{
    private readonly List<AConnection> pendingClose = new List<AConnection>();

    public AcceptReadWriteDispatcherImpl(string name, Executor dcExecutor) : base(name, dcExecutor)
    {
    }

    internal override void Dispatch()
    {
        int selected = selector.Select();

        if (selected != 0)
        {
            ISet<SelectionKey> selectedKeys = selector.SelectedKeys();
            foreach (SelectionKey key in new List<SelectionKey>(selectedKeys))
            {
                selectedKeys.Remove(key);

                if (!key.IsValid())
                    continue;

                switch (key.ReadyOps())
                {
                    case SelectionKey.OP_ACCEPT:
                        Accept(key);
                        break;
                    case SelectionKey.OP_READ:
                        Read(key);
                        break;
                    case SelectionKey.OP_WRITE:
                        Write(key);
                        break;
                    case SelectionKey.OP_READ | SelectionKey.OP_WRITE:
                        Read(key);
                        if (key.IsValid())
                            Write(key);
                        break;
                }
            }
        }
        ProcessPendingClose();
    }

    internal override void CloseConnection(AConnection con)
    {
        lock (pendingClose)
        {
            pendingClose.Add(con);
        }
    }

    private void ProcessPendingClose()
    {
        if (pendingClose.Count == 0)
            return;
        lock (pendingClose)
        {
            long nowMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (int i = pendingClose.Count - 1; i >= 0; i--)
            {
                AConnection connection = pendingClose[i];
                if (connection.IsSendQueueEmpty() || !connection.IsConnected() || nowMillis > connection.pendingCloseUntilMillis)
                {
                    CloseConnectionImpl(connection);
                    pendingClose.RemoveAt(i);
                }
            }
        }
    }
}
