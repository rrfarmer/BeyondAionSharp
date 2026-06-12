using System.Buffers.Binary;
using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion;

public abstract class GameServerPacket : AionServerPacket
{
	public const int MaxClientSupportedPacketSize = 8192;
	public const int MaxUsablePacketBodySize = MaxClientSupportedPacketSize - 7;
	private const byte StaticServerPacketCode = 0x44;

	protected GameServerPacket(int opCode) : base(opCode)
	{
		OpCode = opCode;
	}

	public new int OpCode { get; }

	public byte[] SerializeFrame(GameCrypt crypt)
	{
		// Java parity: network/aion/AionServerPacket.write with NCSoft static packet header.
		using var buffer = new PacketBuffer(MaxClientSupportedPacketSize);
		buffer.WriteH(0);
		WriteOpCode(buffer);
		WritePayload(buffer, crypt);

		var frame = buffer.ToArray();
		BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(0, 2), (ushort)frame.Length);
		crypt.EncryptServerPayload(frame.AsSpan(2));
		return frame;
	}

	protected abstract void WritePayload(PacketBuffer buffer, GameCrypt crypt);

	private void WriteOpCode(PacketBuffer buffer)
	{
		// Java parity: network/aion/ServerPacketsOpcodes encoded opcode header.
		var encodedOpCode = GameCrypt.EncodeServerPacketOpcode(OpCode);
		buffer.WriteH(encodedOpCode);
		buffer.WriteC(StaticServerPacketCode);
		buffer.WriteH(~encodedOpCode);
	}
}
