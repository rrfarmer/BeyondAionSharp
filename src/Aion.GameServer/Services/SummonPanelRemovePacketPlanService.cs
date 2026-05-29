using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonPanelRemovePacketPlanStatus
{
	PacketCreated,
	BlockedNegativeSkillId,
}

public sealed record SummonPanelRemovePacketPlan(
	SummonPanelRemovePacketPlanStatus Status,
	int SkillId,
	SmSummonPanelRemove? Packet,
	bool ShouldSendToMaster,
	string JavaSource)
{
	public bool IsLive => false;
}

public static class SummonPanelRemovePacketPlanService
{
	public static SummonPanelRemovePacketPlan CreateSendToMasterPlan(int summonedBySkillId)
	{
		// Java parity breadcrumb: SummonsService.ReleaseSummonTask.run sends
		// new SM_SUMMON_PANEL_REMOVE(summon.getSummonedBySkillId()) to the master
		// for COMMAND, DISTANCE, and UNSPECIFIED unsummon types.
		if (summonedBySkillId < 0)
		{
			return new SummonPanelRemovePacketPlan(
				SummonPanelRemovePacketPlanStatus.BlockedNegativeSkillId,
				summonedBySkillId,
				Packet: null,
				ShouldSendToMaster: false,
				"SM_SUMMON_PANEL_REMOVE expects a non-negative summoned-by skill id");
		}

		return new SummonPanelRemovePacketPlan(
			SummonPanelRemovePacketPlanStatus.PacketCreated,
			summonedBySkillId,
			new SmSummonPanelRemove(summonedBySkillId),
			ShouldSendToMaster: true,
			"SummonsService.ReleaseSummonTask.run -> PacketSendUtility.sendPacket(master, new SM_SUMMON_PANEL_REMOVE(summon.getSummonedBySkillId()))");
	}
}
