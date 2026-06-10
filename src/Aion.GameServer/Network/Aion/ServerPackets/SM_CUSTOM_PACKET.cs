using System;
using System.Collections.Generic;
using System.Globalization;
using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Network.Aion.Serverpackets;

/// <summary>
/// Java parity: network/aion/serverpackets/SM_CUSTOM_PACKET (Luno). Admin //fsc fake-packet builder.
/// PacketElementType is a Java enum with per-constant abstract write() bodies -> abstract nested class
/// with static-readonly nested subclass instances (nested so they can reach the packet's protected WriteX).
/// Integer.decode/Long.decode -> Decode/LongDecode (0x/#/0-prefix aware); Float/Double.valueOf -> Parse(InvariantCulture).
/// </summary>
public class SM_CUSTOM_PACKET : AionServerPacket
{
    /// <summary>Enumeration of types of packet elements.</summary>
    public abstract class PacketElementType
    {
        public static readonly PacketElementType D = new DElement();
        public static readonly PacketElementType B = new BElement();
        public static readonly PacketElementType H = new HElement();
        public static readonly PacketElementType C = new CElement();
        public static readonly PacketElementType F = new FElement();
        public static readonly PacketElementType DF = new DFElement();
        public static readonly PacketElementType Q = new QElement();
        public static readonly PacketElementType S = new SElement();

        private readonly char code;

        protected PacketElementType(char code)
        {
            this.code = code;
        }

        public static PacketElementType[] Values()
        {
            return new[] { D, B, H, C, F, DF, Q, S };
        }

        public static PacketElementType GetByCode(char code)
        {
            foreach (PacketElementType type in Values())
                if (type.code == code)
                    return type;
            return null;
        }

        /// <summary>Writes <c>value</c> to buffer according to the ElementType.</summary>
        public abstract void Write(SM_CUSTOM_PACKET packet, string value);

        private sealed class DElement : PacketElementType
        {
            public DElement() : base('d') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteD(Decode(value));
        }

        private sealed class BElement : PacketElementType
        {
            public BElement() : base('b') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteB(new byte[int.Parse(value)]);
        }

        private sealed class HElement : PacketElementType
        {
            public HElement() : base('h') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteH(Decode(value));
        }

        private sealed class CElement : PacketElementType
        {
            public CElement() : base('c') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteC(Decode(value));
        }

        private sealed class FElement : PacketElementType
        {
            public FElement() : base('f') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteF(float.Parse(value, CultureInfo.InvariantCulture));
        }

        private sealed class DFElement : PacketElementType
        {
            public DFElement() : base('e') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteDF(double.Parse(value, CultureInfo.InvariantCulture));
        }

        private sealed class QElement : PacketElementType
        {
            public QElement() : base('q') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteQ(LongDecode(value));
        }

        private sealed class SElement : PacketElementType
        {
            public SElement() : base('s') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteS(value);
        }
    }

    public class PacketElement
    {
        private readonly PacketElementType type;
        private readonly string value;

        public PacketElement(PacketElementType type, string value)
        {
            this.type = type;
            this.value = value;
        }

        /// <summary>Writes value stored in this PacketElement into the packet buffer.</summary>
        public void WriteValue(SM_CUSTOM_PACKET packet)
        {
            type.Write(packet, value);
        }
    }

    private List<PacketElement> elements = new List<PacketElement>();

    public SM_CUSTOM_PACKET(int opcode)
        : base(opcode)
    {
    }

    /// <summary>Add an element to this packet.</summary>
    public void AddElement(PacketElement packetElement)
    {
        elements.Add(packetElement);
    }

    /// <summary>Add packet element.</summary>
    public void AddElement(PacketElementType type, string value)
    {
        elements.Add(new PacketElement(type, value));
    }

    protected override void WriteImpl(AionConnection con)
    {
        foreach (PacketElement el in elements)
        {
            el.WriteValue(this);
        }
    }

    // Java parity: Integer.decode(String) — 0x/0X/# hex, leading 0 octal, else decimal (sign-aware).
    private static int Decode(string value)
    {
        value = value.Trim();
        bool neg = value.StartsWith("-");
        string body = neg ? value.Substring(1) : value;
        int result;
        if (body.StartsWith("0x") || body.StartsWith("0X"))
            result = Convert.ToInt32(body.Substring(2), 16);
        else if (body.StartsWith("#"))
            result = Convert.ToInt32(body.Substring(1), 16);
        else if (body.Length > 1 && body.StartsWith("0"))
            result = Convert.ToInt32(body, 8);
        else
            result = int.Parse(body);
        return neg ? -result : result;
    }

    private static long LongDecode(string value)
    {
        value = value.Trim();
        bool neg = value.StartsWith("-");
        string body = neg ? value.Substring(1) : value;
        long result;
        if (body.StartsWith("0x") || body.StartsWith("0X"))
            result = Convert.ToInt64(body.Substring(2), 16);
        else if (body.StartsWith("#"))
            result = Convert.ToInt64(body.Substring(1), 16);
        else if (body.Length > 1 && body.StartsWith("0"))
            result = Convert.ToInt64(body, 8);
        else
            result = long.Parse(body);
        return neg ? -result : result;
    }
}
