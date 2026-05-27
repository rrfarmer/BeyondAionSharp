using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskReportServiceTests
{
	[Fact]
	public void CreateReport_StartPreservesJavaOrder()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		var report = PlayerProtectionActiveTaskReportService.CreateReport(result);

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStarted, report.Status);
		Assert.True(report.IsLive);
		Assert.Equal(
			[
				"if (!getOwner().isProtectionActive())",
				"setVisualState(CreatureVisualState.BLINKING)",
				"cancelCastOn(getOwner())",
				"removeTargetFrom(getOwner())",
				"new SM_PLAYER_STATE(player)",
				"broadcastToSightedPlayers(player, packet, true)",
				"schedule(this::stopProtectionActiveTask, 60000)",
				"addTask(TaskId.PROTECTION_ACTIVE, future)",
			],
			report.Rows.Select(row => row.JavaOperation).ToArray());
		Assert.Contains(report.Rows, row => row.JavaArtifact == "Player" && row.IsLive);
		Assert.Contains(report.Rows, row => row.JavaArtifact == "PacketSendUtility" && row.Kind == PlayerProtectionActiveTaskReportRowKind.PacketIntent && !row.IsLive);
	}

	[Fact]
	public void CreateReport_AlreadyProtectedStartReportsSkippedBranch()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Start,
			ExecuteLiveVisualMutation: true));

		var report = PlayerProtectionActiveTaskReportService.CreateReport(result);

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.AlreadyProtected, report.Status);
		Assert.Equal(
			[
				"if (!getOwner().isProtectionActive())",
				"return because player is already protection active",
			],
			report.Rows.Select(row => row.JavaOperation).ToArray());
		Assert.Contains(report.Rows, row => row.Kind == PlayerProtectionActiveTaskReportRowKind.SkippedBranch);
	}

	[Fact]
	public void CreateReport_StopSpawnedPreservesJavaOrder()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: true));

		var report = PlayerProtectionActiveTaskReportService.CreateReport(result);

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopped, report.Status);
		Assert.Equal(
			[
				"cancelTask(TaskId.PROTECTION_ACTIVE)",
				"unsetVisualState(CreatureVisualState.BLINKING)",
				"new SM_PLAYER_STATE(player)",
				"broadcastToSightedPlayers(player, packet, true)",
				"notifyAIOnMove()",
			],
			report.Rows.Select(row => row.JavaOperation).ToArray());
		Assert.Contains(report.Rows, row => row.JavaArtifact == "Player" && row.IsLive);
		Assert.Contains(report.Rows, row => row.JavaArtifact == "PlayerController" && row.Kind == PlayerProtectionActiveTaskReportRowKind.UnsupportedSideEffect);
	}

	[Fact]
	public void CreateReport_StopUnspawnedReportsSkippedSpawnedBranch()
	{
		var player = new Player { ObjectId = PlayerObjectId };
		player.SetVisualState(PlayerVisualStates.Blinking);
		var result = PlayerProtectionActiveTaskAdapterService.Apply(new PlayerProtectionActiveTaskAdapterRequest(
			player,
			PlayerProtectionActiveTaskAdapterAction.Stop,
			ExecuteLiveVisualMutation: true,
			HasProtectionActiveTask: true,
			IsSpawned: false));

		var report = PlayerProtectionActiveTaskReportService.CreateReport(result);

		Assert.Equal(PlayerProtectionActiveTaskAdapterStatus.LiveVisualStopUnspawned, report.Status);
		Assert.Equal(
			[
				"cancelTask(TaskId.PROTECTION_ACTIVE)",
				"skip spawned-only visual state, SM_PLAYER_STATE fanout, and notifyAIOnMove",
			],
			report.Rows.Select(row => row.JavaOperation).ToArray());
		Assert.Contains(report.Rows, row => row.Kind == PlayerProtectionActiveTaskReportRowKind.SkippedBranch);
		Assert.DoesNotContain(report.Rows, row => row.JavaOperation == "broadcastToSightedPlayers(player, packet, true)");
	}

	private const int PlayerObjectId = 1001;
}
