using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDropRegistrationWorkflowService
{
	private readonly WorldNpcCustomDropService _customDropService;
	private readonly WorldNpcDropRegistrationService _dropRegistrationService;
	private readonly WorldNpcLootBroadcastService _lootBroadcastService;
	private readonly WorldNpcDropModifierService _dropModifierService;
	private readonly WorldNpcQuestDropService? _questDropService;

	public WorldNpcDropRegistrationWorkflowService(
		WorldNpcCustomDropService customDropService,
		WorldNpcDropRegistrationService dropRegistrationService,
		WorldNpcLootBroadcastService lootBroadcastService,
		WorldNpcDropModifierService? dropModifierService = null,
		WorldNpcQuestDropService? questDropService = null)
	{
		_customDropService = customDropService;
		_dropRegistrationService = dropRegistrationService;
		_lootBroadcastService = lootBroadcastService;
		_dropModifierService = dropModifierService ?? new WorldNpcDropModifierService();
		_questDropService = questDropService;
	}

	public async ValueTask<WorldNpcDropRegistrationWorkflowResult> RegisterCustomDropsAsync(
		IWorldNpcObject? npc,
		Player? looter,
		IReadOnlyList<Player>? groupMembers = null,
		WorldNpcDropModifiers? dropModifiers = null,
		int? highestLevel = null,
		TimeSpan? freeForAllDelay = null,
		CancellationToken cancellationToken = default)
	{
		// Java parity: services/drop/DropRegistrationService.registerDrop custom-drop slice, through currentDropMap/dropRegistrationMap and fanout.
		if (npc == null)
			return WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.MissingNpc);
		if (looter == null)
			return WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.MissingLooter);

		var generated = _customDropService.CreateDrops(
			npc.ObjectId,
			npc.TemplateId,
			dropModifiers ?? _dropModifierService.CreateModifiers(npc, looter, highestLevel));
		var questDrops = _questDropService?.CreateDrops(npc, looter, groupMembers, generated.NextIndex)
			?? WorldNpcQuestDropResult.Empty(generated.NextIndex);
		var droppedItems = generated.Drops.Concat(questDrops.Drops).ToArray();
		if (droppedItems.Length == 0)
			return WorldNpcDropRegistrationWorkflowResult.Skipped(WorldNpcDropRegistrationWorkflowStatus.NoGeneratedDrops);

		_dropRegistrationService.RegisterDrop(npc.ObjectId, looter.ObjectId, droppedItems, questDrops.AllowedLooterObjectIds);
		var fanout = await _lootBroadcastService.StartRegisteredDropFanoutAsync(npc, freeForAllDelay, cancellationToken);
		return WorldNpcDropRegistrationWorkflowResult.Registered(droppedItems, fanout);
	}
}

public sealed record WorldNpcDropRegistrationWorkflowResult(
	WorldNpcDropRegistrationWorkflowStatus Status,
	IReadOnlyList<WorldNpcDropItem> Drops,
	WorldNpcRegisteredDropFanoutResult? Fanout)
{
	public static WorldNpcDropRegistrationWorkflowResult Registered(
		IReadOnlyList<WorldNpcDropItem> drops,
		WorldNpcRegisteredDropFanoutResult fanout)
	{
		return new WorldNpcDropRegistrationWorkflowResult(
			WorldNpcDropRegistrationWorkflowStatus.Registered,
			drops,
			fanout);
	}

	public static WorldNpcDropRegistrationWorkflowResult Skipped(WorldNpcDropRegistrationWorkflowStatus status)
	{
		return new WorldNpcDropRegistrationWorkflowResult(status, Array.Empty<WorldNpcDropItem>(), null);
	}
}

public enum WorldNpcDropRegistrationWorkflowStatus
{
	MissingNpc,
	MissingLooter,
	NoGeneratedDrops,
	Registered,
}
