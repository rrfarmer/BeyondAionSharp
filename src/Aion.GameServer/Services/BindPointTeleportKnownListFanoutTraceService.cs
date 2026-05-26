using Aion.GameServer.Network.Aion;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKnownListFanoutTraceStatus
{
	Projected,
	NoPacket,
}

public enum BindPointTeleportKnownListFanoutRecipientKind
{
	SourceSelf,
	KnownListPlayer,
}

public enum BindPointTeleportKnownListFanoutKnownListOrdering
{
	ConcurrentHashMapUnspecified,
}

public sealed record BindPointTeleportKnownListFanoutRecipient(
	int PlayerObjectId,
	BindPointTeleportKnownListFanoutRecipientKind Kind);

public sealed record BindPointTeleportKnownListFanoutTrace(
	BindPointTeleportKnownListFanoutTraceStatus Status,
	BindPointTeleportFanoutPlan? FanoutPlan,
	IReadOnlyList<BindPointTeleportKnownListFanoutRecipient> Recipients,
	bool SendsSourceFirst,
	bool UsesKnownListTraversal,
	bool KnownListExcludesOwnerByNormalAddPath,
	bool DuplicateKnownObjectIdsCollapsed,
	BindPointTeleportKnownListFanoutKnownListOrdering KnownListOrdering,
	string JavaUtilityMethod,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKnownListFanoutTraceService
{
	public static BindPointTeleportKnownListFanoutTrace CreateTrace(
		BindPointTeleportFanoutPlan? fanoutPlan,
		IEnumerable<int>? knownListPlayerObjectIds)
	{
		// Java parity: PacketSendUtility.broadcastPacket(player, packet, true) sends the
		// source first, then iterates player.getKnownList().forEachPlayer(...). KnownList
		// excludes the owner and can include known-but-not-visible players.
		if (fanoutPlan == null || fanoutPlan.Packet == null)
		{
			return new BindPointTeleportKnownListFanoutTrace(
				BindPointTeleportKnownListFanoutTraceStatus.NoPacket,
				fanoutPlan,
				Array.Empty<BindPointTeleportKnownListFanoutRecipient>(),
				SendsSourceFirst: false,
				UsesKnownListTraversal: false,
				KnownListExcludesOwnerByNormalAddPath: true,
				DuplicateKnownObjectIdsCollapsed: true,
				BindPointTeleportKnownListFanoutKnownListOrdering.ConcurrentHashMapUnspecified,
				"PacketSendUtility.broadcastPacket(player, packet, true)",
				"Bind-point fanout trace skipped because no packet plan was supplied",
				IsLive: false);
		}

		var sourcePlayerObjectId = fanoutPlan.SourcePlayerObjectId;
		var knownRecipients = new List<BindPointTeleportKnownListFanoutRecipient>();
		var seen = new HashSet<int>();

		foreach (var knownPlayerObjectId in knownListPlayerObjectIds ?? Array.Empty<int>())
		{
			if (!seen.Add(knownPlayerObjectId))
				continue;

			knownRecipients.Add(new BindPointTeleportKnownListFanoutRecipient(
				knownPlayerObjectId,
				BindPointTeleportKnownListFanoutRecipientKind.KnownListPlayer));
		}

		return new BindPointTeleportKnownListFanoutTrace(
			BindPointTeleportKnownListFanoutTraceStatus.Projected,
			fanoutPlan,
			[
				new BindPointTeleportKnownListFanoutRecipient(
					sourcePlayerObjectId,
					BindPointTeleportKnownListFanoutRecipientKind.SourceSelf),
				.. knownRecipients,
			],
			SendsSourceFirst: true,
			UsesKnownListTraversal: true,
			KnownListExcludesOwnerByNormalAddPath: true,
			DuplicateKnownObjectIdsCollapsed: true,
			BindPointTeleportKnownListFanoutKnownListOrdering.ConcurrentHashMapUnspecified,
			fanoutPlan.JavaUtilityMethod,
			"PacketSendUtility.broadcastPacket(player, packet, true) -> source first, then KnownList.forEachPlayer",
			IsLive: false);
	}
}
