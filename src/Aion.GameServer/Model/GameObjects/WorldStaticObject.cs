using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

public sealed record WorldStaticObject(
	int ObjectId,
	int TemplateId,
	ItemTemplateSummary Template,
	WorldPosition Position,
	int StaticId = 0,
	WorldPosition? SpawnPosition = null)
{
	// Java parity: model/gameobjects/StaticObject is created from a SpawnTemplate and item VisibleObjectTemplate.
	public WorldPosition SpawnLocation => SpawnPosition ?? Position;
}
