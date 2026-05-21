using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmQuestionWindow : GameServerPacket
{
	public const int PacketOpCode = 52;
	public const int BuddyListAddBuddyRequest = 1401498;
	public const int SoulBoundItemConfirm = 95006;
	public const int ItemChargeAllConfirm = 903026;
	public const int ItemCharge2AllConfirm = 904039;
	private const int MaxParameterCount = 3;

	private readonly int _code;
	private readonly int _senderObjectId;
	private readonly int _rangeOrCooldownSeconds;
	private readonly IReadOnlyList<string> _parameters;

	public SmQuestionWindow(int code, int senderObjectId, int rangeOrCooldownSeconds, params string[] parameters)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_QUESTION_WINDOW(int code, int senderId, int rangeOrCooldownSeconds, Object... params).
		if (parameters.Length > MaxParameterCount)
			throw new ArgumentException("More than three question-window parameters are not supported.", nameof(parameters));
		_code = code;
		_senderObjectId = senderObjectId;
		_rangeOrCooldownSeconds = rangeOrCooldownSeconds;
		_parameters = parameters;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_QUESTION_WINDOW.writeImpl.
		buffer.WriteD(_code);
		for (var index = 0; index < MaxParameterCount; index++)
			buffer.WriteS(index < _parameters.Count ? _parameters[index] : null);
		buffer.WriteD(0);
		buffer.WriteC(_rangeOrCooldownSeconds > 0 ? 1 : 0);
		buffer.WriteD(_senderObjectId);
		buffer.WriteD(_rangeOrCooldownSeconds);
	}
}
