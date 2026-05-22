using Aion.Commons.Network;
using Aion.GameServer.Services;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmRiftAnnounce : GameServerPacket
{
	public const int PacketOpCode = 236;
	private readonly RiftAnnounceAction _action;
	private readonly IReadOnlyList<int> _announceCounts;
	private readonly int _gelkmaros;
	private readonly int _inggison;
	private readonly int _objectId;

	public SmRiftAnnounce(RiftAnnounceData announceData)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RIFT_ANNOUNCE(Map<Integer, Integer> rifts).
		_action = RiftAnnounceAction.Aggregate;
		_announceCounts = announceData.Counts;
	}

	public SmRiftAnnounce(bool gelkmaros, bool inggison)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RIFT_ANNOUNCE(boolean gelkmaros, boolean inggison).
		_action = RiftAnnounceAction.Silentera;
		_announceCounts = Array.Empty<int>();
		_gelkmaros = gelkmaros ? 1 : 0;
		_inggison = inggison ? 1 : 0;
	}

	public SmRiftAnnounce(int objectId)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RIFT_ANNOUNCE(int objectId) sends rift despawn info.
		_action = RiftAnnounceAction.Despawn;
		_announceCounts = Array.Empty<int>();
		_objectId = objectId;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_RIFT_ANNOUNCE.writeImpl actions 0, 1, and 4.
		switch (_action)
		{
			case RiftAnnounceAction.Aggregate:
				buffer.WriteH(1 + _announceCounts.Count * 4);
				buffer.WriteC((byte)_action);
				foreach (var value in _announceCounts)
					buffer.WriteD(value);
				break;
			case RiftAnnounceAction.Silentera:
				buffer.WriteH(9);
				buffer.WriteC((byte)_action);
				buffer.WriteD(_gelkmaros);
				buffer.WriteD(_inggison);
				break;
			case RiftAnnounceAction.Despawn:
				buffer.WriteH(5);
				buffer.WriteC((byte)_action);
				buffer.WriteD(_objectId);
				break;
		}
	}

	private enum RiftAnnounceAction : byte
	{
		Aggregate = 0,
		Silentera = 1,
		Despawn = 4,
	}
}
