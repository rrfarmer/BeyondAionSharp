using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmManastone : GameClientPacket
{
	public CmManastone(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public byte ActionType { get; private set; }

	public byte TargetFusedSlot { get; private set; }

	public int TargetItemObjectId { get; private set; }

	public int StoneObjectId { get; private set; }

	public int SupplementObjectId { get; private set; }

	public byte SlotNumber { get; private set; }

	public int NpcObjectId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_MANASTONE.readImpl.
		ActionType = buffer.ReadC();
		TargetFusedSlot = buffer.ReadC();
		TargetItemObjectId = buffer.ReadD();
		switch (ActionType)
		{
			case 1:
			case 2:
			case 4:
			case 8:
				StoneObjectId = buffer.ReadD();
				SupplementObjectId = buffer.ReadD();
				break;
			case 3:
				SlotNumber = buffer.ReadC();
				buffer.ReadC();
				buffer.ReadH();
				NpcObjectId = buffer.ReadD();
				break;
		}
	}
}
