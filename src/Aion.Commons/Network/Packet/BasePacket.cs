namespace Aion.Commons.Network.Packet
{
    /// <summary>
    /// Java parity: commons/network/packet/BasePacket (@author Aquanox). Basic superclass for packets.
    /// getClass().getSimpleName() -> GetType().Name; String.format("[%0Nd] %s") -> string.Format with a "{0:D&lt;N&gt;}" composite
    /// format built from the zero-padding width. Java `final` instance methods -> plain non-virtual C# methods; the overridable
    /// getOpCodeZeroPadding -> protected virtual.
    /// </summary>
    public abstract class BasePacket
    {
        /// <summary>Packet opCode field</summary>
        private int opCode;

        /// <summary>
        /// Constructs a new packet. If this constructor is used, then SetOpCode() must be used just after it.
        /// </summary>
        protected BasePacket()
        {
        }

        /// <summary>Constructs a new packet with specified id.</summary>
        protected BasePacket(int opCode)
        {
            this.opCode = opCode;
        }

        /// <summary>Returns packet opCode.</summary>
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
            return string.Format("[{0:D" + zeroPadding + "}] {1}", opcode, packetName);
        }

        /// <summary>Returns string representation of this packet based on opCode and name.</summary>
        public override string ToString()
        {
            return ToFormattedPacketNameString();
        }
    }
}
