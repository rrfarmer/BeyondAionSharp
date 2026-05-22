using Aion.GameServer.Dataholders;
using Aion.GameServer.World;

namespace Aion.GameServer.Model.GameObjects;

public sealed record PostmanNpc(
	int ObjectId,
	int TemplateId,
	NpcTemplateSummary Template,
	WorldPosition Position,
	int CreatorId,
	string MasterName,
	int State) : IWorldNpcObject
{
	public static PostmanNpc Create(Player owner, int objectId, NpcTemplateSummary template)
	{
		// Java parity: spawnengine/VisibleObjectSpawner.spawnPostman.
		var ownerPosition = owner.Position;
		var angle = ownerPosition.Heading * 3 * Math.PI / 180d;
		var x = ownerPosition.X + (float)(Math.Cos(angle) * 7);
		var y = ownerPosition.Y + (float)(Math.Sin(angle) * 7);
		var position = new WorldPosition(ownerPosition.WorldId, x, y, ownerPosition.Z, 0);
		return new PostmanNpc(
			objectId,
			template.TemplateId,
			template,
			position,
			owner.ObjectId,
			owner.Name,
			WorldNpcState.FromTemplateAndSpawn(template, spawnState: 0));
	}
}
