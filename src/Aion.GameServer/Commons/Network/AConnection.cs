using System;
using System.Collections.Generic;
using Aion.Commons.Concurrent;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.Commons.Options;
using Aion.GameServer.Commons.Network.Packet;

namespace Aion.GameServer.Commons.Network;

/// <summary>
/// Java parity: commons/network/AConnection (-Nemesiss-). Non-generic base holding the socket-layer
/// surface the Dispatcher/NioServer operate on via Java's wildcard <c>AConnection&lt;?&gt;</c> (C# has no
/// wildcard, so wildcard-typed members live here and packet-typed members in AConnection&lt;T&gt;).
/// java.nio.channels -> Aion.Commons.Nio.Channels shim; synchronized -> lock; currentTimeMillis ->
/// UtcNow.ToUnixTimeMilliseconds; Executor.execute(this::onDisconnect) -> Execute(new LambdaRunnable).
/// </summary>
public abstract class AConnection
{
    protected readonly SocketChannel socketChannel;
    protected readonly Dispatcher dispatcher;
    protected SelectionKey key = null!;
    public long pendingCloseUntilMillis;
    protected bool closed;
    protected readonly object guard = new object();
    public readonly ByteBuffer writeBuffer;
    public readonly ByteBuffer readBuffer;
    private readonly string ip;
    private bool locked = false;

    protected AConnection(SocketChannel sc, Dispatcher d, int rbSize, int wbSize)
    {
        socketChannel = sc;
        dispatcher = d;
        writeBuffer = ByteBuffer.Allocate(wbSize);
        writeBuffer.Flip();
        writeBuffer.Order(ByteOrder.LITTLE_ENDIAN);
        readBuffer = ByteBuffer.Allocate(rbSize);
        readBuffer.Order(ByteOrder.LITTLE_ENDIAN);

        this.ip = socketChannel.Socket().GetInetAddress().GetHostAddress();
    }

    internal void SetKey(SelectionKey key) => this.key = key;

    public SocketChannel GetSocketChannel() => socketChannel;

    /// <summary>Closes the connection and calls onDisconnect() on another thread. Dispatcher thread only.</summary>
    internal void Disconnect(Executor dcExecutor)
    {
        if (Assertion.NetworkAssertion)
        {
            // assert Thread.currentThread() == dispatcher;
        }

        lock (guard)
        {
            if (closed)
                return;
            closed = true;
        }

        key.Cancel();
        try
        {
            socketChannel.Close();
        }
        catch (System.IO.IOException)
        {
        }
        key.Attach(null);

        dcExecutor.Execute(new LambdaRunnable(OnDisconnect));
    }

    public bool IsConnected() => key.IsValid();

    public bool IsPendingClose() => pendingCloseUntilMillis != 0 && !closed;

    public bool IsClosed() => closed;

    public string GetIP() => ip;

    internal bool TryLockConnection()
    {
        if (locked)
            return false;
        return locked = true;
    }

    internal void UnlockConnection() => locked = false;

    /// <summary>Closes this connection (regular, no close packet).</summary>
    public abstract void Close();

    /// <summary>True if there are no pending outgoing packets (wildcard-safe accessor for the send queue).</summary>
    internal abstract bool IsSendQueueEmpty();

    protected abstract bool ProcessData(ByteBuffer data);
    internal bool ProcessDataInternal(ByteBuffer data) => ProcessData(data);

    protected abstract bool WriteData(ByteBuffer data);
    internal bool WriteDataInternal(ByteBuffer data) => WriteData(data);

    protected abstract void Initialized();
    internal void InitializedInternal() => Initialized();

    protected abstract void OnDisconnect();

    public abstract void OnServerClose();
}

/// <summary>
/// Java parity: AConnection&lt;T extends BaseServerPacket&gt; — adds the packet-typed send/close surface.
/// </summary>
public abstract class AConnection<T> : AConnection where T : BaseServerPacket
{
    protected AConnection(SocketChannel sc, Dispatcher d, int rbSize, int wbSize) : base(sc, d, rbSize, wbSize)
    {
    }

    /// <summary>Sends the ServerPacket to this client.</summary>
    public void SendPacket(T serverPacket)
    {
        lock (guard)
        {
            if (pendingCloseUntilMillis != 0 || closed)
                return;

            if (IsConnected())
            {
                GetSendMsgQueue().Enqueue(serverPacket);
                key.InterestOps(key.InterestOps() | SelectionKey.OP_WRITE);
                key.Selector().Wakeup();
            }
            else
            {
                Close();
            }
        }
    }

    public override void Close() => Close(null);

    public virtual void Close(T? closePacket)
    {
        lock (guard)
        {
            if (pendingCloseUntilMillis != 0 || closed)
                return;

            pendingCloseUntilMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 2000;
            if (closePacket != null || !IsConnected())
                GetSendMsgQueue().Clear();
            if (closePacket != null && IsConnected())
            {
                GetSendMsgQueue().Enqueue(closePacket);
                key.InterestOps(SelectionKey.OP_WRITE);
            }
            dispatcher.CloseConnection(this);
            key.Selector().Wakeup(); // notify dispatcher
        }
    }

    internal override bool IsSendQueueEmpty() => GetSendMsgQueue().Count == 0;

    protected abstract Queue<T> GetSendMsgQueue();
}
