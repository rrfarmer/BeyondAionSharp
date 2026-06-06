using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmLegionModifyEmblem : GameClientPacket
{
	public CmLegionModifyEmblem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int LegionId { get; private set; }
	public byte EmblemId { get; private set; }
	public byte EmblemType { get; private set; }
	public byte Alpha { get; private set; }
	public byte Red { get; private set; }
	public byte Green { get; private set; }
	public byte Blue { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_LEGION_MODIFY_EMBLEM.readImpl.
		LegionId = buffer.ReadD();
		EmblemId = buffer.ReadC();
		EmblemType = buffer.ReadC() == 0x00 ? (byte)0x00 : (byte)0x80;
		Alpha = buffer.ReadC();
		Red = buffer.ReadC();
		Green = buffer.ReadC();
		Blue = buffer.ReadC();
	}
}
