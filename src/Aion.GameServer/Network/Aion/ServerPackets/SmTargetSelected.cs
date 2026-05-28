using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed class SmTargetSelected : GameServerPacket
{
	public const int PacketOpCode = 41;

	private readonly TargetSelectedSnapshot _target;

	public SmTargetSelected(TargetSelectedSnapshot? target)
		: base(PacketOpCode)
	{
		// Java parity: network/aion/serverpackets/SM_TARGET_SELECTED(VisibleObject).
		// Null target keeps all Java primitive fields at their default zero values.
		_target = target ?? TargetSelectedSnapshot.Empty;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: SM_TARGET_SELECTED.writeImpl writes target id, level, max/current HP,
		// then max/current MP. Non-creature targets carry only the object id.
		buffer.WriteD(_target.TargetObjectId);
		buffer.WriteH(_target.Level);
		buffer.WriteD(_target.MaxHp);
		buffer.WriteD(_target.CurrentHp);
		buffer.WriteD(_target.MaxMp);
		buffer.WriteD(_target.CurrentMp);
	}
}

public sealed record TargetSelectedSnapshot(
	int TargetObjectId,
	int Level,
	int MaxHp,
	int CurrentHp,
	int MaxMp,
	int CurrentMp)
{
	public static TargetSelectedSnapshot Empty { get; } = new(0, 0, 0, 0, 0, 0);

	public static TargetSelectedSnapshot VisibleObject(int targetObjectId)
	{
		return new TargetSelectedSnapshot(targetObjectId, 0, 0, 0, 0, 0);
	}
}
