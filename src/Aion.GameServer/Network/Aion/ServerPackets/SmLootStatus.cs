using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmLootStatus : GameServerPacket
{
	public const int PacketOpCode = 205;

	private readonly int _targetObjectId;
	private readonly SmLootStatusType _status;
	private readonly int _lootEffectId;

	public SmLootStatus(int targetObjectId, SmLootStatusType status, int lootEffectId = 0)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_LOOT_STATUS.
		_targetObjectId = targetObjectId;
		_status = status;
		_lootEffectId = status == SmLootStatusType.LootEnable ? lootEffectId : 0;
	}

	public int TargetObjectId => _targetObjectId;

	public SmLootStatusType Status => _status;

	public int LootEffectId => _lootEffectId;

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		buffer.WriteD(_targetObjectId);
		buffer.WriteC((int)_status);
		buffer.WriteD(_lootEffectId);
	}
}

public enum SmLootStatusType
{
	LootEnable = 0,
	LootDisable = 1,
	OpenDropList = 2,
	CloseDropList = 3,
}
