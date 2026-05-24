using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmItemUsageAnimation : GameServerPacket
{
	public const int PacketOpCode = 183;

	private readonly int _playerObjectId;
	private readonly int _targetObjectId;
	private readonly int _itemObjectId;
	private readonly int _itemId;
	private readonly int _time;
	private readonly int _end;
	private readonly int _unknown;
	private readonly int _unknown1;
	private readonly int _unknown2;
	private readonly int _unknown3;

	public SmItemUsageAnimation(int playerObjectId, int itemObjectId, int itemId)
		: this(playerObjectId, itemObjectId, itemId, 0, 1, 1)
	{
		// Java parity: SM_ITEM_USAGE_ANIMATION(int, int, int) defaults target=self, time=0, end=1, unk2=1, unk3=1.
	}

	public SmItemUsageAnimation(int playerObjectId, int itemObjectId, int itemId, int time, int end)
		: this(playerObjectId, itemObjectId, itemId, time, end, 0)
	{
		// Java parity: SM_ITEM_USAGE_ANIMATION(int, int, int, int, int) leaves unk3 at its Java field default 0.
	}

	public SmItemUsageAnimation(int playerObjectId, int itemObjectId, int itemId, int time, int end, int unknown)
		: this(playerObjectId, playerObjectId, itemObjectId, itemId, time, end, 0, 0, 1, unknown)
	{
		// Java parity: network/aion/serverpackets/SM_ITEM_USAGE_ANIMATION(int, int, int, int, int, int).
	}

	public SmItemUsageAnimation(
		int playerObjectId,
		int targetObjectId,
		int itemObjectId,
		int itemId,
		int time,
		int end,
		int unknown)
		: this(playerObjectId, targetObjectId, itemObjectId, itemId, time, end, 0, 0, 1, unknown)
	{
		// Java parity: SM_ITEM_USAGE_ANIMATION(int, int, int, int, int, int, int) maps the final argument to unk3.
	}

	public SmItemUsageAnimation(
		int playerObjectId,
		int targetObjectId,
		int itemObjectId,
		int itemId,
		int time,
		int end,
		int unknown,
		int unknown1,
		int unknown2,
		int unknown3)
		: base(PacketOpCode)
	{
		// Java parity: SM_ITEM_USAGE_ANIMATION.writeImpl payload shape.
		_playerObjectId = playerObjectId;
		_targetObjectId = targetObjectId;
		_itemObjectId = itemObjectId;
		_itemId = itemId;
		_time = time;
		_end = end;
		_unknown = unknown;
		_unknown1 = unknown1;
		_unknown2 = unknown2;
		_unknown3 = unknown3;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_playerObjectId);
		buffer.WriteD(_targetObjectId);
		buffer.WriteD(_itemObjectId);
		buffer.WriteD(_itemId);
		buffer.WriteD(_time);
		buffer.WriteC(_end);
		buffer.WriteC(_unknown);
		buffer.WriteC(_unknown1);
		buffer.WriteC(_unknown2);
		buffer.WriteD(_unknown3);
	}
}
