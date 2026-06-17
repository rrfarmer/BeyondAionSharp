using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class AbyssPointsService
{
	public static AbyssPointsAddPlan AddAp(
		Player? player,
		int amount,
		AbyssPointsAddOptions? options = null)
	{
		// Java parity: services/abyss/AbyssPointsService.addAp(Player, int).
		// CreateAddApPlan mutates the faithful in-memory AbyssRank in place (Java parity:
		// player.getAbyssRank().addAp(amount)); no separate assignment back to the player is needed.
		return CreateAddApPlan(player, amount, options);
	}

	public static AbyssPointsAddPlan CreateAddApPlan(
		Player? player,
		int amount,
		AbyssPointsAddOptions? options = null)
	{
		// Java parity: services/abyss/AbyssPointsService.addAp(Player, int), split for DB-transaction callers.
		if (player == null)
			return AbyssPointsAddPlan.NoPlayer();

		var rank = player.GetAbyssRank();
		var oldAp = rank.Ap;
		var oldRank = rank.Rank;
		// Java parity: services/abyss/AbyssPointsService.addAp -> player.getAbyssRank().addAp(amount).
		// The reworked AP-cap option is applied here before delegating to the faithful AddAp(int).
		var enableApCap = options?.EnableApCap ?? false;
		var apCapValue = options?.ApCapValue ?? 1_000_000;
		var appliedAmount = enableApCap && rank.Ap + amount > apCapValue
			? (int)(apCapValue - rank.Ap)
			: amount;
		rank.AddAp(appliedAmount);
		var updatedRank = PlayerAbyssRank.FromAbyssRank(rank);
		var added = updatedRank.Ap - oldAp;
		var rankChanged = oldRank != updatedRank.Rank;

		var packets = new List<AionServerPacket>
		{
			amount >= 0
				? SM_SYSTEM_MESSAGE.STR_MSG_COMBAT_MY_ABYSS_POINT_GAIN(added)
				: SM_SYSTEM_MESSAGE.STR_MSG_USE_ABYSSPOINT(-added),
		};
		if (added != 0 || rankChanged)
			packets.Add(new SmAbyssRank(updatedRank));

		var rankUpdate = rankChanged
			? new SM_ABYSS_RANK_UPDATE(0, player)
			: null;
		var legionContribution = CreateLegionContribution(player, added, options);
		return new AbyssPointsAddPlan(
			AbyssPointsAddStatus.Applied,
			OldAp: oldAp,
			Added: added,
			OldRank: oldRank,
			UpdatedRank: updatedRank,
			PlayerPackets: packets,
			RankUpdatePacket: rankUpdate,
			ShouldCheckRankLimitItems: rankChanged,
			ShouldUpdateAbyssSkills: rankChanged,
			LegionContribution: legionContribution,
			SiegeCallback: null);
	}

	public static AbyssPointsAddPlan AddApFromObject(
		Player? player,
		int sourceObjectId,
		bool sourceIsPlayer,
		bool sourceIsSiegeNpc,
		bool sourceSiegeNpcPeace,
		int amount,
		AbyssPointsAddOptions? options = null)
	{
		// Java parity: services/abyss/AbyssPointsService.addAp(Player, VisibleObject, int)
		// delegates to addAp(player, amount), then SiegeService.onAbyssPointsAdded.
		var plan = AddAp(player, amount, options);
		if (plan.Status != AbyssPointsAddStatus.Applied || player == null)
			return plan;

		var shouldNotifySiege = sourceIsPlayer || (sourceIsSiegeNpc && !sourceSiegeNpcPeace);
		return plan with
		{
			SiegeCallback = shouldNotifySiege
				? new AbyssPointsSiegeCallback(player.ObjectId, sourceObjectId, amount)
				: null,
		};
	}

	private static AbyssPointsLegionContribution? CreateLegionContribution(Player player, int added, AbyssPointsAddOptions? options)
	{
		if ((player.GetLegion()?.GetLegionId() ?? 0) == 0 || added <= 0)
			return null;

		var newContribution = (options?.CurrentLegionContributionPoints ?? 0) + added;
		return new AbyssPointsLegionContribution(
			(player.GetLegion()?.GetLegionId() ?? 0),
			added,
			newContribution,
			SmLegionEdit.Contribution(newContribution));
	}
}

public sealed record AbyssPointsAddOptions(
	long CurrentLegionContributionPoints = 0,
	bool EnableApCap = false,
	long ApCapValue = 1_000_000);

public sealed record AbyssPointsAddPlan(
	AbyssPointsAddStatus Status,
	int OldAp,
	int Added,
	int OldRank,
	PlayerAbyssRank? UpdatedRank,
	IReadOnlyList<AionServerPacket> PlayerPackets,
	SM_ABYSS_RANK_UPDATE? RankUpdatePacket,
	bool ShouldCheckRankLimitItems,
	bool ShouldUpdateAbyssSkills,
	AbyssPointsLegionContribution? LegionContribution,
	AbyssPointsSiegeCallback? SiegeCallback)
{
	public bool Applied => Status == AbyssPointsAddStatus.Applied;

	public static AbyssPointsAddPlan NoPlayer()
	{
		return new AbyssPointsAddPlan(
			AbyssPointsAddStatus.NoPlayer,
			OldAp: 0,
			Added: 0,
			OldRank: 0,
			UpdatedRank: null,
			PlayerPackets: Array.Empty<AionServerPacket>(),
			RankUpdatePacket: null,
			ShouldCheckRankLimitItems: false,
			ShouldUpdateAbyssSkills: false,
			LegionContribution: null,
			SiegeCallback: null);
	}
}

public enum AbyssPointsAddStatus
{
	Applied,
	NoPlayer,
}

public sealed record AbyssPointsLegionContribution(
	int LegionId,
	int AddedContributionPoints,
	long NewContributionPoints,
	SmLegionEdit Packet);

public sealed record AbyssPointsSiegeCallback(
	int PlayerObjectId,
	int SourceObjectId,
	int AbyssPoints);
