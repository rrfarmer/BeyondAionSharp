using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmDialogSelect : GameClientPacket
{
	public const int Buy = 2;
	public const int Recovery = 35;
	public const int ExtendInventory = 47;
	public const int ExtendCharWarehouse = 48;
	public const int CombineTask = 58;
	public const int BuyAgain = 70;
	public const int ChargeItemMulti = 76;
	public const int TradeIn = 78;
	public const int ChargeItemMulti2 = 95;
	public const int OpenInstanceRecruit = 105;
	public const int Select1_1 = 1012;

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
