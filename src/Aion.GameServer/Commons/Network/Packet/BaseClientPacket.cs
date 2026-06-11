using System;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Aion.Commons.Lang;
using Aion.Commons.Nio;
using Aion.GameServer.Commons.Network;
using Aion.GameServer.Commons.Utils;

namespace Aion.GameServer.Commons.Network.Packet;

/// <summary>
/// Java parity: commons/network/packet/BaseClientPacket (-Nemesiss-). Base for every client packet.
/// &lt;T extends AConnection&lt;?&gt;&gt; -> where T : AConnection (the non-generic erasure base). implements
/// Runnable -> Runnable (Run() left abstract for AionClientPacket). BufferUnderflowException ->
/// catch (Exception) (the read helpers log and return a default on under-read). ConcurrentHashMap.newKeySet
/// -> ConcurrentDictionary used as a once-set.
/// </summary>
public abstract class BaseClientPacket<T> : BasePacket, Runnable where T : AConnection
{
    private static readonly ILogger log = NullLoggerFactory.Instance.CreateLogger("BaseClientPacket");
    private static readonly ConcurrentDictionary<int, bool> partiallyReadPackets = new ConcurrentDictionary<int, bool>();

    private T client = default!;
    private ByteBuffer buf = null!;

    public BaseClientPacket(int opcode) : this(null!, opcode)
    {
    }

    public BaseClientPacket(ByteBuffer buf, int opcode) : base(opcode)
    {
        this.buf = buf;
    }

    public void SetBuffer(ByteBuffer buf) => this.buf = buf;

    public void SetConnection(T client) => this.client = client;

    /// <summary>Reads data from the packet buffer. On error the connection is closed.</summary>
    public bool Read()
    {
        int startPos = buf.Position();
        try
        {
            ReadImpl();

            if (GetRemainingBytes() > 0 && partiallyReadPackets.TryAdd(GetOpCode(), true))
                log.LogWarning(this + " was not fully read! Last " + GetRemainingBytes() + " bytes were not read from buffer:\n" + NetworkUtils.ToHex(buf, startPos, buf.Limit()));

            return true;
        }
        catch (Exception ex)
        {
            string msg = "Reading failed for packet " + this + ". Buffer Info";
            if (GetRemainingBytes() > 0)
                msg += " (last " + GetRemainingBytes() + " bytes were not read)";
            msg += ":\n" + NetworkUtils.ToHex(buf, startPos, buf.Limit());
            log.LogError(ex, msg);
            return false;
        }
    }

    /// <summary>Data reading implementation.</summary>
    protected abstract void ReadImpl();

    public int GetRemainingBytes() => buf.Remaining();

    protected int ReadD()
    {
        try { return buf.GetInt(); }
        catch (Exception) { log.LogError("Missing D for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected byte ReadC()
    {
        try { return buf.Get(); }
        catch (Exception) { log.LogError("Missing C for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected int ReadUC()
    {
        try { return buf.Get() & 0xFF; }
        catch (Exception) { log.LogError("Missing C for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected short ReadH()
    {
        try { return buf.GetShort(); }
        catch (Exception) { log.LogError("Missing H for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected int ReadUH()
    {
        try { return buf.GetShort() & 0xFFFF; }
        catch (Exception) { log.LogError("Missing H for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected double ReadDF()
    {
        try { return buf.GetDouble(); }
        catch (Exception) { log.LogError("Missing DF for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected float ReadF()
    {
        try { return buf.GetFloat(); }
        catch (Exception) { log.LogError("Missing F for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected long ReadQ()
    {
        try { return buf.GetLong(); }
        catch (Exception) { log.LogError("Missing Q for: " + this + " (sent from " + client + ")"); }
        return 0;
    }

    protected string ReadS()
    {
        StringBuilder sb = new StringBuilder();
        char ch;
        try
        {
            while ((ch = buf.GetChar()) != 0)
                sb.Append(ch);
        }
        catch (Exception) { log.LogError("Missing S for: " + this + " (sent from " + client + ")"); }
        return sb.ToString();
    }

    protected byte[] ReadB(int length)
    {
        byte[] result = new byte[length];
        try { buf.Get(result); }
        catch (Exception) { log.LogError("Missing byte[] for: " + this + " (sent from " + client + ")"); }
        return result;
    }

    /// <summary>Execute this packet's action.</summary>
    protected abstract void RunImpl();

    /// <summary>java.lang.Runnable.run() — provided by AionClientPacket.</summary>
    public abstract void Run();

    public T GetConnection() => client;
}
