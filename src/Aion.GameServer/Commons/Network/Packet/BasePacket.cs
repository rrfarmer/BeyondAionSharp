namespace Aion.GameServer.Commons.Network.Packet;

/// <summary>
/// Basic superclass for packets. Java parity: commons/network/packet/BasePacket (Aquanox).
/// getClass().getSimpleName()→GetType().Name; String.format %0Nd→ToString("D"+N).
/// </summary>
public abstract class BasePacket
{
    private int opCode;

    /// <summary>Constructs a new packet. If this constructor is used, then SetOpCode() must be used just after it.</summary>
    protected BasePacket()
    {
    }

    protected BasePacket(int opCode)
    {
        this.opCode = opCode;
    }

    public int GetOpCode()
    {
        return opCode;
    }

    protected void SetOpCode(int opCode)
    {
        this.opCode = opCode;
    }

    /// <summary>Returns packet name (the simple name of the underlying class).</summary>
    public string GetPacketName()
    {
        return GetType().Name;
    }

    protected virtual int GetOpCodeZeroPadding()
    {
        return 3;
    }

    public string ToFormattedPacketNameString()
    {
        return ToFormattedPacketNameString(GetOpCodeZeroPadding(), GetOpCode(), GetPacketName());
    }

    public static string ToFormattedPacketNameString(int zeroPadding, int opcode, string packetName)
    {
        return "[" + opcode.ToString("D" + zeroPadding) + "] " + packetName;
    }

    public override string ToString()
    {
        return ToFormattedPacketNameString();
    }
}
