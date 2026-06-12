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
		var plan = CreateAddApPlan(player, amount, options);
		if (player != null && plan.UpdatedRank != null)
			player.AbyssRank = plan.UpdatedRank;
		return plan;
	}

	public static AbyssPointsAddPlan CreateAddApPlan(
		Player? player,
		int amount,
		AbyssPointsAddOptions? options = null)
	{
		// Java parity: services/abyss/AbyssPointsService.addAp(Player, int), split for DB-transaction callers.
		if (player == null)
			return AbyssPointsAddPlan.NoPlayer();

		var oldAp = player.AbyssRank.Ap;
		var oldRank = player.AbyssRank.Rank;
		var updatedRank = player.AbyssRank.AddAp(
			amount,
			options?.EnableApCap ?? false,
			options?.ApCapValue ?? 1_000_000);
		var added = updatedRank.Ap - oldAp;
		var rankChanged = oldRank != updatedRank.Rank;

		var packets = new List<AionServerPacket>
		{
			amount >= 0
				? SmSystemMessage.CombatMyAbyssPointGain(added)
				: SmSystemMessage.UseAbyssPoint(-added),
		};
		if (added != 0 || rankChanged)
			packets.Add(new SmAbyssRank(updatedRank));

		var rankUpdate = rankChanged
			? SmAbyssRankUpdate.RankChange(new Player { ObjectId = player.ObjectId, AbyssRank = updatedRank })
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
	SmAbyssRankUpdate? RankUpdatePacket,
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
