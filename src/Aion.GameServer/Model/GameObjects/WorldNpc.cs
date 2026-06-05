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

// Java parity: model/gameobjects/Pet visible object facts needed by CM_BUY_ITEM
// action 17 pet merchant dispatch.
public interface IWorldPetObject
{
	int ObjectId { get; }

	bool HasMerchantFunction { get; }

	int? MerchantSellModifier { get; }
}

public sealed record WorldNpc(
	int ObjectId,
	int TemplateId,
	NpcTemplateSummary Template,
	WorldPosition Position,
	int State = WorldNpcState.DefaultSpawnState,
	string AiName = "",
	int RespawnSeconds = 0,
	int StaticId = 0,
	int RandomWalkRange = 0,
	string WalkerId = "",
	int WalkerIndex = 0,
	string Anchor = "",
	WorldPosition? SpawnPosition = null) : IWorldNpcObject
{
	// Java parity: model/templates/spawns/SpawnTemplate remains the source for walker grouping and respawn placement after runtime position changes.
	public WorldPosition SpawnLocation => SpawnPosition ?? Position;
}

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
