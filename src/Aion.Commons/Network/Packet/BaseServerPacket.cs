using Aion.Commons.Nio;

namespace Aion.Commons.Network.Packet
{
    /// <summary>
    /// Java parity: commons/network/packet/BaseServerPacket (@author -Nemesiss-). Base class for every server packet.
    /// java.nio.ByteBuffer -> Aion.Commons.Nio.ByteBuffer; put* -> Put*; writeS writes UTF-16 chars + NUL terminator via PutChar('\0').
    /// Java `final` write helpers -> plain non-virtual C# methods.
    /// </summary>
    public abstract class BaseServerPacket : BasePacket
    {
        /// <summary>ByteBuffer that contains this packet data</summary>
        public ByteBuffer buf;

        /// <summary>Constructs a new server packet. If this constructor was used, then SetOpCode(int) must be called.</summary>
        protected BaseServerPacket()
            : base()
        {
        }

        /// <summary>Constructs a new server packet with specified id.</summary>
        protected BaseServerPacket(int opCode)
            : base(opCode)
        {
        }

        /// <summary>Sets the buf.</summary>
        public void SetBuf(ByteBuffer buf)
        {
            this.buf = buf;
        }

        /// <summary>Write int to buffer.</summary>
        protected void WriteD(int value)
        {
            buf.PutInt(value);
        }

        /// <summary>Write short to buffer.</summary>
        protected void WriteH(int value)
        {
            buf.PutShort((short)value);
        }

        /// <summary>Write byte to buffer.</summary>
        protected void WriteC(int value)
        {
            buf.Put((byte)value);
        }

        /// <summary>Write byte to buffer.</summary>
        protected void WriteC(byte value)
        {
            buf.Put(value);
        }

        /// <summary>Write double to buffer.</summary>
        protected void WriteDF(double value)
        {
            buf.PutDouble(value);
        }

        /// <summary>Write float to buffer.</summary>
        protected void WriteF(float value)
        {
            buf.PutFloat(value);
        }

        /// <summary>Write long to buffer.</summary>
        protected void WriteQ(long value)
        {
            buf.PutLong(value);
        }

        /// <summary>Write String to buffer.</summary>
        protected void WriteS(string text)
        {
            if (text == null)
            {
                buf.PutChar('\0');
            }
            else
            {
                int len = text.Length;
                for (int i = 0; i < len; i++)
                    buf.PutChar(text[i]);
                buf.PutChar('\0');
            }
        }

        /// <summary>Write byte array to buffer.</summary>
        protected void WriteB(byte[] data)
        {
            buf.Put(data);
        }
    }
}
