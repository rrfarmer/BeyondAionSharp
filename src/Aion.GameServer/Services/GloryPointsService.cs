using Aion.GameServer.Data;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class GloryPointsService
{
	public static GloryPointsAddPlan AddGp(Player? player, int playerObjectId, int amount)
	{
		// Java parity: services/abyss/GloryPointsService.addGp(int, int).
		// CreateAddGpPlan mutates the faithful in-memory AbyssRank in place (Java parity:
		// player.getAbyssRank().addGp(amount, addToStats)); no assignment back to the player is needed.
		return CreateAddGpPlan(player, playerObjectId, amount);
	}

	public static GloryPointsAddPlan CreateAddGpPlan(Player? player, int playerObjectId, int amount)
	{
		// Java parity: services/abyss/GloryPointsService.addGp returns immediately for zero GP.
		if (amount == 0)
			return GloryPointsAddPlan.NoReward(playerObjectId);

		var addToStats = amount > 0;
		if (player == null)
		{
			return GloryPointsAddPlan.OfflineDaoUpdateRequired(
				playerObjectId,
				amount,
				addToStats);
		}

		var rank = player.GetAbyssRank();
		var oldGp = rank.Gp;
		// Java parity: services/abyss/GloryPointsService.addGp -> player.getAbyssRank().addGp(amount, addToStats).
		rank.AddGp(amount, addToStats);
		var updatedRank = PlayerAbyssRank.FromAbyssRank(rank);
		var added = updatedRank.Gp - oldGp;
		var packets = new List<AionServerPacket>
		{
			amount >= 0
				? SmSystemMessage.GloryPointGain(added)
				: SmSystemMessage.GloryPointLose(-added),
		};
		if (added != 0)
			packets.Add(new SmAbyssRank(updatedRank));

		return GloryPointsAddPlan.CreateApplied(
			player.ObjectId,
			amount,
			added,
			oldGp,
			updatedRank,
			addToStats,
			packets);
	}

	public static async ValueTask<GloryPointsOfflineExecutionResult> ExecuteOfflineDaoUpdateAsync(
		GloryPointsAddPlan plan,
		IAbyssRankRepository repository,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(plan);
		ArgumentNullException.ThrowIfNull(repository);

		// Java parity: services/abyss/GloryPointsService.addGp offline player branch delegates
		// to dao/AbyssRankDAO.addGp(playerObjId, amount, addToStats). This explicit method
		// keeps live callers gated until offline reward execution is intentionally wired.
		if (!plan.RequiresOfflineDaoUpdate)
			return GloryPointsOfflineExecutionResult.NotOfflineDaoUpdate(plan);

		var executed = await repository.AddGpAsync(
			plan.ObjectId,
			plan.Amount,
			plan.AddsDailyWeeklyStats,
			cancellationToken);
		return GloryPointsOfflineExecutionResult.FromRepositoryResult(plan, executed);
	}
}

public sealed record GloryPointsAddPlan(
	GloryPointsAddStatus Status,
	int ObjectId,
	int Amount,
	int Added,
	int PreviousGp,
	PlayerAbyssRank? UpdatedRank,
	bool AddsDailyWeeklyStats,
	bool RequiresOfflineDaoUpdate,
	IReadOnlyList<AionServerPacket> PlayerPackets,
	string JavaSource)
{
	public bool Applied => Status == GloryPointsAddStatus.Applied;

	public static GloryPointsAddPlan CreateApplied(
		int objectId,
		int amount,
		int added,
		int previousGp,
		PlayerAbyssRank updatedRank,
		bool addsDailyWeeklyStats,
		IReadOnlyList<AionServerPacket> playerPackets)
	{
		return new GloryPointsAddPlan(
			GloryPointsAddStatus.Applied,
			objectId,
			amount,
			added,
			previousGp,
			updatedRank,
			addsDailyWeeklyStats,
			RequiresOfflineDaoUpdate: false,
			playerPackets,
			"GloryPointsService.addGp online player branch");
	}

	public static GloryPointsAddPlan OfflineDaoUpdateRequired(int objectId, int amount, bool addsDailyWeeklyStats)
	{
		return new GloryPointsAddPlan(
			GloryPointsAddStatus.OfflineDaoUpdateRequired,
			objectId,
			amount,
			0,
			0,
			null,
			addsDailyWeeklyStats,
			RequiresOfflineDaoUpdate: true,
			Array.Empty<AionServerPacket>(),
			"AbyssRankDAO.addGp offline player branch");
	}

	public static GloryPointsAddPlan NoReward(int objectId)
	{
		return new GloryPointsAddPlan(
			GloryPointsAddStatus.NoReward,
			objectId,
			0,
			0,
			0,
			null,
			AddsDailyWeeklyStats: false,
			RequiresOfflineDaoUpdate: false,
			Array.Empty<AionServerPacket>(),
			"GloryPointsService.addGp zero GP guard");
	}
}

public enum GloryPointsAddStatus
{
	Applied,
	OfflineDaoUpdateRequired,
	NoReward,
}

public sealed record GloryPointsOfflineExecutionResult(
	GloryPointsOfflineExecutionStatus Status,
	int ObjectId,
	int Amount,
	bool AddsDailyWeeklyStats,
	bool RepositorySucceeded,
	string JavaSource)
{
	public bool Executed => Status == GloryPointsOfflineExecutionStatus.Executed;

	public static GloryPointsOfflineExecutionResult FromRepositoryResult(
		GloryPointsAddPlan plan,
		bool repositorySucceeded)
	{
		return new GloryPointsOfflineExecutionResult(
			repositorySucceeded
				? GloryPointsOfflineExecutionStatus.Executed
				: GloryPointsOfflineExecutionStatus.RepositoryFailed,
			plan.ObjectId,
			plan.Amount,
			plan.AddsDailyWeeklyStats,
			repositorySucceeded,
			"AbyssRankDAO.addGp offline player branch");
	}

	public static GloryPointsOfflineExecutionResult NotOfflineDaoUpdate(GloryPointsAddPlan plan)
	{
		return new GloryPointsOfflineExecutionResult(
			GloryPointsOfflineExecutionStatus.NotOfflineDaoUpdate,
			plan.ObjectId,
			plan.Amount,
			plan.AddsDailyWeeklyStats,
			RepositorySucceeded: false,
			"GloryPointsService.addGp online/zero branch skips AbyssRankDAO.addGp");
	}
}

public enum GloryPointsOfflineExecutionStatus
{
	Executed,
	RepositoryFailed,
	NotOfflineDaoUpdate,
}
