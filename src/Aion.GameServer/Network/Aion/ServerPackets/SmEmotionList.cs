using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmEmotionList : GameServerPacket
{
	public const int PacketOpCode = 79;

	private readonly byte _action;
	private readonly IReadOnlyList<PlayerEmotion> _emotions;
	private readonly Func<DateTimeOffset> _clock;

	public SmEmotionList(byte action, IReadOnlyList<PlayerEmotion> emotions, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION_LIST(byte action, Collection<Emotion>).
		_action = action;
		_emotions = emotions;
		_clock = clock ?? (() => DateTimeOffset.Now);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_EMOTION_LIST.writeImpl.
		buffer.WriteC(_action);
		buffer.WriteH(_emotions.Count);
		var now = _clock();
		foreach (var emotion in _emotions)
		{
			buffer.WriteD(emotion.Id);
			buffer.WriteH(emotion.SecondsUntilExpiration(now));
		}
	}
}
