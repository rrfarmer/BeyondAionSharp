using System;
using System.Collections.Generic;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Utils.ChatHandlers;

namespace Aion.GameServer.Network.Aion.ServerPackets;

/// <summary>
/// Java parity: network/aion/serverpackets/SM_CUSTOM_PACKET (Luno). Admin //fsc fake-packet builder.
/// PacketElementType is a Java enum with per-constant abstract write() bodies -> abstract nested class
/// with static-readonly nested subclass instances (nested so they can reach the packet's protected WriteX).
/// Numeric element values use the same lexical, radix, and overflow rules as their Java wrapper methods.
/// </summary>
public class SM_CUSTOM_PACKET : AionServerPacket
{
    // Java parity (writeImpl audited 1:1 vs game-server/.../serverpackets/SM_CUSTOM_PACKET.java): 2026-06-17. admin //fsc builder, no plain-value ctor -> T2 audit; writeImpl iterates elements, each PacketElementType.write byte-identical (d/h/c via Integer.decode, b via Integer.valueOf decimal, q via Long.decode, f/e Float/Double, s writeS); con never read.
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
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteD(JavaNumberParser.DecodeInt(value));
        }

        private sealed class BElement : PacketElementType
        {
            public BElement() : base('b') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteB(new byte[JavaNumberParser.ParseInt(value)]);
        }

        private sealed class HElement : PacketElementType
        {
            public HElement() : base('h') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteH(JavaNumberParser.DecodeInt(value));
        }

        private sealed class CElement : PacketElementType
        {
            public CElement() : base('c') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteC(JavaNumberParser.DecodeInt(value));
        }

        private sealed class FElement : PacketElementType
        {
            public FElement() : base('f') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteF(JavaNumberParser.ParseFloat(value));
        }

        private sealed class DFElement : PacketElementType
        {
            public DFElement() : base('e') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteDF(JavaNumberParser.ParseDouble(value));
        }

        private sealed class QElement : PacketElementType
        {
            public QElement() : base('q') { }
            public override void Write(SM_CUSTOM_PACKET packet, string value) => packet.WriteQ(JavaNumberParser.DecodeLong(value));
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

}
