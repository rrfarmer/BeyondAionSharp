using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportServiceTests
{
	[Fact]
	public void Create_ListsDirectStopPacketCallersFromJavaSourceReview()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportService.Create();
		var callers = report.Rows
			.Where(row => row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ClientPacketDirectStopCaller)
			.ToArray();

		Assert.True(report.HasDirectStopPacketCallers);
		Assert.Equal(9, callers.Length);
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_ATTACK", StringComparison.Ordinal) && row.ObserverEvent == "cm_attack_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_CASTSPELL", StringComparison.Ordinal) && row.ObserverEvent == "cm_castspell_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_COMPOSITE_STONES", StringComparison.Ordinal) && row.ObserverEvent == "cm_composite_stones_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_EMOTION", StringComparison.Ordinal) && row.ObserverEvent == "cm_emotion_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_DIALOG_SELECT", StringComparison.Ordinal) && row.ObserverEvent == "cm_dialog_select_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_MOVE", StringComparison.Ordinal) && row.ObserverEvent == "cm_move_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_MOVE_IN_AIR", StringComparison.Ordinal) && row.ObserverEvent == "cm_move_in_air_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_SHOW_DIALOG", StringComparison.Ordinal) && row.ObserverEvent == "cm_show_dialog_stop_protection");
		Assert.Contains(callers, row => row.JavaArtifact.EndsWith(".CM_USE_ITEM", StringComparison.Ordinal) && row.ObserverEvent == "cm_use_item_stop_protection");
		Assert.All(callers, row => Assert.Equal("runImpl", row.JavaMethod));
	}

	[Fact]
	public void Create_RecordsControllerTaskMapAndTeleportRunnableFutureHooks()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportService.Create();

		Assert.True(report.HasTeleportRunnableFutureHook);
		Assert.True(report.HasProtectionLifecycleHook);
		Assert.True(report.HasTaskMapHooks);
		Assert.True(report.HasTeleportFutureRegistrationHook);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.TeleportAnimationDoneRunnableFuture
			&& row.JavaArtifact == "com.aionemu.gameserver.network.aion.clientpackets.CM_TELEPORT_ANIMATION_DONE"
			&& row.JavaMethod == "runImpl"
			&& row.Notes.Contains("RunnableFuture", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerProtectionLifecycle
			&& row.JavaMethod == "startProtectionActiveTask"
			&& row.Notes.Contains("60000 ms", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.ControllerTaskMap
			&& row.JavaMethod == "addTask"
			&& row.Notes.Contains("ConcurrentHashMap.compute", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.TeleportFutureTaskRegistration
			&& row.JavaMethod == "sendLoc"
			&& row.Notes.Contains("FutureTask<Void>", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DocumentsGenericPacketObserverButMissingProtectionArtifactSerializer()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportService.Create();

		Assert.True(report.HasGenericPacketSerializationObserver);
		Assert.True(report.NeedsProtectionArtifactSerializer);
		Assert.True(report.NeedsJavaObserverImplementation);
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.False(report.IsLive);
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.PacketSerializationCaptureObserver
			&& row.JavaArtifact == "com.aionemu.gameserver.network.aion.AionServerPacket"
			&& row.RequiresJavaChange == false
			&& row.RequiresProtectionArtifactSerializer
			&& row.Notes.Contains("protection stop-trigger schema-v1 artifact serialization is still missing", StringComparison.Ordinal));
		Assert.Contains(report.Rows, row =>
			row.Kind == PlayerProtectionActiveTaskStopTriggerJavaHookDetailKind.PacketCaptureObserverDependency
			&& row.JavaArtifact == "com.aionemu.gameserver.network.aion.capture.NoOpServerPacketCaptureObserver"
			&& row.Notes.Contains("emits no artifacts", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_UsesStableOrderAndSourcePaths()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaHookDetailReportService.Create();

		Assert.Equal(19, report.Rows.Count);
		Assert.Equal(Enumerable.Range(1, report.Rows.Count), report.Rows.Select(row => row.Order));
		Assert.All(report.Rows, row => Assert.StartsWith("game-server/src/com/aionemu/gameserver/", row.JavaSourcePath, StringComparison.Ordinal));
		Assert.All(report.Rows, row => Assert.False(string.IsNullOrWhiteSpace(row.ObserverEvent)));
		Assert.Equal("Java source hook detail map for protection stop-trigger runtime artifacts", report.JavaSource);
	}
}
