using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Aion.Commons.Nio.Channels;

/// <summary>
/// Java parity shim: java.nio.channels selector model (SelectableChannel/SocketChannel/
/// ServerSocketChannel/SelectionKey/Selector/SelectorProvider) implemented over
/// System.Net.Sockets. This is the acknowledged unavoidable platform boundary: the faithful
/// commons.network reactor (AConnection/Dispatcher/Acceptor/NioServer) is ported 1:1 against this
/// surface, and the selector primitives map to Socket.Select. Signatures mirror the subset of the
/// JDK API the reactor uses.
/// </summary>
public abstract class SelectableChannel
{
    internal Socket socket = null!;
    internal bool blocking = true;
    internal readonly Dictionary<Selector, SelectionKey> keys = new();

    /// <summary>java.nio: SelectableChannel.configureBlocking(boolean).</summary>
    public SelectableChannel ConfigureBlocking(bool block)
    {
        blocking = block;
        if (socket != null)
            socket.Blocking = block;
        return this;
    }

    /// <summary>java.nio: register(Selector, int ops, Object att) -> SelectionKey.</summary>
    public SelectionKey Register(Selector sel, int ops, object? att)
    {
        if (keys.TryGetValue(sel, out SelectionKey? existing))
        {
            existing.SetInterestOps(ops);
            existing.Attach(att);
            return existing;
        }
        SelectionKey key = new SelectionKey(this, sel, ops, att);
        keys[sel] = key;
        sel.Add(key);
        return key;
    }

    /// <summary>java.nio: keyFor(Selector).</summary>
    public SelectionKey? KeyFor(Selector sel) => keys.TryGetValue(sel, out SelectionKey? k) ? k : null;

    internal Socket Sock => socket;
}

/// <summary>Java parity: java.net.Socket facade exposing the subset the reactor uses.</summary>
public sealed class JavaNetSocket
{
    private readonly Socket socket;
    internal JavaNetSocket(Socket socket) { this.socket = socket; }

    /// <summary>java.net.Socket.getInetAddress().</summary>
    public InetAddress GetInetAddress()
    {
        IPEndPoint? rep = socket.RemoteEndPoint as IPEndPoint;
        return new InetAddress(rep?.Address);
    }

    /// <summary>java.net.ServerSocket.bind(SocketAddress).</summary>
    public void Bind(EndPoint endpoint) => socket.Bind(endpoint);
}

/// <summary>Java parity: java.net.InetAddress facade (getHostAddress()).</summary>
public sealed class InetAddress
{
    private readonly IPAddress? address;
    internal InetAddress(IPAddress? address) { this.address = address; }

    /// <summary>java.net.InetAddress.getHostAddress() -> textual IP.</summary>
    public string GetHostAddress() => address?.ToString() ?? "0.0.0.0";
}

/// <summary>Java parity: java.nio.channels.SocketChannel over a connected System.Net.Sockets.Socket.</summary>
public sealed class SocketChannel : SelectableChannel
{
    private SocketChannel(Socket s) { socket = s; }

    /// <summary>java.nio: SocketChannel.open() (unconnected — used internally by accept()).</summary>
    public static SocketChannel Open(Socket accepted) => new SocketChannel(accepted);

    /// <summary>java.net facade.</summary>
    public JavaNetSocket Socket() => new JavaNetSocket(socket);

    /// <summary>java.nio: read(ByteBuffer) — reads into buffer at position, advances it; -1 on EOF.</summary>
    public int Read(ByteBuffer dst)
    {
        int rem = dst.Remaining();
        if (rem == 0)
            return 0;
        byte[] arr = dst.Array();
        int off = dst.ArrayOffset() + dst.Position();
        int n;
        try
        {
            n = socket.Receive(arr, off, rem, SocketFlags.None);
        }
        catch (SocketException)
        {
            throw new System.IO.IOException("socket read failed");
        }
        if (n == 0)
            return -1; // graceful remote close
        dst.SetPosition(dst.Position() + n);
        return n;
    }

    /// <summary>java.nio: write(ByteBuffer) — writes buffer's remaining bytes, advances position.</summary>
    public int Write(ByteBuffer src)
    {
        int rem = src.Remaining();
        if (rem == 0)
            return 0;
        byte[] arr = src.Array();
        int off = src.ArrayOffset() + src.Position();
        int n;
        try
        {
            n = socket.Send(arr, off, rem, SocketFlags.None);
        }
        catch (SocketException)
        {
            throw new System.IO.IOException("socket write failed");
        }
        src.SetPosition(src.Position() + n);
        return n;
    }

    public void Close() => socket.Close();
}

/// <summary>Java parity: java.nio.channels.ServerSocketChannel.</summary>
public sealed class ServerSocketChannel : SelectableChannel
{
    private ServerSocketChannel(Socket s) { socket = s; }

    /// <summary>java.nio: ServerSocketChannel.open().</summary>
    public static ServerSocketChannel Open()
    {
        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        return new ServerSocketChannel(s);
    }

    /// <summary>java.net facade (bind()).</summary>
    public JavaNetSocket Socket() => new JavaNetSocket(socket);

    /// <summary>java.nio: accept() -> SocketChannel for the new connection (null if none pending).</summary>
    public SocketChannel? Accept()
    {
        try
        {
            if (!blocking && !socket.Poll(0, SelectMode.SelectRead))
                return null;
            Socket accepted = socket.Accept();
            return SocketChannel.Open(accepted);
        }
        catch (SocketException)
        {
            return null;
        }
    }
}

