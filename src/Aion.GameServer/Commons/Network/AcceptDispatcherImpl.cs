using System;
using System.Collections.Generic;
using Aion.Commons.Concurrent;
using Aion.Commons.Nio.Channels;

namespace Aion.GameServer.Commons.Network;

/// <summary>Java parity: commons/network/AcceptDispatcherImpl (-Nemesiss-). Accept-only dispatcher.</summary>
public class AcceptDispatcherImpl : Dispatcher
{
    public AcceptDispatcherImpl(string name, Executor dcExecutor) : base(name, dcExecutor)
    {
    }

    internal override void Dispatch()
    {
        if (selector.Select() != 0)
        {
            ISet<SelectionKey> selectedKeys = selector.SelectedKeys();
            foreach (SelectionKey key in new List<SelectionKey>(selectedKeys))
            {
                selectedKeys.Remove(key);

                if (key.IsValid())
                    Accept(key);
            }
        }
    }

    internal override void CloseConnection(AConnection con)
    {
        // Java parity: commons/network/AcceptDispatcherImpl.java::closeConnection throws UnsupportedOperationException
        throw new NotSupportedException("This method should never be called!");
    }
}
