using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonUpdatePacketPlanStatus
{
	PacketCreated,
	BlockedInvalidSnapshot,
}

public sealed record SummonUpdatePacketPlan(
	SummonUpdatePacketPlanStatus Status,
	SummonUpdateSnapshot Snapshot,
	SmSummonUpdate? Packet,
	bool ShouldSendToMaster,
	bool ShouldBroadcastFromSummon,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class SummonUpdatePacketPlanService
{
	public static SummonUpdatePacketPlan CreateSendToMasterPlan(SummonUpdateSnapshot snapshot, string javaSource)
	{
		return CreatePlan(snapshot, shouldSendToMaster: true, shouldBroadcastFromSummon: false, javaSource);
	}

	public static SummonUpdatePacketPlan CreateBroadcastFromSummonPlan(SummonUpdateSnapshot snapshot)
	{
		return CreatePlan(
			snapshot,
			shouldSendToMaster: false,
			shouldBroadcastFromSummon: true,
			"SummonsService.createSummon -> PacketSendUtility.broadcastPacket(summon, new SM_SUMMON_UPDATE(summon))");
	}

	private static SummonUpdatePacketPlan CreatePlan(
		SummonUpdateSnapshot snapshot,
		bool shouldSendToMaster,
		bool shouldBroadcastFromSummon,
		string javaSource)
	{
		// Java parity boundary: Java receives a live Summon and Stat2 instances.
		// This planner only records the packet intent once those values are snapshotted.
		if (!IsValid(snapshot))
		{
			return new SummonUpdatePacketPlan(
				SummonUpdatePacketPlanStatus.BlockedInvalidSnapshot,
				snapshot,
				Packet: null,
				ShouldSendToMaster: false,
				ShouldBroadcastFromSummon: false,
				"SM_SUMMON_UPDATE requires a resolved live Summon snapshot with non-negative primitive stat values and a Java SummonMode id");
		}

		return new SummonUpdatePacketPlan(
			SummonUpdatePacketPlanStatus.PacketCreated,
			snapshot,
			new SmSummonUpdate(snapshot),
			shouldSendToMaster,
			shouldBroadcastFromSummon,
			javaSource);
	}

	private static bool IsValid(SummonUpdateSnapshot snapshot)
	{
		return Enum.IsDefined(snapshot.Mode)
			&& snapshot.Level >= 0
			&& snapshot.CurrentHp >= 0
			&& IsNonNegative(snapshot.MaxHp)
			&& IsNonNegative(snapshot.MainHandPhysicalAttack)
			&& IsNonNegative(snapshot.PhysicalDefense)
			&& IsNonNegative(snapshot.MagicResist)
			&& IsNonNegative(snapshot.MagicDefense)
			&& IsNonNegative(snapshot.MainHandPhysicalAccuracy)
			&& IsNonNegative(snapshot.MainHandPhysicalCritical)
			&& IsNonNegative(snapshot.MagicBoost)
			&& IsNonNegative(snapshot.MagicBoostResist)
			&& IsNonNegative(snapshot.MagicAccuracy)
			&& IsNonNegative(snapshot.MagicCritical)
			&& IsNonNegative(snapshot.Parry)
			&& IsNonNegative(snapshot.Evasion);
	}

	private static bool IsNonNegative(SummonUpdateStatSnapshot stat)
	{
		return stat.Current >= 0 && stat.Base >= 0;
	}
}
