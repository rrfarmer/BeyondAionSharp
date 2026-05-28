namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind
{
	ClientPacketDirectStopCaller,
	TeleportAnimationDoneRunnableFuture,
	ControllerProtectionLifecycle,
	ControllerTaskMap,
	TeleportFutureTaskRegistration,
	PacketSerializationCaptureObserver,
	PacketCaptureObserverDependency,
}

public sealed record PlayerProtectionActiveTaskStopTriggerJavaHookDetailRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind Kind,
	string JavaArtifact,
	string JavaSourcePath,
	string JavaMethod,
	string ObserverEvent,
	bool RequiresJavaChange,
	bool RequiresProtectionArtifactSerializer,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerJavaHookDetailReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerJavaHookDetailRow> Rows,
	bool HasDirectStopPacketCallers,
	bool HasTeleportRunnableFutureHook,
	bool HasProtectionLifecycleHook,
	bool HasTaskMapHooks,
	bool HasTeleportFutureRegistrationHook,
	bool HasGenericPacketSerializationObserver,
	bool NeedsProtectionArtifactSerializer,
	bool NeedsJavaObserverImplementation,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: read-only hook detail map for future protection stop-trigger Java observers.
/// The Java implementation remains the oracle; this report records source locations only.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportService
{
	public static PlayerProtectionActiveTaskStopTriggerJavaHookDetailReport Create()
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerJavaHookDetailRow>();

		AddDirectStopPacketCallers(rows);

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.TeleportAnimationDoneRunnableFuture,
			"com.aionemu.gameserver.network.aion.clientpackets.CM_TELEPORT_ANIMATION_DONE",
			"game-server/src/com/aionemu/gameserver/network/aion/clientpackets/CM_TELEPORT_ANIMATION_DONE.java",
			"runImpl",
			"teleport_animation_task_dispatch",
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			"Removes TaskId.TELEPORT, runs unfinished RunnableFuture immediately, calls get() to surface exceptions, and falls back to SM_PLAYER_INFO plus World.spawn when the player is still unspawned.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerProtectionLifecycle,
			"com.aionemu.gameserver.controllers.PlayerController",
			"game-server/src/com/aionemu/gameserver/controllers/PlayerController.java",
			"startProtectionActiveTask",
			"protection_active_task_start",
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			"Sets BLINKING, cancels cast/target state, broadcasts SM_PLAYER_STATE to sighted players, and schedules stopProtectionActiveTask after 60000 ms.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerProtectionLifecycle,
			"com.aionemu.gameserver.controllers.PlayerController",
			"game-server/src/com/aionemu/gameserver/controllers/PlayerController.java",
			"stopProtectionActiveTask",
			"protection_active_task_stop",
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			"Cancels TaskId.PROTECTION_ACTIVE, unsets BLINKING only while spawned, broadcasts SM_PLAYER_STATE, and notifies AI movement.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerTaskMap,
			"com.aionemu.gameserver.controllers.CreatureController",
			"game-server/src/com/aionemu/gameserver/controllers/CreatureController.java",
			"getAndRemoveTask",
			"controller_task_get_and_remove",
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			"Removes the Future from the ConcurrentHashMap by TaskId ordinal; ordering and race behavior remain Java-concurrency-sensitive.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerTaskMap,
			"com.aionemu.gameserver.controllers.CreatureController",
			"game-server/src/com/aionemu/gameserver/controllers/CreatureController.java",
			"cancelTask",
			"controller_task_cancel",
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			"Removes the Future and calls cancel(false) when present; interruption and completion semantics need runtime evidence.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerTaskMap,
			"com.aionemu.gameserver.controllers.CreatureController",
			"game-server/src/com/aionemu/gameserver/controllers/CreatureController.java",
			"addTask",
			"controller_task_add_or_replace",
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			"Uses ConcurrentHashMap.compute and cancels the previous Future before storing the replacement.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.TeleportFutureTaskRegistration,
			"com.aionemu.gameserver.services.teleport.TeleportService",
			"game-server/src/com/aionemu/gameserver/services/teleport/TeleportService.java",
			"sendLoc",
			"teleport_future_task_registered",
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			"Registers new FutureTask<Void>(spawnTask, null) under TaskId.TELEPORT after sending SM_TELEPORT_LOC for non-instant teleport animations.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.PacketSerializationCaptureObserver,
			"com.aionemu.gameserver.network.aion.AionServerPacket",
			"game-server/src/com/aionemu/gameserver/network/aion/AionServerPacket.java",
			"write",
			"server_packet_clear_frame_serialized",
			requiresJavaChange: false,
			requiresProtectionArtifactSerializer: true,
			"Generic capture observer already exists around clear-frame serialization before encryption, but protection stop-trigger schema-v1 artifact serialization is still missing.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.PacketCaptureObserverDependency,
			"com.aionemu.gameserver.network.aion.capture.ServerPacketCaptureObserver",
			"game-server/src/com/aionemu/gameserver/network/aion/capture/ServerPacketCaptureObserver.java",
			"onPacketSerialized",
			"server_packet_capture_observer_callback",
			requiresJavaChange: false,
			requiresProtectionArtifactSerializer: true,
			"Observer contract exposes AionConnection, AionServerPacket, and clear-frame ByteBuffer; no protection-specific JSON artifact writer is wired.");

		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.PacketCaptureObserverDependency,
			"com.aionemu.gameserver.network.aion.capture.NoOpServerPacketCaptureObserver",
			"game-server/src/com/aionemu/gameserver/network/aion/capture/NoOpServerPacketCaptureObserver.java",
			"isEnabled/onPacketSerialized",
			"server_packet_capture_observer_default_noop",
			requiresJavaChange: false,
			requiresProtectionArtifactSerializer: true,
			"Default observer remains disabled and emits no artifacts.");

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerJavaHookDetailReport(
			rowArray,
			HasDirectStopPacketCallers: rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ClientPacketDirectStopCaller),
			HasTeleportRunnableFutureHook: rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.TeleportAnimationDoneRunnableFuture),
			HasProtectionLifecycleHook: rowArray.Count(row => row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerProtectionLifecycle) == 2,
			HasTaskMapHooks: rowArray.Count(row => row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerTaskMap) == 3,
			HasTeleportFutureRegistrationHook: rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.TeleportFutureTaskRegistration),
			HasGenericPacketSerializationObserver: rowArray.Any(row => row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.PacketSerializationCaptureObserver),
			NeedsProtectionArtifactSerializer: rowArray.Any(row => row.RequiresProtectionArtifactSerializer),
			NeedsJavaObserverImplementation: rowArray.Any(row => row.RequiresJavaChange),
			ReadyForRuntimeComparison: false,
			"Java source hook detail map for protection stop-trigger runtime artifacts",
			IsLive: false);
	}

	private static void AddDirectStopPacketCallers(ICollection<PlayerProtectionActiveTaskStopTriggerJavaHookDetailRow> rows)
	{
		AddDirectStopPacketCaller(rows, "CM_ATTACK", "runImpl", "cm_attack_stop_protection", "Melee attack packet stops protection when the player is protection-active.");
		AddDirectStopPacketCaller(rows, "CM_CASTSPELL", "runImpl", "cm_castspell_stop_protection", "Cast spell packet stops protection before skill execution proceeds.");
		AddDirectStopPacketCaller(rows, "CM_COMPOSITE_STONES", "runImpl", "cm_composite_stones_stop_protection", "Composite stone packet stops protection before item-action handling proceeds.");
		AddDirectStopPacketCaller(rows, "CM_EMOTION", "runImpl", "cm_emotion_stop_protection", "Emotion/action packet stops protection in its action branch.");
		AddDirectStopPacketCaller(rows, "CM_DIALOG_SELECT", "runImpl", "cm_dialog_select_stop_protection", "Dialog selection packet stops protection before dialog handling.");
		AddDirectStopPacketCaller(rows, "CM_MOVE", "runImpl", "cm_move_stop_protection", "Ground movement packet stops protection after movement validation reaches the active-player branch.");
		AddDirectStopPacketCaller(rows, "CM_MOVE_IN_AIR", "runImpl", "cm_move_in_air_stop_protection", "Air movement packet stops protection for flying movement.");
		AddDirectStopPacketCaller(rows, "CM_SHOW_DIALOG", "runImpl", "cm_show_dialog_stop_protection", "Show-dialog packet stops protection before opening NPC dialog.");
		AddDirectStopPacketCaller(rows, "CM_USE_ITEM", "runImpl", "cm_use_item_stop_protection", "Use-item packet stops protection before item action execution.");
	}

	private static void AddDirectStopPacketCaller(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaHookDetailRow> rows,
		string packetClass,
		string javaMethod,
		string observerEvent,
		string notes)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ClientPacketDirectStopCaller,
			$"com.aionemu.gameserver.network.aion.clientpackets.{packetClass}",
			$"game-server/src/com/aionemu/gameserver/network/aion/clientpackets/{packetClass}.java",
			javaMethod,
			observerEvent,
			requiresJavaChange: true,
			requiresProtectionArtifactSerializer: true,
			notes);
	}

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerJavaHookDetailRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind kind,
		string javaArtifact,
		string javaSourcePath,
		string javaMethod,
		string observerEvent,
		bool requiresJavaChange,
		bool requiresProtectionArtifactSerializer,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerJavaHookDetailRow(
			rows.Count + 1,
			kind,
			javaArtifact,
			javaSourcePath,
			javaMethod,
			observerEvent,
			requiresJavaChange,
			requiresProtectionArtifactSerializer,
			notes));
	}
}
