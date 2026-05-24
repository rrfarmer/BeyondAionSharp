using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmDuel : GameServerPacket
{
	public const int PacketOpCode = 185;

	private readonly byte _type;
	private readonly int _opponentObjectId;
	private readonly DuelResultKind _result;
	private readonly string _playerName;

	private SmDuel(byte type, int opponentObjectId = 0, DuelResultKind result = DuelResultKind.Win, string playerName = "")
		: base(PacketOpCode)
	{
		_type = type;
		_opponentObjectId = opponentObjectId;
		_result = result;
		_playerName = playerName;
	}

	public static SmDuel Started(int opponentObjectId)
	{
		// Java parity: SM_DUEL.SM_DUEL_STARTED(int).
		return new SmDuel(0, opponentObjectId);
	}

	public static SmDuel Result(DuelResultKind result, string playerName)
	{
		// Java parity: SM_DUEL.SM_DUEL_RESULT(DuelResult, String).
		return new SmDuel(1, result: result, playerName: playerName);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_DUEL.writeImpl.
		buffer.WriteC(_type);
		if (_type == 0)
		{
			buffer.WriteD(_opponentObjectId);
			return;
		}

		if (_type == 1)
		{
			buffer.WriteC((byte)_result);
			buffer.WriteD(GetResultMessageId(_result));
			buffer.WriteS(_playerName);
		}
	}

	private static int GetResultMessageId(DuelResultKind result)
	{
		return result switch
		{
			DuelResultKind.Win => 1300098,
			DuelResultKind.Lose => 1300099,
			DuelResultKind.Draw => 1300100,
			_ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported duel result."),
		};
	}
}

public enum DuelResultKind : byte
{
	// Java parity: model/DuelResult result ids consumed by SM_DUEL.
	Lose = 0,
	Draw = 1,
	Win = 2,
}
