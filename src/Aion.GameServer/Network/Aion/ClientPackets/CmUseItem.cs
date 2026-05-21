using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmUseItem : GameClientPacket
{
	public CmUseItem(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int SourceItemObjectId { get; private set; }

	public byte Type { get; private set; }

	public int TargetItemObjectId { get; private set; }

	public int SyncId { get; private set; }

	public int IndexReturn { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_USE_ITEM.readImpl.
		SourceItemObjectId = buffer.ReadD();
		Type = buffer.ReadC();
		switch (Type)
		{
			case 2:
				TargetItemObjectId = buffer.ReadD();
				break;
			case 5:
				SyncId = buffer.ReadD();
				break;
			case 6:
				IndexReturn = buffer.ReadD();
				break;
		}
	}
}
