using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public enum SummonUpdateModeId
{
	Attack = 0,
	Guard = 1,
	Rest = 2,
	Release = 3,
	Unknown = 5,
}

public sealed record SummonUpdateStatSnapshot(int Current, int Base);

public sealed record SummonUpdateSnapshot(
	int Level,
	SummonUpdateModeId Mode,
	int CurrentHp,
	SummonUpdateStatSnapshot MaxHp,
	SummonUpdateStatSnapshot MainHandPhysicalAttack,
	SummonUpdateStatSnapshot PhysicalDefense,
	SummonUpdateStatSnapshot MagicResist,
	SummonUpdateStatSnapshot MagicDefense,
	SummonUpdateStatSnapshot MainHandPhysicalAccuracy,
	SummonUpdateStatSnapshot MainHandPhysicalCritical,
	SummonUpdateStatSnapshot MagicBoost,
	SummonUpdateStatSnapshot MagicBoostResist,
	SummonUpdateStatSnapshot MagicAccuracy,
	SummonUpdateStatSnapshot MagicCritical,
	SummonUpdateStatSnapshot Parry,
	SummonUpdateStatSnapshot Evasion);

public sealed class SmSummonUpdate : GameServerPacket
{
	public const int PacketOpCode = 155;
	private readonly SummonUpdateSnapshot _snapshot;

	public SmSummonUpdate(SummonUpdateSnapshot snapshot) : base(PacketOpCode)
	{
		_snapshot = snapshot;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SUMMON_UPDATE.writeImpl.
		buffer.WriteC(_snapshot.Level);
		buffer.WriteH((int)_snapshot.Mode);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(_snapshot.CurrentHp);
		buffer.WriteD(_snapshot.MaxHp.Current);
		buffer.WriteD(_snapshot.MainHandPhysicalAttack.Current);
		buffer.WriteD(_snapshot.PhysicalDefense.Current);
		buffer.WriteH(_snapshot.MagicResist.Current);
		buffer.WriteD(_snapshot.MagicDefense.Current);
		buffer.WriteH(_snapshot.MainHandPhysicalAccuracy.Current);
		buffer.WriteH(_snapshot.MainHandPhysicalCritical.Current);
		buffer.WriteH(_snapshot.MagicBoost.Current);
		buffer.WriteH(_snapshot.MagicBoostResist.Current);
		buffer.WriteH(_snapshot.MagicAccuracy.Current);
		buffer.WriteH(_snapshot.MagicCritical.Current);
		buffer.WriteH(_snapshot.Parry.Current);
		buffer.WriteH(_snapshot.Evasion.Current);
		buffer.WriteD(_snapshot.MaxHp.Base);
		buffer.WriteD(_snapshot.MainHandPhysicalAttack.Base);
		buffer.WriteD(_snapshot.PhysicalDefense.Base);
		buffer.WriteH(_snapshot.MagicResist.Base);
		buffer.WriteD(_snapshot.MagicDefense.Base);
		buffer.WriteH(_snapshot.MainHandPhysicalAccuracy.Base);
		buffer.WriteH(_snapshot.MainHandPhysicalCritical.Base);
		buffer.WriteH(_snapshot.MagicBoost.Base);
		buffer.WriteH(_snapshot.MagicBoostResist.Base);
		buffer.WriteH(_snapshot.MagicAccuracy.Base);
		buffer.WriteH(_snapshot.MagicCritical.Base);
		buffer.WriteH(_snapshot.Parry.Base);
		buffer.WriteH(_snapshot.Evasion.Base);
	}
}
