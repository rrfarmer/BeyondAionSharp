using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmCloseQuestionWindow : GameServerPacket
{
	public const int PacketOpCode = 53;
	private const int MaxParameterCount = 3;

	private readonly int _messageId;
	private readonly IReadOnlyList<string> _parameters;

	private SmCloseQuestionWindow(int messageId, params string[] parameters)
		: base(PacketOpCode)
	{
		_messageId = messageId;
		_parameters = parameters;
	}

	public int MessageId => _messageId;

	public static SmCloseQuestionWindow DuelRequesterWithdrawRequest(string playerName)
	{
		// Java parity: SM_CLOSE_QUESTION_WINDOW.STR_DUEL_REQUESTER_WITHDRAW_REQUEST(String).
		return new SmCloseQuestionWindow(1300134, playerName);
	}

	public static SmCloseQuestionWindow DuelHeRejectDuel(string playerName)
	{
		// Java parity: SM_CLOSE_QUESTION_WINDOW.STR_DUEL_HE_REJECT_DUEL(String).
		return new SmCloseQuestionWindow(1300097, playerName);
	}

	public static SmCloseQuestionWindow CloseQuestionWindow()
	{
		// Java parity: SM_CLOSE_QUESTION_WINDOW.CLOSE_QUESTION_WINDOW.
		return new SmCloseQuestionWindow(0);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_CLOSE_QUESTION_WINDOW.writeImpl.
		buffer.WriteD(0);
		buffer.WriteD(_messageId);
		for (var index = 0; index < MaxParameterCount; index++)
			buffer.WriteS(index < _parameters.Count ? _parameters[index] : null);
	}
}
