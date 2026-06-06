using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ClientPackets;

public sealed class CmDialogSelect : GameClientPacket
{
	public const int UseObject = 0xFFFF; // Java DialogAction.USE_OBJECT is -1; CM_DIALOG_SELECT reads it as unsigned H.
	public const int Buy = 2;
	public const int QuestAccept = 29;
	public const int QuestSelect = 31;
	public const int Recovery = 35;
	public const int CheckUserHasQuestItem = 39;
	public const int ExtendInventory = 47;
	public const int ExtendCharWarehouse = 48;
	public const int OpenLegionWarehouse = 53;
	public const int CombineTask = 58;
	public const int InstanceEntry = 65;
	public const int BuyAgain = 70;
	public const int ChargeItemMulti = 76;
	public const int InstancePartyMatch = 77;
	public const int TradeIn = 78;
	public const int ChargeItemMulti2 = 95;
	public const int OpenInstanceRecruit = 105;
	public const int QuestAccept1 = 1002;
	public const int QuestRefuse1 = 1003;
	public const int QuestRefuse2 = 1004;
	public const int AskQuestAccept = 1007;
	public const int FinishDialog = 1008;
	public const int SelectQuestReward = 1009;
	public const int Select1_1 = 1012;
	public const int SelectNone1 = 4763;
	public const int SelectNone2 = 4848;
	public const int SetSucceed = 10255;
	public const int QuestAcceptSimple = 20000;
	public const int QuestRefuseSimple = 20001;
	public const int CheckUserHasQuestItemSimple = 20002;

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
