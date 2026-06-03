using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public sealed class VortexRiftGeneratorLookupPlanService
{
	public const int GeneratorNpcIdA = 209487;
	public const int GeneratorNpcIdB = 209486;

	public VortexRiftGeneratorLookupPlan CreatePlan(
		VortexLocationSummary location,
		IReadOnlyList<VortexStartSpawnedNpcSnapshot>? spawnedNpcs)
	{
		ArgumentNullException.ThrowIfNull(location);

		var candidates = (spawnedNpcs ?? [])
			.Where(IsGeneratorNpc)
			.ToArray();
		var selected = candidates.LastOrDefault();
		if (selected == null)
		{
			return new VortexRiftGeneratorLookupPlan(
				VortexRiftGeneratorLookupPlanStatus.MissingGenerator,
				location.Id,
				location.HomePoint.WorldId,
				candidates,
				SelectedGenerator: null,
				JavaExceptionMessage: $"No generator was found in loc:{location.Id}",
				JavaSource: "services/vortex/DimensionalVortex.initRiftGenerator");
		}

		return new VortexRiftGeneratorLookupPlan(
			VortexRiftGeneratorLookupPlanStatus.Planned,
			location.Id,
			location.HomePoint.WorldId,
			candidates,
			selected,
			JavaSource: "services/vortex/DimensionalVortex.initRiftGenerator -> Npc.getObserveController().attach(DeathObserver)");
	}

	private static bool IsGeneratorNpc(VortexStartSpawnedNpcSnapshot spawnedNpc)
	{
		return spawnedNpc.NpcId == GeneratorNpcIdA || spawnedNpc.NpcId == GeneratorNpcIdB;
	}
}

public enum VortexRiftGeneratorLookupPlanStatus
{
	MissingGenerator,
	Planned,
}

public sealed record VortexRiftGeneratorLookupPlan(
	VortexRiftGeneratorLookupPlanStatus Status,
	int LocationId,
	int HomeWorldId,
	IReadOnlyList<VortexStartSpawnedNpcSnapshot> CandidateGenerators,
	VortexStartSpawnedNpcSnapshot? SelectedGenerator,
	string JavaExceptionMessage = "",
	string JavaSource = "")
{
	public bool HasGenerator => SelectedGenerator != null;
	public bool ShouldAttachLiveDeathObserver => false;
	public bool WouldStopInvasionOnGeneratorDeath => Status == VortexRiftGeneratorLookupPlanStatus.Planned;
}
