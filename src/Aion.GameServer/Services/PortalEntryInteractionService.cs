using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.World;
using GameWorld = Aion.GameServer.World.World;

namespace Aion.GameServer.Services;

public sealed class PortalEntryInteractionService
{
	private readonly PlayerEnterWorldService _playerEnterWorldService;

	public PortalEntryInteractionService(PlayerEnterWorldService playerEnterWorldService)
	{
		_playerEnterWorldService = playerEnterWorldService;
	}

	public async Task<PortalDialogEntryResult> HandleDialogSelectAsync(
		Player player,
		int targetObjectId,
		int dialogActionId,
		int questId,
		GameWorld? world,
		PortalPathTable? portalPaths,
		PortalLocTable? portalLocs,
		InstanceCooltimeTable? instanceCooltimes,
		WorldMapRuntimeStateTable? worldMaps,
		ItemTemplateTable? itemTemplates,
		Func<GameServerPacket, CancellationToken, Task> sendPacketAsync,
		DateTimeOffset now,
		Func<Player, int, bool>? isKnownNpc = null,
		Func<Player, PortalLocSummary, CancellationToken, Task>? sameInstanceTeleportAsync = null,
		Func<Player, PortalEntryPreparationResult, CancellationToken, Task>? continuePortalTransferAsync = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: data/handlers/ai/portals/PortalDialogAI.onDialogSelect -> PortalService.port.
		if (questId != 0)
			return PortalDialogEntryResult.NotHandled(PortalDialogEntryStatus.QuestDialog);
		if (world == null
			|| (isKnownNpc?.Invoke(player, targetObjectId) == false)
			|| !world.TryGetObject(targetObjectId, out var target)
			|| target is not IWorldNpcObject npc)
		{
			return PortalDialogEntryResult.NotHandled(PortalDialogEntryStatus.UnknownTarget);
		}
		if (!IsInTalkRange(player, npc))
			return PortalDialogEntryResult.CreateHandled(PortalDialogEntryStatus.TooFar, null);
		if (portalPaths == null || portalLocs == null || instanceCooltimes == null || worldMaps == null || itemTemplates == null)
			return PortalDialogEntryResult.CreateHandled(PortalDialogEntryStatus.MissingStaticData, null);

		var portalPath = portalPaths.GetPortalDialogPath(npc.TemplateId, dialogActionId, player.Race);
		if (portalPath == null)
			return PortalDialogEntryResult.NotHandled(PortalDialogEntryStatus.NoPortalPath);

		var preparation = await _playerEnterWorldService.PreparePortalEntryAsync(
			player,
			portalPath,
			portalLocs,
			instanceCooltimes,
			worldMaps,
			itemTemplates,
			now,
			npc.ObjectId,
			npcIsDialogNpc: npc.Template.IsDialogNpc,
			cancellationToken: cancellationToken);

		if (preparation.Status == PortalEntryPreparationStatus.ValidationRejected)
		{
			if (preparation.EntryPlan.FailurePacket != null)
				await sendPacketAsync(preparation.EntryPlan.FailurePacket, cancellationToken);
			return PortalDialogEntryResult.CreateHandled(PortalDialogEntryStatus.ValidationRejected, preparation);
		}
		if (preparation.Status == PortalEntryPreparationStatus.UnsupportedTeamPortal)
			return PortalDialogEntryResult.CreateHandled(PortalDialogEntryStatus.UnsupportedTeamPortal, preparation);

		if (preparation.Status != PortalEntryPreparationStatus.Ready)
			return PortalDialogEntryResult.CreateHandled(MapPreparationFailure(preparation.Status), preparation);

		foreach (var packet in preparation.Packets)
			await sendPacketAsync(packet, cancellationToken);

		if (preparation.EntryPlan.Action == PortalEntryPlanAction.SameInstanceTeleport
			&& preparation.EntryPlan.PortalLoc != null
			&& sameInstanceTeleportAsync != null)
		{
			await sameInstanceTeleportAsync(player, preparation.EntryPlan.PortalLoc, cancellationToken);
		}
		else if (preparation.EntryPlan.Action == PortalEntryPlanAction.Continue
			&& continuePortalTransferAsync != null)
		{
			await continuePortalTransferAsync(player, preparation, cancellationToken);
		}

		return PortalDialogEntryResult.CreateHandled(PortalDialogEntryStatus.Ready, preparation);
	}

	private static bool IsInTalkRange(Player player, IWorldNpcObject npc)
	{
		// Java parity: controllers/NpcController.onDialogSelect requires PositionUtil.isInTalkRange before AI dispatch.
		return PositionUtilService.IsInNpcTalkRange(
			player.Position,
			npc.Position,
			npc.Template.TalkDistance,
			npc.Template.BoundRadius);
	}

	private static PortalDialogEntryStatus MapPreparationFailure(PortalEntryPreparationStatus status)
	{
		return status switch
		{
			PortalEntryPreparationStatus.RequirementApplicationFailed => PortalDialogEntryStatus.RequirementApplicationFailed,
			PortalEntryPreparationStatus.RequirementPersistenceFailed => PortalDialogEntryStatus.RequirementPersistenceFailed,
			_ => PortalDialogEntryStatus.UnknownPreparationStatus,
		};
	}
}

public sealed record PortalDialogEntryResult(
	bool Handled,
	PortalDialogEntryStatus Status,
	PortalEntryPreparationResult? Preparation)
{
	public static PortalDialogEntryResult CreateHandled(
		PortalDialogEntryStatus status,
		PortalEntryPreparationResult? preparation)
	{
		return new PortalDialogEntryResult(true, status, preparation);
	}

	public static PortalDialogEntryResult NotHandled(PortalDialogEntryStatus status)
	{
		return new PortalDialogEntryResult(false, status, null);
	}
}

public enum PortalDialogEntryStatus
{
	Ready,
	ValidationRejected,
	UnsupportedTeamPortal,
	RequirementApplicationFailed,
	RequirementPersistenceFailed,
	MissingStaticData,
	TooFar,
	NoPortalPath,
	QuestDialog,
	UnknownTarget,
	UnknownPreparationStatus,
}
