using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonOwnerRemovePacketPlanStatus
{
	PacketCreated,
	BlockedInvalidSummonObjectId,
}

public sealed record SummonOwnerRemovePacketPlan(
	SummonOwnerRemovePacketPlanStatus Status,
	int SummonObjectId,
	SmSummonOwnerRemove? Packet,
	bool ShouldSendToMaster,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class SummonOwnerRemovePacketPlanService
{
	public static SummonOwnerRemovePacketPlan CreateSendToMasterPlan(int summonObjectId)
	{
		// Java parity breadcrumb: SummonsService.ReleaseSummonTask.run sends
		// new SM_SUMMON_OWNER_REMOVE(summon.getObjectId()) to the master
		// after SM_SUMMON_PANEL_REMOVE for COMMAND, DISTANCE, and UNSPECIFIED.
		if (summonObjectId <= 0)
		{
			return new SummonOwnerRemovePacketPlan(
				SummonOwnerRemovePacketPlanStatus.BlockedInvalidSummonObjectId,
				summonObjectId,
				Packet: null,
				ShouldSendToMaster: false,
				"SM_SUMMON_OWNER_REMOVE requires a resolved positive summon object id");
		}

		return new SummonOwnerRemovePacketPlan(
			SummonOwnerRemovePacketPlanStatus.PacketCreated,
			summonObjectId,
			new SmSummonOwnerRemove(summonObjectId),
			ShouldSendToMaster: true,
			"SummonsService.ReleaseSummonTask.run -> PacketSendUtility.sendPacket(master, new SM_SUMMON_OWNER_REMOVE(summon.getObjectId()))");
	}
}
