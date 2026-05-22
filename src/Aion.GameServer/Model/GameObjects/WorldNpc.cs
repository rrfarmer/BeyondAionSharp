using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

// Java parity: model/gameobjects/Npc visible object state needed by Player.isTargetingNpcWithFunction.
public interface IWorldNpcObject
{
	int ObjectId { get; }

	int TemplateId { get; }

	NpcTemplateSummary Template { get; }

	WorldPosition Position { get; }

	int State { get; }
}

public sealed record WorldNpc(
	int ObjectId,
	int TemplateId,
	NpcTemplateSummary Template,
	WorldPosition Position,
	int State = WorldNpcState.DefaultSpawnState) : IWorldNpcObject;

public static class WorldNpcState
{
	public const int Active = 1;
	public const int WalkMode = 1 << 6;
	public const int DefaultSpawnState = Active | WalkMode;

	public static int FromTemplateAndSpawn(NpcTemplateSummary template, int spawnState)
	{
		// Java parity: controllers/NpcController.onBeforeSpawn applies template state, then SpawnTemplate state.
		if (spawnState > 0)
			return spawnState;

		return template.State > 0 ? template.State : DefaultSpawnState;
	}
}
