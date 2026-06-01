using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmHouseScript : GameClientPacket
{
	private const int StaticBodySize = 6;
	private const int DynamicHeaderSize = 11;
	private const int ScriptPaddingLength = 8;

	public const int MaxCompressedScriptSize =
		GameServerPacket.MaxUsablePacketBodySize - StaticBodySize - DynamicHeaderSize - ScriptPaddingLength;

	public CmHouseScript(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int Address { get; private set; }

	public int ScriptId { get; private set; }

	public int TotalSize { get; private set; }

	public int CompressedSize { get; private set; }

	public int UncompressedSize { get; private set; }

	public byte[] ScriptContent { get; private set; } = Array.Empty<byte>();

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_HOUSE_SCRIPT.readImpl.
		Address = buffer.ReadD();
		ScriptId = buffer.ReadC();
		TotalSize = buffer.ReadH();
		if (TotalSize <= 0)
			return;

		CompressedSize = buffer.ReadD();
		if (CompressedSize > MaxCompressedScriptSize)
			return;

		UncompressedSize = buffer.ReadD();
		ScriptContent = buffer.ReadB(CompressedSize);
	}
}
