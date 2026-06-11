using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Concurrent;
using Aion.Commons.Nio;
using Aion.Commons.Nio.Channels;
using Aion.Commons.Options;
using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Commons.Network;

/// <summary>
/// Java parity: commons/network/Dispatcher (-Nemesiss-). Dispatches the SelectionKeys selected by a
/// Selector. Java <c>extends Thread</c> -> a backing System.Threading.Thread + Start()/Run(); java.nio
/// -> Aion.Commons.Nio.Channels shim; synchronized(gate) -> lock; assert -> Assertion.NetworkAssertion guard.
/// </summary>
public abstract class Dispatcher
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger(nameof(Dispatcher));

    private readonly Executor dcExecutor;
    protected internal Selector selector;
    private readonly object gate = new object();
    private readonly Thread thread;

    public Dispatcher(string name, Executor dcExecutor)
    {
        this.dcExecutor = dcExecutor;
        this.selector = SelectorProvider.Provider().OpenSelector();
        this.thread = new Thread(Run) { Name = name, IsBackground = true };
    }

    /// <summary>Java parity: Thread.start().</summary>
    public void Start() => thread.Start();

    internal abstract void CloseConnection(AConnection con);

    internal abstract void Dispatch();

    public Selector Selector() => selector;

    /// <summary>Java parity: Thread.run() — dispatch loop.</summary>
    public void Run()
    {
        for (;;)
        {
            try
            {
                Dispatch();

                lock (gate)
                {
                }
            }
            catch (Exception e)
            {
                log.LogError(e, "");
            }
        }
    }

    /// <summary>Register a client connection's channel; stores the resulting key on the connection.</summary>
    public void Register(SelectableChannel ch, int ops, AConnection att)
    {
        lock (gate)
        {
            selector.Wakeup();
            att.SetKey(ch.Register(selector, ops, att));
        }
    }

    /// <summary>Register an Acceptor's server channel; returns the resulting key.</summary>
    public SelectionKey Register(SelectableChannel ch, int ops, Acceptor att)
    {
        lock (gate)
        {
            selector.Wakeup();
            return ch.Register(selector, ops, att);
        }
    }

    /// <summary>Accept a new connection.</summary>
    internal void Accept(SelectionKey key)
    {
        try
        {
            ((Acceptor)key.Attachment()!).Accept(key);
        }
        catch (Exception e)
        {
            log.LogError(e, "");
        }
    }

    /// <summary>Read, parse and process data from the channel; prepare the buffer for the next read.</summary>
    internal void Read(SelectionKey key)
    {
        SocketChannel socketChannel = (SocketChannel)key.Channel();
        AConnection con = (AConnection)key.Attachment()!;

        ByteBuffer rb = con.readBuffer;

        int numRead;
        try
        {
            numRead = socketChannel.Read(rb);
        }
        catch (System.IO.IOException)
        {
            CloseConnectionImpl(con);
            return;
        }

        if (numRead == -1)
        {
            CloseConnectionImpl(con);
            return;
        }
        else if (numRead == 0)
        {
            return;
        }

        rb.Flip();
        while (rb.Remaining() > 2 && rb.Remaining() >= (rb.GetShort(rb.Position()) & 0xFFFF))
        {
            if (!Parse(con, rb))
            {
                CloseConnectionImpl(con);
                return;
            }
        }
        if (rb.HasRemaining())
        {
            rb.Compact();
        }
        else
        {
            rb.Clear();
        }
    }

    private bool Parse(AConnection con, ByteBuffer buf)
    {
        int size = (buf.GetShort() & 0xFFFF) - 2; // size includes the read short, subtract two bytes
        if (size <= 0)
        {
            log.LogWarning("Received empty packet without opcode from " + con + ", content:\n" + NetworkUtils.ToHex(buf));
            return false;
        }
        ByteBuffer b = buf.Slice().Order(buf.Order());
        try
        {
            b.SetLimit(size);
            buf.SetPosition(buf.Position() + size);

            return con.ProcessDataInternal(b);
        }
        catch (Exception e)
        {
            log.LogError(e, "Error parsing input from " + con + ", packet size: " + size);
            return false;
        }
    }

    /// <summary>Write as much pending data as possible; disable write interest when fully drained.</summary>
    internal void Write(SelectionKey key)
    {
        SocketChannel socketChannel = (SocketChannel)key.Channel();
        AConnection con = (AConnection)key.Attachment()!;

        int numWrite;
        ByteBuffer wb = con.writeBuffer;
        if (wb.HasRemaining())
        {
            try
            {
                numWrite = socketChannel.Write(wb);
            }
            catch (System.IO.IOException)
            {
                CloseConnectionImpl(con);
                return;
            }

            if (numWrite == 0)
            {
                log.LogInformation("Write " + numWrite + " ip: " + con.GetIP());
                return;
            }

            if (wb.HasRemaining())
                return;
        }

        while (true)
        {
            wb.Clear();
            bool writeFailed = !con.WriteDataInternal(wb);

            if (writeFailed)
            {
                wb.SetLimit(0);
                break;
            }

            try
            {
                numWrite = socketChannel.Write(wb);
            }
            catch (System.IO.IOException)
            {
                CloseConnectionImpl(con);
                return;
            }

            if (numWrite == 0)
            {
                log.LogInformation("Write " + numWrite + " ip: " + con.GetIP());
                return;
            }

            if (wb.HasRemaining())
                return;
        }

        // All data written; no longer interested in writing on this socket.
        key.InterestOps(key.InterestOps() & ~SelectionKey.OP_WRITE);
    }

    /// <summary>Close a connection. Dispatcher thread only.</summary>
    protected void CloseConnectionImpl(AConnection con)
    {
        if (Assertion.NetworkAssertion)
        {
            // assert Thread.currentThread() == this;
        }

        con.Disconnect(dcExecutor);
    }
}
