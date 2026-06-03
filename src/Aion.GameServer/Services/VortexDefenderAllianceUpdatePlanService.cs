using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class VortexDefenderAllianceUpdatePlanService
{
	public VortexDefenderAllianceUpdatePlan CreatePlan(
		VortexLocationSummary location,
		IReadOnlyList<VortexZonePlayerSnapshot>? zonePlayers)
	{
		ArgumentNullException.ThrowIfNull(location);

		var players = zonePlayers ?? [];
		var defenders = players
			.Where(player => string.Equals(player.Race, location.DefendersRace, StringComparison.Ordinal))
			.ToArray();
		var skipped = players
			.Where(player => !string.Equals(player.Race, location.DefendersRace, StringComparison.Ordinal))
			.ToArray();

		return new VortexDefenderAllianceUpdatePlan(
			VortexDefenderAllianceUpdatePlanStatus.Planned,
			location.Id,
			location.DefendersRace,
			defenders,
			skipped,
			JavaSource: "services/vortex/Invasion.updateAlliance -> services/vortex/Invasion.updateDefenders");
	}
}

public enum VortexDefenderAllianceUpdatePlanStatus
{
	Planned,
}

public sealed record VortexDefenderAllianceUpdatePlan(
	VortexDefenderAllianceUpdatePlanStatus Status,
	int LocationId,
	string DefendersRace,
	IReadOnlyList<VortexZonePlayerSnapshot> DefenderUpdatePlayers,
	IReadOnlyList<VortexZonePlayerSnapshot> SkippedPlayers,
	string JavaSource)
{
	public IReadOnlyList<int> DefenderObjectIds => DefenderUpdatePlayers.Select(player => player.PlayerObjectId).ToArray();
	public IReadOnlyList<int> SkippedObjectIds => SkippedPlayers.Select(player => player.PlayerObjectId).ToArray();
	public bool ShouldMutateLiveAlliance => false;
	public bool WouldCallUpdateDefenders => DefenderUpdatePlayers.Count > 0;
}

public sealed record VortexZonePlayerSnapshot(
	int PlayerObjectId,
	string Race,
	bool IsOnline = true)
{
	public static VortexZonePlayerSnapshot FromPlayer(Player player)
	{
		ArgumentNullException.ThrowIfNull(player);
		return new VortexZonePlayerSnapshot(player.ObjectId, player.Race, player.IsOnline);
	}
}
