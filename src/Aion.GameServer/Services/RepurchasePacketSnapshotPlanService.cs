using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum RepurchasePacketSnapshotPlanStatus
{
	SnapshotCreated,
	BlockedMissingTemplate,
}

public sealed record RepurchasePacketSnapshotPlan(
	RepurchasePacketSnapshotPlanStatus Status,
	int TargetObjectId,
	IReadOnlyList<RepurchaseSourceItem> RepurchaseItems,
	IReadOnlyList<int> MissingTemplateItemIds,
	SmRepurchase? Packet,
	bool WouldQueryRepurchaseItems,
	bool DidQueryRepurchaseItems,
	bool WouldSendPacket,
	bool DidSendPacket,
	bool ShouldDispatchLiveSideEffects,
	string JavaSource,
	bool IsLive);

public static class RepurchasePacketSnapshotPlanService
{
	public static RepurchasePacketSnapshotPlan CreateDisabledPlan(
		int targetObjectId,
		IReadOnlyList<RepurchaseSourceItem> repurchaseItems,
		ItemTemplateTable itemTemplates)
	{
		// Java parity: SM_REPURCHASE(Player, npcId) snapshots
		// RepurchaseService.getRepurchaseItems(player.getObjectId()) in the
		// packet constructor, then writeImpl serializes the snapshot collection.
		var packetItems = new List<RepurchasePacketItem>();
		var missingTemplateIds = new List<int>();

		foreach (var repurchaseItem in repurchaseItems)
		{
			var template = itemTemplates.GetItemTemplate(repurchaseItem.Item.ItemId);
			if (template == null)
			{
				missingTemplateIds.Add(repurchaseItem.Item.ItemId);
				continue;
			}

			packetItems.Add(new RepurchasePacketItem(repurchaseItem.Item, template, repurchaseItem.RepurchasePrice));
		}

		if (missingTemplateIds.Count > 0)
		{
			return new RepurchasePacketSnapshotPlan(
				RepurchasePacketSnapshotPlanStatus.BlockedMissingTemplate,
				targetObjectId,
				repurchaseItems.ToArray(),
				missingTemplateIds.ToArray(),
				Packet: null,
				WouldQueryRepurchaseItems: true,
				DidQueryRepurchaseItems: false,
				WouldSendPacket: false,
				DidSendPacket: false,
				ShouldDispatchLiveSideEffects: false,
				"SM_REPURCHASE(Player, npcId) snapshot cannot be composed because supplied repurchase item facts lack item templates",
				IsLive: false);
		}

		return new RepurchasePacketSnapshotPlan(
			RepurchasePacketSnapshotPlanStatus.SnapshotCreated,
			targetObjectId,
			repurchaseItems.ToArray(),
			MissingTemplateItemIds: Array.Empty<int>(),
			new SmRepurchase(targetObjectId, packetItems),
			WouldQueryRepurchaseItems: true,
			DidQueryRepurchaseItems: false,
			WouldSendPacket: true,
			DidSendPacket: false,
			ShouldDispatchLiveSideEffects: false,
			"SM_REPURCHASE(Player, npcId) disabled snapshot over supplied RepurchaseService.getRepurchaseItems-equivalent facts; supplied collection order is preserved",
			IsLive: false);
	}
}
