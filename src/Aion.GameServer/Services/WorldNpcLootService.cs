using Aion.GameServer.Model;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public sealed class WorldNpcLootService
{
	private readonly WorldNpcDropRegistrationService _dropRegistrationService;

	public WorldNpcLootService(WorldNpcDropRegistrationService dropRegistrationService)
	{
		_dropRegistrationService = dropRegistrationService;
	}

	public WorldNpcLootResult RequestDropList(Player? player, int npcObjectId)
	{
		// Java parity: services/drop/DropService.requestDropList.
		if (player == null || !_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.UnknownDrop);

		var packets = new List<GameServerPacket>();
		var visiblePackets = new List<GameServerPacket>();
		if (player.IsLooting)
		{
			var closeResult = CloseDropList(player, player.LootingNpcObjectId);
			packets.AddRange(closeResult.PlayerPackets);
			visiblePackets.AddRange(closeResult.VisiblePlayerPackets);
		}

		if (!registration.IsAllowedToLoot(player.ObjectId))
		{
			packets.Add(SmSystemMessage.LootNoRight());
			return new WorldNpcLootResult(WorldNpcLootStatus.NoRight, packets, visiblePackets);
		}

		if (!registration.TryBeginLooting(player.ObjectId, out _))
		{
			packets.Add(SmSystemMessage.LootFailOnLooting());
			return new WorldNpcLootResult(WorldNpcLootStatus.AlreadyLooted, packets, visiblePackets);
		}

		var dropItems = _dropRegistrationService.GetCurrentDrops(npcObjectId);
		packets.Add(new SmLootItemList(npcObjectId, dropItems, player));
		packets.Add(new SmLootStatus(npcObjectId, SmLootStatusType.OpenDropList));
		player.StartLooting(npcObjectId);
		visiblePackets.Add(new SmEmotion(player, EmotionType.StartLoot, 0, npcObjectId));

		return new WorldNpcLootResult(WorldNpcLootStatus.Opened, packets, visiblePackets);
	}

	public WorldNpcLootResult CloseDropList(Player? player, int npcObjectId)
	{
		// Java parity: services/drop/DropService.closeDropList.
		if (player == null)
			return WorldNpcLootResult.None(WorldNpcLootStatus.NoPlayer);

		var wasLootingThisNpc = player.IsLooting && player.LootingNpcObjectId == npcObjectId;
		player.StopLooting();
		var visiblePackets = new List<GameServerPacket>
		{
			new SmEmotion(player, EmotionType.EndLoot, 0, npcObjectId),
		};

		if (!_dropRegistrationService.TryGetRegistration(npcObjectId, out var registration) || registration == null)
			return new WorldNpcLootResult(WorldNpcLootStatus.ClosedMissingRegistration, Array.Empty<GameServerPacket>(), visiblePackets);

		if (!wasLootingThisNpc || !registration.ClearLootingPlayer(player.ObjectId))
			return new WorldNpcLootResult(WorldNpcLootStatus.CloseRejected, Array.Empty<GameServerPacket>(), visiblePackets);

		return new WorldNpcLootResult(WorldNpcLootStatus.Closed, Array.Empty<GameServerPacket>(), visiblePackets);
	}

	public SmLootStatus CreateLootEnableStatus(int npcObjectId)
	{
		// Java parity: SM_LOOT_STATUS(Status.LOOT_ENABLE) chooses the first non-zero DropItem loot effect.
		var lootEffectId = _dropRegistrationService.GetCurrentDrops(npcObjectId)
			.Select(drop => drop.LootEffectId)
			.FirstOrDefault(effectId => effectId != 0);
		return new SmLootStatus(npcObjectId, SmLootStatusType.LootEnable, lootEffectId);
	}
}

public sealed record WorldNpcLootResult(
	WorldNpcLootStatus Status,
	IReadOnlyList<GameServerPacket> PlayerPackets,
	IReadOnlyList<GameServerPacket> VisiblePlayerPackets)
{
	public static WorldNpcLootResult None(WorldNpcLootStatus status)
	{
		return new WorldNpcLootResult(status, Array.Empty<GameServerPacket>(), Array.Empty<GameServerPacket>());
	}
}

public enum WorldNpcLootStatus
{
	NoPlayer,
	UnknownDrop,
	NoRight,
	AlreadyLooted,
	Opened,
	Closed,
	ClosedMissingRegistration,
	CloseRejected,
}
