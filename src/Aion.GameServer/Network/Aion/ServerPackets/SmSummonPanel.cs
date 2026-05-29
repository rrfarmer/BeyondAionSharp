using Aion.Commons.Network;

namespace Aion.GameServer.Network.Aion.ServerPackets;

public sealed record SummonPanelSnapshot(
	int ObjectId,
	int Level,
	int CurrentHp,
	int MaxHp,
	int MainHandPhysicalAttack,
	int PhysicalDefense,
	int MagicResist,
	int LiveTime);

public sealed class SmSummonPanel : GameServerPacket
{
	public const int PacketOpCode = 153;
	private readonly SummonPanelSnapshot _snapshot;

	public SmSummonPanel(SummonPanelSnapshot snapshot) : base(PacketOpCode)
	{
		_snapshot = snapshot;
	}

	protected override void WritePayload(PacketBuffer buffer, GameCrypt crypt)
	{
		// Java parity: network/aion/serverpackets/SM_SUMMON_PANEL.writeImpl.
		buffer.WriteD(_snapshot.ObjectId);
		buffer.WriteH(_snapshot.Level);
		buffer.WriteD(0);
		buffer.WriteD(0);
		buffer.WriteD(_snapshot.CurrentHp);
		buffer.WriteD(_snapshot.MaxHp);
		buffer.WriteD(_snapshot.MainHandPhysicalAttack);
		buffer.WriteH(_snapshot.PhysicalDefense);
		buffer.WriteH(0);
		buffer.WriteH(_snapshot.MagicResist);
		buffer.WriteH(0);
		buffer.WriteH(0);
		buffer.WriteD(_snapshot.LiveTime);
	}
}
