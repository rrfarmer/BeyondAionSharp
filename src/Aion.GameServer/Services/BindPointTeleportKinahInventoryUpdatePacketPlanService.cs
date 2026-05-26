using Aion.GameServer.Dataholders;
using Aion.GameServer.Network.Aion.ServerPackets;

namespace Aion.GameServer.Services;

public enum BindPointTeleportKinahInventoryUpdatePacketPlanStatus
{
	NoPacket,
	MissingTemplate,
	PacketReady,
}

public sealed record BindPointTeleportKinahInventoryUpdatePacketPlan(
	BindPointTeleportKinahInventoryUpdatePacketPlanStatus Status,
	BindPointTeleportKinahPersistenceDecision Decision,
	SmInventoryUpdateItem? Packet,
	bool ShouldSendPacket,
	int? UpdateType,
	string JavaSource,
	bool IsLive);

public static class BindPointTeleportKinahInventoryUpdatePacketPlanService
{
	public static BindPointTeleportKinahInventoryUpdatePacketPlan CreatePlan(
		BindPointTeleportKinahPersistenceDecision decision,
		ItemTemplateSummary? kinahTemplate)
	{
		// Java parity: ItemPacketService.sendItemUpdatePacket builds
		// SM_INVENTORY_UPDATE_ITEM(player, item, DEC_KINAH_FLY) for cube Kinah updates.
		// This planner only creates the packet intent; it never sends to a client.
		if (decision.Status != BindPointTeleportKinahPersistenceDecisionStatus.ContinueAfterPersistence
			|| !decision.ShouldEmitKinahInventoryUpdatePacket
			|| decision.KinahItemUpdate == null
			|| decision.KinahInventoryUpdateType == null)
		{
			return new BindPointTeleportKinahInventoryUpdatePacketPlan(
				BindPointTeleportKinahInventoryUpdatePacketPlanStatus.NoPacket,
				decision,
				Packet: null,
				ShouldSendPacket: false,
				UpdateType: null,
				"Scheduled bind-point Kinah update packet is suppressed unless persistence decision is ContinueAfterPersistence with Kinah update metadata",
				IsLive: false);
		}

		if (kinahTemplate == null)
		{
			return new BindPointTeleportKinahInventoryUpdatePacketPlan(
				BindPointTeleportKinahInventoryUpdatePacketPlanStatus.MissingTemplate,
				decision,
				Packet: null,
				ShouldSendPacket: false,
				decision.KinahInventoryUpdateType,
				"C# staging guard: SM_INVENTORY_UPDATE_ITEM requires the Kinah item template before a packet intent can be built",
				IsLive: false);
		}

		return new BindPointTeleportKinahInventoryUpdatePacketPlan(
			BindPointTeleportKinahInventoryUpdatePacketPlanStatus.PacketReady,
			decision,
			new SmInventoryUpdateItem(
				decision.KinahItemUpdate,
				kinahTemplate,
				decision.KinahInventoryUpdateType.Value),
			ShouldSendPacket: true,
			decision.KinahInventoryUpdateType,
			"ItemPacketService.sendItemUpdatePacket -> SM_INVENTORY_UPDATE_ITEM(player, item, DEC_KINAH_FLY); C# packet intent remains non-sending",
			IsLive: false);
	}
}
