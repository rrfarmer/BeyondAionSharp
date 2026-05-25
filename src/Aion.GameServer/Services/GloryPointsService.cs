using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public static class GloryPointsService
{
	public static GloryPointsAddPlan AddGp(Player? player, int playerObjectId, int amount)
	{
		// Java parity: services/abyss/GloryPointsService.addGp(int, int).
		var plan = CreateAddGpPlan(player, playerObjectId, amount);
		if (player != null && plan.UpdatedRank != null)
			player.AbyssRank = plan.UpdatedRank;
		return plan;
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

		var oldGp = player.AbyssRank.Gp;
		var updatedRank = player.AbyssRank.AddGp(amount, addToStats);
		var added = updatedRank.Gp - oldGp;
		var packets = new List<GameServerPacket>
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
	IReadOnlyList<GameServerPacket> PlayerPackets,
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
		IReadOnlyList<GameServerPacket> playerPackets)
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
			Array.Empty<GameServerPacket>(),
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
			Array.Empty<GameServerPacket>(),
			"GloryPointsService.addGp zero GP guard");
	}
}

public enum GloryPointsAddStatus
{
	Applied,
	OfflineDaoUpdateRequired,
	NoReward,
}
