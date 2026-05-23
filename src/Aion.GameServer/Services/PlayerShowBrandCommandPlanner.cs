using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class PlayerShowBrandCommandPlanner(
	PlayerGroupRuntime groups,
	PlayerAllianceRuntime alliances)
{
	public PlayerShowBrandCommandPlan CreatePlan(Player player, int brandId, int targetObjectId)
	{
		// Java parity: network/aion/clientpackets/CM_SHOW_BRAND.runImpl echoes solo brands or updates the current team when the caller has rights.
		if (player.TeamMembership == PlayerTeamMembership.None)
		{
			return new PlayerShowBrandCommandPlan(
				PlayerShowBrandCommandPlanStatus.SoloEcho,
				player.ObjectId,
				brandId,
				targetObjectId,
				new PlayerShowBrandIntent(player.ObjectId, brandId, targetObjectId));
		}

		if (player.TeamMembership == PlayerTeamMembership.Group)
		{
			var group = groups.Resolve(player);
			if (group == null || groups.GetDescriptor(group.TeamId) == null)
				return PlayerShowBrandCommandPlan.TeamMissing(player.ObjectId, brandId, targetObjectId);

			if (!groups.IsLeader(group.TeamId, player))
				return PlayerShowBrandCommandPlan.NotAuthorized(player.ObjectId, brandId, targetObjectId);

			return new PlayerShowBrandCommandPlan(
				PlayerShowBrandCommandPlanStatus.GroupUpdated,
				player.ObjectId,
				brandId,
				targetObjectId,
				SoloEchoIntent: null,
				groups.UpdateBrand(group.TeamId, brandId, targetObjectId));
		}

		if (player.TeamMembership == PlayerTeamMembership.Alliance)
		{
			var alliance = alliances.Resolve(player);
			if (alliance == null || alliances.GetDescriptor(alliance.AllianceId) == null)
				return PlayerShowBrandCommandPlan.TeamMissing(player.ObjectId, brandId, targetObjectId);

			if (!alliances.IsLeader(alliance.AllianceId, player)
				&& !alliances.IsViceCaptain(alliance.AllianceId, player.ObjectId))
				return PlayerShowBrandCommandPlan.NotAuthorized(player.ObjectId, brandId, targetObjectId);

			return new PlayerShowBrandCommandPlan(
				PlayerShowBrandCommandPlanStatus.AllianceUpdated,
				player.ObjectId,
				brandId,
				targetObjectId,
				SoloEchoIntent: null,
				GroupUpdatePlan: null,
				alliances.UpdateBrand(alliance.AllianceId, brandId, targetObjectId));
		}

		return PlayerShowBrandCommandPlan.TeamMissing(player.ObjectId, brandId, targetObjectId);
	}
}

public enum PlayerShowBrandCommandPlanStatus
{
	SoloEcho,
	GroupUpdated,
	AllianceUpdated,
	NotAuthorized,
	TeamMissing,
}

public sealed record PlayerShowBrandCommandPlan(
	PlayerShowBrandCommandPlanStatus Status,
	int CallerObjectId,
	int BrandId,
	int TargetObjectId,
	PlayerShowBrandIntent? SoloEchoIntent = null,
	PlayerGroupBrandUpdatePlan? GroupUpdatePlan = null,
	PlayerAllianceBrandUpdatePlan? AllianceUpdatePlan = null)
{
	public static PlayerShowBrandCommandPlan NotAuthorized(int callerObjectId, int brandId, int targetObjectId)
	{
		return new PlayerShowBrandCommandPlan(PlayerShowBrandCommandPlanStatus.NotAuthorized, callerObjectId, brandId, targetObjectId);
	}

	public static PlayerShowBrandCommandPlan TeamMissing(int callerObjectId, int brandId, int targetObjectId)
	{
		return new PlayerShowBrandCommandPlan(PlayerShowBrandCommandPlanStatus.TeamMissing, callerObjectId, brandId, targetObjectId);
	}
}

public sealed record PlayerShowBrandIntent(
	int RecipientObjectId,
	int BrandId,
	int TargetObjectId)
{
	public SmShowBrand CreatePacket()
	{
		// Java parity: CM_SHOW_BRAND sends SM_SHOW_BRAND directly back to solo players without storing team state.
		return new SmShowBrand(BrandId, TargetObjectId);
	}
}
