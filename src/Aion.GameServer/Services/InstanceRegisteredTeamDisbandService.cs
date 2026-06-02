using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class InstanceRegisteredTeamDisbandService
{
	public static bool IsRegisteredTeamDisbanded(
		WorldMapInstanceRuntimeState instance,
		PlayerGroupRuntime playerGroups,
		PlayerAllianceRuntime playerAlliances)
	{
		ArgumentNullException.ThrowIfNull(instance);
		ArgumentNullException.ThrowIfNull(playerGroups);
		ArgumentNullException.ThrowIfNull(playerAlliances);

		// Java parity: EmptyInstanceCheckerTask.isRegisteredTeamDisbanded checks
		// worldMapInstance.getRegisteredTeam() != null && registeredTeam.isDisbanded().
		// C# removes disbanded group/alliance runtime rows, while the instance keeps the registered team id.
		if (instance.RegisteredTeamId is not { } registeredTeamId)
			return false;

		return playerGroups.GetMemberObjectIds(registeredTeamId).Count == 0
			&& playerAlliances.GetMemberObjectIds(registeredTeamId).Count == 0;
	}
}
