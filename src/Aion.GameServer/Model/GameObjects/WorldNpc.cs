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
}

public sealed record WorldNpc(
	int ObjectId,
	int TemplateId,
	NpcTemplateSummary Template,
	WorldPosition Position) : IWorldNpcObject;
