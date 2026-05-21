using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmDialogSelect : GameClientPacket
{
	public const int ChargeItemMulti = 76;
	public const int ChargeItemMulti2 = 95;

	public CmDialogSelect(int opCode, IReadOnlySet<GameConnectionState> validStates)
		: base(opCode, validStates)
	{
	}

	public int TargetObjectId { get; private set; }

	public int DialogActionId { get; private set; }

	public int ExtendedRewardIndex { get; private set; }

	public int LastPage { get; private set; }

	public int QuestId { get; private set; }

	protected override void ReadPayload(PacketBuffer buffer)
	{
		// Java parity: network/aion/clientpackets/CM_DIALOG_SELECT.readImpl.
		TargetObjectId = buffer.ReadD();
		DialogActionId = buffer.ReadH();
		ExtendedRewardIndex = buffer.ReadH();
		LastPage = buffer.ReadH();
		QuestId = buffer.ReadD();
		buffer.ReadH();
	}
}
