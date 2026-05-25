using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmNearbyQuests : GameServerPacket
{
	public const int PacketOpCode = 127;
	private const int NotYetAvailableQuestFlag = 1 << 17;

	private readonly IReadOnlyList<NearbyQuestMarker> _nearbyQuests;

	public SmNearbyQuests(IReadOnlyList<NearbyQuestMarker> nearbyQuests)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_NEARBY_QUESTS(Map<Integer, Integer>).
		_nearbyQuests = nearbyQuests;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_NEARBY_QUESTS.writeImpl.
		buffer.WriteC(0);
		buffer.WriteH((-_nearbyQuests.Count) & 0xffff);
		foreach (var nearbyQuest in _nearbyQuests)
		{
			var questId = nearbyQuest.LevelRequirementDiff > 0
				? nearbyQuest.QuestId | NotYetAvailableQuestFlag
				: nearbyQuest.QuestId;
			buffer.WriteD(questId);
		}
	}
}

public readonly record struct NearbyQuestMarker(int QuestId, int LevelRequirementDiff);
