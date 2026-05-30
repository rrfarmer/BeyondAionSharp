namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskSightedRecipientTraceStatus
{
	Projected,
	NoBroadcast,
}

public enum PlayerProtectionActiveTaskSightedRecipientKind
{
	SourceSelf,
	KnownListSightedPlayer,
}

public enum PlayerProtectionActiveTaskKnownListOrdering
{
	ConcurrentHashMapUnspecified,
}

public sealed record PlayerProtectionActiveTaskRecipientVisibilityFact(
	int RecipientPlayerObjectId,
	bool RecipientSeesSource,
	string JavaSource = "com.aionemu.gameserver.world.knownlist.KnownList.sees"
);

public sealed record PlayerProtectionActiveTaskSightedRecipient(
	int PlayerObjectId,
	PlayerProtectionActiveTaskSightedRecipientKind Kind,
	bool RecipientSeesSource
);

public sealed record PlayerProtectionActiveTaskSightedRecipientTrace(
	PlayerProtectionActiveTaskSightedRecipientTraceStatus Status,
	PlayerProtectionActiveTaskFanoutPlan FanoutPlan,
	IReadOnlyList<PlayerProtectionActiveTaskSightedRecipient> Recipients,
	bool SendsSourceFirst,
	bool UsesSourceKnownListTraversal,
	bool UsesRecipientKnownListSeesFilter,
	bool KnownListExcludesOwnerByNormalAddPath,
	bool DuplicateKnownObjectIdsCollapsed,
	PlayerProtectionActiveTaskKnownListOrdering KnownListOrdering,
	string JavaUtilityMethod,
	string JavaSource,
	bool IsLive
);

public static class PlayerProtectionActiveTaskSightedRecipientTraceService
{
	public static PlayerProtectionActiveTaskSightedRecipientTrace CreateTrace(
		PlayerProtectionActiveTaskFanoutPlan fanoutPlan,
		PlayerKnownListMembershipSnapshot? sourceKnownListSnapshot,
		IEnumerable<PlayerProtectionActiveTaskRecipientVisibilityFact>? recipientVisibilityFacts
	)
	{
		// Java parity: PacketSendUtility.broadcastToSightedPlayers iterates the source known list and keeps
		// only recipients whose own known list sees the source. This trace reconstructs that staged recipient
		// set from membership and visibility facts.
		if (!fanoutPlan.ShouldBroadcast)
		{
			return new PlayerProtectionActiveTaskSightedRecipientTrace(
				PlayerProtectionActiveTaskSightedRecipientTraceStatus.NoBroadcast,
				fanoutPlan,
				Array.Empty<PlayerProtectionActiveTaskSightedRecipient>(),
				SendsSourceFirst: false,
				UsesSourceKnownListTraversal: false,
				UsesRecipientKnownListSeesFilter: false,
				KnownListExcludesOwnerByNormalAddPath: true,
				DuplicateKnownObjectIdsCollapsed: true,
				PlayerProtectionActiveTaskKnownListOrdering.ConcurrentHashMapUnspecified,
				"PacketSendUtility.broadcastToSightedPlayers(player, packet, true)",
				"Protection fanout trace skipped because Java branch does not call PacketSendUtility.broadcastToSightedPlayers",
				IsLive: false
			);
		}

		var visibilityByRecipient = (recipientVisibilityFacts ?? Array.Empty<PlayerProtectionActiveTaskRecipientVisibilityFact>())
			.GroupBy(fact => fact.RecipientPlayerObjectId)
			.ToDictionary(group => group.Key, group => group.Last().RecipientSeesSource);
		var seenKnownPlayers = new HashSet<int>();
		var knownRecipients = new List<PlayerProtectionActiveTaskSightedRecipient>();

		foreach (var entry in sourceKnownListSnapshot?.Entries ?? Array.Empty<PlayerKnownListMembershipEntry>())
		{
			if (!seenKnownPlayers.Add(entry.KnownPlayerObjectId))
				continue;
			if (!visibilityByRecipient.TryGetValue(entry.KnownPlayerObjectId, out var recipientSeesSource) || !recipientSeesSource)
				continue;

			knownRecipients.Add(
				new PlayerProtectionActiveTaskSightedRecipient(
					entry.KnownPlayerObjectId,
					PlayerProtectionActiveTaskSightedRecipientKind.KnownListSightedPlayer,
					RecipientSeesSource: true
				)
			);
		}

		return new PlayerProtectionActiveTaskSightedRecipientTrace(
			PlayerProtectionActiveTaskSightedRecipientTraceStatus.Projected,
			fanoutPlan,
			[
				new PlayerProtectionActiveTaskSightedRecipient(
					fanoutPlan.PlayerObjectId,
					PlayerProtectionActiveTaskSightedRecipientKind.SourceSelf,
					RecipientSeesSource: true
				),
				.. knownRecipients,
			],
			SendsSourceFirst: true,
			UsesSourceKnownListTraversal: true,
			UsesRecipientKnownListSeesFilter: true,
			KnownListExcludesOwnerByNormalAddPath: true,
			DuplicateKnownObjectIdsCollapsed: true,
			PlayerProtectionActiveTaskKnownListOrdering.ConcurrentHashMapUnspecified,
			"PacketSendUtility.broadcastToSightedPlayers(player, packet, true)",
			"broadcastToSightedPlayers -> sendPacket(source) -> source.getKnownList().forEachPlayer(other -> other.getKnownList().sees(source))",
			IsLive: false
		);
	}
}
