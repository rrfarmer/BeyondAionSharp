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
	private readonly RiftPortalState? _portal;
	private readonly Func<DateTimeOffset>? _clock;

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

	public SmRiftAnnounce(RiftPortalState portal, bool isMaster, Func<DateTimeOffset>? clock = null)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_RIFT_ANNOUNCE(RVController rift, boolean isMaster).
		_action = isMaster ? RiftAnnounceAction.PortalDetail : RiftAnnounceAction.PortalEntryUpdate;
		_announceCounts = Array.Empty<int>();
		_portal = portal;
		_clock = clock ?? (() => DateTimeOffset.UtcNow);
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_RIFT_ANNOUNCE.writeImpl actions 0 through 4.
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
			case RiftAnnounceAction.PortalDetail:
			{
				var portal = GetPortal();
				var owner = portal.MasterNpc;
				var position = owner.Position;
				buffer.WriteH(35);
				buffer.WriteC((byte)_action);
				buffer.WriteD(owner.ObjectId);
				buffer.WriteD(portal.MaxEntries);
				buffer.WriteD(portal.GetRemainTime(GetNow()));
				buffer.WriteD(portal.MinLevel);
				buffer.WriteD(portal.MaxLevel);
				buffer.WriteF(position.X);
				buffer.WriteF(position.Y);
				buffer.WriteF(position.Z);
				buffer.WriteC(GetRiftType(portal));
				buffer.WriteC(1);
				break;
			}
			case RiftAnnounceAction.PortalEntryUpdate:
			{
				var portal = GetPortal();
				buffer.WriteH(15);
				buffer.WriteC((byte)_action);
				buffer.WriteD(portal.MasterNpc.ObjectId);
				buffer.WriteD(portal.UsedEntries);
				buffer.WriteD(portal.GetRemainTime(GetNow()));
				buffer.WriteC(GetRiftType(portal));
				buffer.WriteC(0);
				break;
			}
			case RiftAnnounceAction.Despawn:
				buffer.WriteH(5);
				buffer.WriteC((byte)_action);
				buffer.WriteD(_objectId);
				break;
		}
	}

	private RiftPortalState GetPortal()
	{
		return _portal ?? throw new InvalidOperationException("Rift portal packet requires portal state.");
	}

	private DateTimeOffset GetNow()
	{
		return (_clock ?? (() => DateTimeOffset.UtcNow))();
	}

	private static byte GetRiftType(RiftPortalState portal)
	{
		// Java parity: SM_RIFT_ANNOUNCE.writeRiftType.
		if (portal.IsVortex)
			return 1;
		if (portal.IsVolatile)
			return 4;
		if (portal.IsInvasion)
			return 5;
		return 0;
	}

	private enum RiftAnnounceAction : byte
	{
		Aggregate = 0,
		Silentera = 1,
		PortalDetail = 2,
		PortalEntryUpdate = 3,
		Despawn = 4,
	}
}
