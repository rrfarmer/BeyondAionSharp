using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonPanelPacketPlanStatus
{
	PacketCreated,
	BlockedInvalidSnapshot,
}

public sealed record SummonPanelPacketPlan(
	SummonPanelPacketPlanStatus Status,
	SummonPanelSnapshot Snapshot,
	SmSummonPanel? Packet,
	bool ShouldSendToMaster,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class SummonPanelPacketPlanService
{
	public static SummonPanelPacketPlan CreateSendToMasterPlan(SummonPanelSnapshot snapshot)
	{
		// Java parity breadcrumb: SummonsService.createSummon sends
		// new SM_SUMMON_PANEL(summon) to the master after master.setSummon(summon).
		if (!IsValid(snapshot))
		{
			return new SummonPanelPacketPlan(
				SummonPanelPacketPlanStatus.BlockedInvalidSnapshot,
				snapshot,
				Packet: null,
				ShouldSendToMaster: false,
				"SM_SUMMON_PANEL requires a resolved live Summon snapshot with non-negative primitive stat values");
		}

		return new SummonPanelPacketPlan(
			SummonPanelPacketPlanStatus.PacketCreated,
			snapshot,
			new SmSummonPanel(snapshot),
			ShouldSendToMaster: true,
			"SummonsService.createSummon -> PacketSendUtility.sendPacket(master, new SM_SUMMON_PANEL(summon))");
	}

	private static bool IsValid(SummonPanelSnapshot snapshot)
	{
		return snapshot.ObjectId > 0
			&& snapshot.Level >= 0
			&& snapshot.CurrentHp >= 0
			&& snapshot.MaxHp >= 0
			&& snapshot.MainHandPhysicalAttack >= 0
			&& snapshot.PhysicalDefense >= 0
			&& snapshot.MagicResist >= 0
			&& snapshot.LiveTime >= 0;
	}
}
