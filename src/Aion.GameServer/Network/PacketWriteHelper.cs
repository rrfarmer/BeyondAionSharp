using Aion.Commons.Nio;

namespace Aion.GameServer.Network;

/// <summary>Java parity: network/PacketWriteHelper (-Nemesiss-). Base for buffer-writing helpers (writeD/H/C/F/Q/S/B/DF/skip/dyeInfo over java.nio ByteBuffer). Integer rgb -> int?.</summary>
public abstract class PacketWriteHelper
{
    protected abstract void WriteMe(ByteBuffer buf);

    /// <summary>Write int to buffer.</summary>
    protected static void WriteD(ByteBuffer buf, int value)
    {
        buf.PutInt(value);
    }

    /// <summary>Write short to buffer.</summary>
    protected static void WriteH(ByteBuffer buf, int value)
    {
        buf.PutShort((short)value);
    }

    /// <summary>Write byte to buffer.</summary>
    protected static void WriteC(ByteBuffer buf, int value)
    {
        buf.Put((byte)value);
    }

    /// <summary>Write double to buffer.</summary>
    protected static void WriteDF(ByteBuffer buf, double value)
    {
        buf.PutDouble(value);
    }

    /// <summary>Write float to buffer.</summary>
    protected static void WriteF(ByteBuffer buf, float value)
    {
        buf.PutFloat(value);
    }

    /// <summary>Write long to buffer.</summary>
    protected static void WriteQ(ByteBuffer buf, long value)
    {
        buf.PutLong(value);
    }

    /// <summary>Write String to buffer.</summary>
    protected static void WriteS(ByteBuffer buf, string text)
    {
        if (text == null)
        {
            buf.PutChar((char)0);
        }
        else
        {
            int len = text.Length;
            for (int i = 0; i < len; i++)
                buf.PutChar(text[i]);
            buf.PutChar((char)0);
        }
    }

    /// <summary>Write String to buffer (fixed size).</summary>
    protected static void WriteS(ByteBuffer buf, string text, int size)
    {
        if (text == null)
        {
            buf.Put(new byte[size]);
        }
        else
        {
            int len = text.Length;
            for (int i = 0; i < len; i++)
                buf.PutChar(text[i]);
            buf.Put(new byte[size - (len * 2)]);
        }
    }

    /// <summary>Write byte array to buffer.</summary>
    protected static void WriteB(ByteBuffer buf, byte[] data)
    {
        buf.Put(data);
    }

    /// <summary>Skip specified amount of bytes.</summary>
    protected static void Skip(ByteBuffer buf, int bytes)
    {
        buf.Put(new byte[bytes]);
    }

    /// <summary>See AionServerPacket.writeDyeInfo(Integer rgb).</summary>
    protected static void WriteDyeInfo(ByteBuffer buf, int? rgb)
    {
        if (rgb == null)
        {
            Skip(buf, 4);
        }
        else
        {
            WriteC(buf, 1); // dye status (1 = dyed, 0 = not dyed)
            WriteC(buf, (rgb.Value & 0xFF0000) >> 16); // r
            WriteC(buf, (rgb.Value & 0xFF00) >> 8); // g
            WriteC(buf, rgb.Value & 0xFF); // b
        }
    }
}
