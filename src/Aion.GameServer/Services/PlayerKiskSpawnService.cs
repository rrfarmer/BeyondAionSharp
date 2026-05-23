using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.World;

namespace Aion.GameServer.Services;

public static class PlayerKiskSpawnService
{
	public static PlayerKiskSpawnPlan CreatePlan(
		Player player,
		InventoryItem sourceItem,
		NpcTemplateSummary kiskTemplate,
		int kiskObjectId)
	{
		// Java parity: model/templates/item/actions/ToyPetSpawnAction.act + spawnengine/VisibleObjectSpawner.spawnKisk.
		var spawnPosition = CreateSpawnPosition(player.Position);
		var kisk = new WorldNpc(
			kiskObjectId,
			kiskTemplate.TemplateId,
			kiskTemplate,
			spawnPosition,
			WorldNpcState.FromTemplateAndSpawn(kiskTemplate, spawnState: 0),
			WorldNpcAiName.FromTemplateAndSpawn(kiskTemplate, string.Empty),
			SpawnPosition: spawnPosition);
		var sourceItemUpdate = sourceItem.Count > 1 ? CopyInventoryItem(sourceItem, sourceItem.Count - 1) : null;
		int? deletedSourceItemObjectId = sourceItem.Count <= 1 ? sourceItem.ObjectId : null;

		return new PlayerKiskSpawnPlan(
			kisk,
			PlayerKiskRuntimeState.FromTemplate(kiskObjectId, player.ObjectId, kiskTemplate, player.Race, player.LegionId),
			sourceItemUpdate,
			deletedSourceItemObjectId);
	}

	public static WorldPosition CreateSpawnPosition(WorldPosition playerPosition)
	{
		return playerPosition with { Heading = (byte)((playerPosition.Heading + 60) % 120) };
	}

	private static InventoryItem CopyInventoryItem(InventoryItem item, long count)
	{
		return new InventoryItem
		{
			ObjectId = item.ObjectId,
			ItemId = item.ItemId,
			Count = count,
			Color = item.Color,
			ColorExpires = item.ColorExpires,
			Creator = item.Creator,
			ExpireTime = item.ExpireTime,
			ActivationCount = item.ActivationCount,
			OwnerId = item.OwnerId,
			IsEquipped = item.IsEquipped,
			IsSoulBound = item.IsSoulBound,
			Slot = item.Slot,
			Location = item.Location,
			Enchant = item.Enchant,
			EnchantBonus = item.EnchantBonus,
			ItemSkin = item.ItemSkin,
			FusionedItem = item.FusionedItem,
			OptionalSocket = item.OptionalSocket,
			OptionalFusionSocket = item.OptionalFusionSocket,
			Charge = item.Charge,
			TuneCount = item.TuneCount,
			RandomBonus = item.RandomBonus,
			FusionRandomBonus = item.FusionRandomBonus,
			Tempering = item.Tempering,
			PackCount = item.PackCount,
			IsAmplified = item.IsAmplified,
			BuffSkill = item.BuffSkill,
			RandomPlumeBonus = item.RandomPlumeBonus,
			ManaStones = item.ManaStones,
			FusionStones = item.FusionStones,
			Godstone = item.Godstone,
			IdianStone = item.IdianStone,
		};
	}
}

public sealed record PlayerKiskSpawnPlan(
	WorldNpc Kisk,
	PlayerKiskRuntimeState RuntimeState,
	InventoryItem? SourceItemUpdate,
	int? DeletedSourceItemObjectId)
{
	public PlayerKiskOwnership Ownership => RuntimeState.Ownership;
}
