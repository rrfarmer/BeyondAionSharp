using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public static class PlayerGroupSnapshotResolver
{
	public static PlayerGroupSnapshot? Resolve(Player player)
	{
		// Java parity: model/gameobjects/player/Player.getPlayerGroup feeding PlayerGroup.getTeamId/getMembers in PortalService.port.
		if (player.TeamMembership != PlayerTeamMembership.Group)
			return null;

		if (player.CurrentGroupSnapshot != null)
			return player.CurrentGroupSnapshot;

		if (player.CurrentTeamId == 0 && player.CurrentTeamMemberObjectIds.Count == 0)
			return null;

		return new PlayerGroupSnapshot(player.CurrentTeamId, player.CurrentTeamMemberObjectIds);
	}
}
