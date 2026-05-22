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

	string AiName { get; }
}

public sealed record WorldNpc(
	int ObjectId,
	int TemplateId,
	NpcTemplateSummary Template,
	WorldPosition Position,
	int State = WorldNpcState.DefaultSpawnState,
	string AiName = "") : IWorldNpcObject;

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

public static class WorldNpcAiName
{
	private const string NoAi = "__NO_AI__";

	public static string FromTemplateAndSpawn(NpcTemplateSummary template, string spawnAiName)
	{
		// Java parity: model/gameobjects/Creature constructor applies SpawnTemplate.NO_AI as a null AI override.
		if (string.IsNullOrWhiteSpace(spawnAiName))
			return template.AiName;

		return string.Equals(spawnAiName, NoAi, StringComparison.Ordinal) ? string.Empty : spawnAiName;
	}
}