/// <summary>
/// Java parity: java.nio.channels.SelectionKey. Registration handle binding a channel to a
/// selector with an interest set, a ready set, and an attachment.
/// </summary>
public sealed class SelectionKey
{
    public const int OP_READ = 1 << 0;   // 1
    public const int OP_WRITE = 1 << 2;  // 4
    public const int OP_CONNECT = 1 << 3; // 8
    public const int OP_ACCEPT = 1 << 4; // 16

    private readonly SelectableChannel channel;
    private readonly Selector selector;
    private int interestOps;
    internal int readyOpsValue;
    private object? attachment;
    private bool valid = true;

    internal SelectionKey(SelectableChannel channel, Selector selector, int ops, object? att)
    {
        this.channel = channel;
        this.selector = selector;
        this.interestOps = ops;
        this.attachment = att;
    }

    public SelectableChannel Channel() => channel;
    public Selector Selector() => selector;

    public int InterestOps() => interestOps;
    public SelectionKey InterestOps(int ops) { interestOps = ops; return this; }
    internal void SetInterestOps(int ops) => interestOps = ops;

    public int ReadyOps() => readyOpsValue;

    public bool IsValid() => valid;
    public bool IsReadable() => (readyOpsValue & OP_READ) != 0;
    public bool IsWritable() => (readyOpsValue & OP_WRITE) != 0;
    public bool IsAcceptable() => (readyOpsValue & OP_ACCEPT) != 0;
    public bool IsConnectable() => (readyOpsValue & OP_CONNECT) != 0;

    public object? Attachment() => attachment;
    public object? Attach(object? ob) { object? prev = attachment; attachment = ob; return prev; }

    public void Cancel()
    {
        valid = false;
        selector.Remove(this);
        channel.keys.Remove(selector);
    }
}

/// <summary>
/// Java parity: java.nio.channels.Selector over Socket.Select. select() blocks (with a short
/// timeout to remain interruptible by wakeup()) until one or more registered channels are ready.
/// </summary>
public sealed class Selector
{
    private readonly HashSet<SelectionKey> keys = new();
    private readonly HashSet<SelectionKey> selectedKeys = new();
    private volatile bool wakeupPending;
    private readonly object gate = new();

    internal void Add(SelectionKey key) { lock (gate) keys.Add(key); }
    internal void Remove(SelectionKey key) { lock (gate) { keys.Remove(key); selectedKeys.Remove(key); } }

    /// <summary>java.nio: Selector.keys() — full registered key set.</summary>
    public ISet<SelectionKey> Keys() { lock (gate) return new HashSet<SelectionKey>(keys); }

    /// <summary>java.nio: Selector.selectedKeys() — mutable ready set (caller removes processed keys).</summary>
    public ISet<SelectionKey> SelectedKeys() => selectedKeys;

    /// <summary>java.nio: Selector.wakeup() — cause a blocking select() to return promptly.</summary>
    public Selector Wakeup() { wakeupPending = true; return this; }

    /// <summary>java.nio: Selector.selectNow() — non-blocking.</summary>
    public int SelectNow() => DoSelect(0);

    /// <summary>java.nio: Selector.select() — blocks (bounded) until ready keys or wakeup.</summary>
    public int Select() => DoSelect(1000);

    private int DoSelect(int timeoutMillis)
    {
        if (wakeupPending)
        {
            wakeupPending = false;
            return 0;
        }
        List<SelectionKey> snapshot;
        lock (gate) snapshot = new List<SelectionKey>(keys);

        var readList = new List<Socket>();
        var writeList = new List<Socket>();
        var map = new Dictionary<Socket, SelectionKey>();
        foreach (SelectionKey k in snapshot)
        {
            Socket s = ((SelectableChannel)k.Channel()).Sock;
            if (s == null) continue;
            map[s] = k;
            int ops = k.InterestOps();
            if ((ops & (SelectionKey.OP_READ | SelectionKey.OP_ACCEPT)) != 0) readList.Add(s);
            if ((ops & SelectionKey.OP_WRITE) != 0) writeList.Add(s);
        }
        if (readList.Count == 0 && writeList.Count == 0)
        {
            if (timeoutMillis > 0) System.Threading.Thread.Sleep(Math.Min(timeoutMillis, 50));
            return 0;
        }

        var rr = new List<Socket>(readList);
        var ww = new List<Socket>(writeList);
        try
        {
            Socket.Select(rr.Count > 0 ? rr : null, ww.Count > 0 ? ww : null, null, timeoutMillis <= 0 ? 0 : timeoutMillis * 1000);
        }
        catch (SocketException)
        {
            return 0;
        }

        selectedKeys.Clear();
        foreach (Socket s in rr)
            if (map.TryGetValue(s, out SelectionKey? k))
            {
                int interest = k.InterestOps();
                k.readyOpsValue = interest & (SelectionKey.OP_READ | SelectionKey.OP_ACCEPT);
                selectedKeys.Add(k);
            }
        foreach (Socket s in ww)
            if (map.TryGetValue(s, out SelectionKey? k))
            {
                k.readyOpsValue |= k.InterestOps() & SelectionKey.OP_WRITE;
                selectedKeys.Add(k);
            }
        return selectedKeys.Count;
    }

    public void Close() { lock (gate) { keys.Clear(); selectedKeys.Clear(); } }
}

/// <summary>Java parity: java.nio.channels.spi.SelectorProvider.</summary>
public sealed class SelectorProvider
{
    private static readonly SelectorProvider instance = new SelectorProvider();

    /// <summary>java.nio: SelectorProvider.provider().</summary>
    public static SelectorProvider Provider() => instance;

    /// <summary>java.nio: openSelector().</summary>
    public Selector OpenSelector() => new Selector();
}
