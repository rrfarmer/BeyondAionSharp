using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDialogWindow : GameServerPacket
{
	public const int PacketOpCode = 60;
	public const int MailPageId = 18;
	public const int LegionWarehousePageId = 25;
	public const int NoRightPageId = 27;

	private readonly int _targetObjectId;
	private readonly int _dialogPageId;
	private readonly int _questId;
	private readonly int _dialogContextId;

	public SmDialogWindow(int targetObjectId, int dialogPageId, int questId = 0, int dialogContextId = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_DIALOG_WINDOW.
		_targetObjectId = targetObjectId;
		_dialogPageId = dialogPageId;
		_questId = questId;
		_dialogContextId = dialogContextId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_targetObjectId);
		buffer.WriteH(_dialogPageId);
		buffer.WriteD(_questId);
		buffer.WriteH(0);
		buffer.WriteH(_dialogContextId);
	}
}
