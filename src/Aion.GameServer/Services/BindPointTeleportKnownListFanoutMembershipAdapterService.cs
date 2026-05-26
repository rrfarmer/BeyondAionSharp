namespace Aion.GameServer.Services;

public static class BindPointTeleportKnownListFanoutMembershipAdapterService
{
	public static BindPointTeleportKnownListFanoutTrace CreateTrace(
		BindPointTeleportFanoutPlan? fanoutPlan,
		PlayerKnownListMembershipSnapshot? membershipSnapshot)
	{
		// Java parity: PacketSendUtility.broadcastPacket(player, packet, true) traverses
		// KnownList.forEachPlayer membership, not KnownList.sees visibility state.
		var knownPlayerObjectIds = membershipSnapshot?.Entries.Select(entry => entry.KnownPlayerObjectId);
		return BindPointTeleportKnownListFanoutTraceService.CreateTrace(fanoutPlan, knownPlayerObjectIds);
	}
}
