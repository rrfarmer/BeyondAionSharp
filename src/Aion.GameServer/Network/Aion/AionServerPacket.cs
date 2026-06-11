using Aion.Commons.Nio;
using Aion.GameServer.Commons.Network.Packet;
using Aion.GameServer.Network;
using Aion.GameServer.Network.Aion.Capture;

namespace Aion.GameServer.Network.Aion;

/// <summary>
/// Java parity: network/aion/AionServerPacket (-Nemesiss-). Base for every GS -> Aion server packet.
/// Extends the commons BaseServerPacket mirror; ctor assigns the opcode from ServerPacketsOpcodes;
/// write() frames + encrypts via the connection's Crypt. java.nio buf ops -> Aion.Commons.Nio.ByteBuffer.
/// </summary>
public abstract class AionServerPacket : BaseServerPacket
{
    public const int MAX_CLIENT_SUPPORTED_PACKET_SIZE = 8192;
    public const int MAX_USABLE_PACKET_BODY_SIZE = MAX_CLIENT_SUPPORTED_PACKET_SIZE - 7;
    private static volatile ServerPacketCaptureObserver captureObserver = NoOpServerPacketCaptureObserver.INSTANCE;

    public static int ByteLengthForString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 2;
        return (text.Length + 1) * 2;
    }

    public static int ByteLengthForFixedString(int fixedLength) => (fixedLength + 1) * 2;

    protected AionServerPacket() : base()
    {
        SetOpCode(ServerPacketsOpcodes.GetOpcode(GetType()));
    }

    protected AionServerPacket(int opCode) : base(opCode)
    {
    }

    public static void SetCaptureObserver(ServerPacketCaptureObserver observer)
    {
        captureObserver = observer ?? NoOpServerPacketCaptureObserver.INSTANCE;
    }

    /// <summary>Write packet opCode and two additional bytes.</summary>
    private void WriteOP()
    {
        int op = Crypt.EncodeServerPacketOpcode(GetOpCode());
        buf.PutShort((short)op);
        buf.Put(Crypt.staticServerPacketCode);
        buf.PutShort((short)(~op));
    }

    /// <summary>Write and encrypt this packet's data for the given connection, into the given buffer.</summary>
    public void Write(AionConnection con, ByteBuffer buffer)
    {
        SetBuf(buffer);
        buf.PutShort((short)0);
        WriteOP();
        WriteImpl(con);
        buf.Flip();
        buf.PutShort((short)buf.Limit());
        ServerPacketCaptureObserver observer = captureObserver;
        if (observer.IsEnabled())
            observer.OnPacketSerialized(con, this, buf.AsReadOnlyBuffer());
        ByteBuffer b = buf.Slice();
        buf.SetPosition(0);
        con.Encrypt(b);
    }

    /// <summary>Write the data this packet represents to the given buffer.</summary>
    protected virtual void WriteImpl(AionConnection con)
    {
    }

    public ByteBuffer GetBuf() => this.buf;

    /// <summary>
    /// Write a fixed-length string: exceeding chars truncated, missing ones zero-padded. Writes
    /// fixedLength*2 + 2 bytes (the trailing terminating char the client always requires).
    /// </summary>
    protected void WriteS(string? text, int fixedLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            buf.Put(new byte[ByteLengthForFixedString(fixedLength)]);
        }
        else
        {
            for (int i = 0; i < fixedLength; i++)
                buf.PutChar(i < text.Length ? text[i] : '\0');
            buf.PutChar('\0');
        }
    }

    /// <summary>Writes dye information (dye status + 3 byte RGB value). rgb may be null.</summary>
    protected void WriteDyeInfo(int? rgb)
    {
        if (rgb == null)
        {
            WriteB(new byte[4]);
        }
        else
        {
            WriteC(1); // dye status (1 = dyed)
            WriteC((rgb.Value & 0xFF0000) >> 16); // r
            WriteC((rgb.Value & 0xFF00) >> 8); // g
            WriteC(rgb.Value & 0xFF); // b
        }
    }

    protected override int GetOpCodeZeroPadding() => 3;
}
