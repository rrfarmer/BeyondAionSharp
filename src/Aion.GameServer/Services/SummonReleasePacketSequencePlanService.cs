using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum SummonReleaseUnsummonType
{
	Logout,
	Distance,
	Command,
	Unspecified,
}

public enum SummonReleasePacketSequencePlanStatus
{
	SequenceCreated,
	SkippedLogout,
	BlockedNegativeSkillId,
	BlockedInvalidSummonObjectId,
}

public sealed record SummonReleasePacketSequencePlan(
	SummonReleasePacketSequencePlanStatus Status,
	SummonReleaseUnsummonType UnsummonType,
	int SummonedBySkillId,
	int SummonObjectId,
	SummonPanelRemovePacketPlan? PanelRemovePlan,
	SummonOwnerRemovePacketPlan? OwnerRemovePlan,
	IReadOnlyList<GameServerPacket> PacketsInOrder,
	bool SendsPanelRemoveFirst,
	bool SendsOwnerRemoveSecond,
	bool ShouldSendToMaster,
	string JavaSource
)
{
	public bool IsLive => false;
}

public static class SummonReleasePacketSequencePlanService
{
	public static SummonReleasePacketSequencePlan CreatePlan(SummonReleaseUnsummonType unsummonType, int summonedBySkillId, int summonObjectId)
	{
		// Java parity: SummonsService.ReleaseSummonTask.run sends the release system message first, then
		// SM_SUMMON_PANEL_REMOVE, then SM_SUMMON_OWNER_REMOVE for COMMAND, DISTANCE, and UNSPECIFIED.
		// LOGOUT skips this master packet pair. This planner models only the two-packet release sequence.
		if (unsummonType == SummonReleaseUnsummonType.Logout)
		{
			return new SummonReleasePacketSequencePlan(
				SummonReleasePacketSequencePlanStatus.SkippedLogout,
				unsummonType,
				summonedBySkillId,
				summonObjectId,
				PanelRemovePlan: null,
				OwnerRemovePlan: null,
				PacketsInOrder: [],
				SendsPanelRemoveFirst: false,
				SendsOwnerRemoveSecond: false,
				ShouldSendToMaster: false,
				"SummonsService.ReleaseSummonTask.run -> LOGOUT branch skips SM_SUMMON_PANEL_REMOVE and SM_SUMMON_OWNER_REMOVE"
			);
		}

		var panelRemovePlan = SummonPanelRemovePacketPlanService.CreateSendToMasterPlan(summonedBySkillId);
		if (panelRemovePlan.Status != SummonPanelRemovePacketPlanStatus.PacketCreated || panelRemovePlan.Packet == null)
		{
			return new SummonReleasePacketSequencePlan(
				SummonReleasePacketSequencePlanStatus.BlockedNegativeSkillId,
				unsummonType,
				summonedBySkillId,
				summonObjectId,
				panelRemovePlan,
				OwnerRemovePlan: null,
				PacketsInOrder: [],
				SendsPanelRemoveFirst: false,
				SendsOwnerRemoveSecond: false,
				ShouldSendToMaster: false,
				panelRemovePlan.JavaSource
			);
		}

		var ownerRemovePlan = SummonOwnerRemovePacketPlanService.CreateSendToMasterPlan(summonObjectId);
		if (ownerRemovePlan.Status != SummonOwnerRemovePacketPlanStatus.PacketCreated || ownerRemovePlan.Packet == null)
		{
			return new SummonReleasePacketSequencePlan(
				SummonReleasePacketSequencePlanStatus.BlockedInvalidSummonObjectId,
				unsummonType,
				summonedBySkillId,
				summonObjectId,
				panelRemovePlan,
				ownerRemovePlan,
				PacketsInOrder: [],
				SendsPanelRemoveFirst: false,
				SendsOwnerRemoveSecond: false,
				ShouldSendToMaster: false,
				ownerRemovePlan.JavaSource
			);
		}

		return new SummonReleasePacketSequencePlan(
			SummonReleasePacketSequencePlanStatus.SequenceCreated,
			unsummonType,
			summonedBySkillId,
			summonObjectId,
			panelRemovePlan,
			ownerRemovePlan,
			[panelRemovePlan.Packet, ownerRemovePlan.Packet],
			SendsPanelRemoveFirst: true,
			SendsOwnerRemoveSecond: true,
			ShouldSendToMaster: true,
			"SummonsService.ReleaseSummonTask.run -> SM_SYSTEM_MESSAGE -> SM_SUMMON_PANEL_REMOVE -> SM_SUMMON_OWNER_REMOVE"
		);
	}
}
