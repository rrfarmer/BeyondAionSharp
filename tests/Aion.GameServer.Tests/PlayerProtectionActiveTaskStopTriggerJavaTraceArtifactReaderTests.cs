using System.Text.Json;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactReaderTests(ITestOutputHelper output)
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
	};

	[Fact]
	public void ParseProtectionStopTriggerArtifact_ReadsSchemaV1TraceFields()
	{
		var artifact = ParseInlineArtifact();

		Assert.Equal(1, artifact.SchemaVersion);
		Assert.Equal("cm-move-z-threshold-stop", artifact.Scenario);
		Assert.Equal("java", artifact.RuntimeFacts.ServerFlavor);
		Assert.Contains("CM_MOVE.runImpl", artifact.JavaSources);
		Assert.Contains("PlayerController.stopProtectionActiveTask", artifact.JavaSources);
		Assert.Contains("CreatureController.cancelTask", artifact.JavaSources);
		Assert.Equal(["packet_enter", "stop_condition_eval", "stop_call_enter", "task_cancel", "visual_mutate", "state_broadcast", "ai_notify_enqueue", "packet_return"], artifact.Traces.Select(trace => trace.Phase));
	}

	[Fact]
	public async Task FindProtectionStopTriggerJavaArtifacts_IsGuardedUntilGeneratorOutputExists()
	{
		var artifactRoot = GetArtifactRoot();
		var artifacts = Directory.Exists(artifactRoot)
			? Directory.GetFiles(artifactRoot, "*.json").Order(StringComparer.Ordinal).ToArray()
			: [];

		if (artifacts.Length == 0)
		{
			output.WriteLine("Needs Verification: Java protection stop-trigger trace artifacts are not present yet.");
			return;
		}

		foreach (var artifactPath in artifacts)
		{
			var json = await File.ReadAllTextAsync(artifactPath);
			var artifact = JsonSerializer.Deserialize<ProtectionStopTriggerJavaTraceArtifact>(json, JsonOptions);
			Assert.NotNull(artifact);
			Assert.Equal(1, artifact.SchemaVersion);
			Assert.NotEmpty(artifact.Scenario);
			Assert.NotEmpty(artifact.Traces);
			AssertArtifactSemantics(artifact);
		}
	}

	[Fact]
	public void ArtifactTraceRows_ContainRequiredPhaseSequence()
	{
		var artifact = ParseInlineArtifact();

		AssertArtifactSemantics(artifact);
		Assert.Equal(Enumerable.Range(0, artifact.Traces.Count), artifact.Traces.Select(trace => trace.EventSeq));
		Assert.Contains(artifact.Traces, trace => trace.Phase == "stop_call_enter" && trace.StopCalled);
		Assert.Contains(artifact.Traces, trace => trace.Phase == "packet_return" && trace.ReturnReason == "stop_completed");
	}

	[Fact]
	public void ArtifactTraceRows_RequireMovementPrecisionFields()
	{
		var artifact = ParseInlineArtifact();
		var stopCondition = artifact.Traces.Single(trace => trace.Phase == "stop_condition_eval");

		Assert.NotNull(stopCondition.Movement);
		Assert.Equal(100f, RequiredFloat(stopCondition.Movement!.OldX, "oldX"));
		Assert.Equal(100f, RequiredFloat(stopCondition.Movement.PacketX, "packetX"));
		Assert.Equal(50f, RequiredFloat(stopCondition.Movement.OldZ, "oldZ"));
		Assert.Equal(49.4f, RequiredFloat(stopCondition.Movement.PacketZ, "packetZ"));
		Assert.Equal(0.6f, RequiredFloat(stopCondition.Movement.ZDelta, "zDelta"));
		Assert.True(stopCondition.Movement.StopThresholdExceeded);
		Assert.Equal("cm_move_z_drop_threshold", stopCondition.ActionBranchName);
	}

	[Fact]
	public void ArtifactTraceRows_RequireTaskCancellationFields()
	{
		var artifact = ParseInlineArtifact();
		var taskCancel = artifact.Traces.Single(trace => trace.Phase == "task_cancel");

		Assert.NotNull(taskCancel.TaskCancellation);
		Assert.Equal("PROTECTION_ACTIVE", taskCancel.TaskCancellation!.TaskIdName);
		Assert.Equal(3, taskCancel.TaskCancellation.TaskIdOrdinal);
		Assert.True(taskCancel.TaskCancellation.TaskPresentBeforeCancel);
		Assert.True(taskCancel.TaskCancellation.TaskRemovedBeforeCancel);
		Assert.False(taskCancel.TaskCancellation.FutureCancelArgument);
		Assert.True(taskCancel.TaskCancellation.FutureCancelResult);
	}

	[Fact]
	public void ArtifactTraceRows_RequireFanoutAndAiNotifyFields()
	{
		var artifact = ParseInlineArtifact();
		var fanout = artifact.Traces.Single(trace => trace.Phase == "state_broadcast");
		var aiNotify = artifact.Traces.Single(trace => trace.Phase == "ai_notify_enqueue");

		Assert.NotNull(fanout.Fanout);
		Assert.Equal("SM_PLAYER_STATE", fanout.Fanout!.PacketName);
		Assert.True(fanout.Fanout.IncludeSelf);
		Assert.Equal(2, fanout.Fanout.RecipientCount);
		Assert.NotNull(aiNotify.AiNotify);
		Assert.True(aiNotify.AiNotify!.NotifyAiOnMoveCalled);
		Assert.Equal("after_state_broadcast", aiNotify.AiNotify.Ordering);
	}

	[Fact]
	public void ArtifactTraceRows_ClassifyReturnReasonsWithStopExpectations()
	{
		var artifact = ParseInlineArtifact();

		Assert.Equal("cm_move_z_drop_threshold", artifact.RuntimeFacts.ExpectedReturnReason);
		Assert.Contains(artifact.Traces, trace =>
			trace.ReturnReason == "cm_move_z_drop_threshold"
			&& trace.ExpectsStopProtectionCall);
		Assert.Contains(artifact.Traces, trace =>
			trace.ReturnReason == "stop_completed"
			&& trace.StopCalled);
		Assert.DoesNotContain(artifact.Traces, trace =>
			trace.ReturnReason is "anti_hack_reject" or "emotion_stance_reject");
	}

	[Fact]
	public void ArtifactTraceRows_DoNotUseTimestampsAsParityKeysAndSerializeEnumsAsStableNames()
	{
		var artifact = ParseInlineArtifact();

		Assert.All(artifact.Traces, trace =>
		{
			Assert.False(trace.TimestampIsParityKey);
			Assert.False(string.IsNullOrWhiteSpace(trace.Phase));
			Assert.DoesNotContain(trace.Phase, char.IsDigit);
			Assert.False(string.IsNullOrWhiteSpace(trace.ReturnReason));
			Assert.DoesNotContain(trace.ReturnReason, char.IsDigit);
			Assert.True(trace.WallTimeEpochMillis > 0);
			Assert.True(trace.MonotonicNanos > 0);
		});
	}

	private static void AssertArtifactSemantics(ProtectionStopTriggerJavaTraceArtifact artifact)
	{
		Assert.Equal(1, artifact.SchemaVersion);
		Assert.False(string.IsNullOrWhiteSpace(artifact.Scenario));
		Assert.False(string.IsNullOrWhiteSpace(artifact.JavaCommit));
		Assert.NotEmpty(artifact.JavaSources);
		Assert.NotEmpty(artifact.Traces);
		Assert.Contains(artifact.Traces, trace => trace.Phase == "packet_enter");
		Assert.Contains(artifact.Traces, trace => trace.Phase == "task_cancel");
		Assert.Contains(artifact.Traces, trace => trace.Phase == "state_broadcast");
		Assert.Contains(artifact.Traces, trace => trace.Phase == "ai_notify_enqueue");
		Assert.All(artifact.Traces, trace =>
		{
			Assert.True(trace.SchemaVersion == artifact.SchemaVersion, "Trace row schema version must match artifact schema.");
			Assert.True(trace.EventSeq >= 0, "Trace row event sequence must be non-negative.");
			Assert.False(string.IsNullOrWhiteSpace(trace.TraceId));
			Assert.False(string.IsNullOrWhiteSpace(trace.Phase));
			Assert.False(string.IsNullOrWhiteSpace(trace.ReturnReason));
			Assert.False(string.IsNullOrWhiteSpace(trace.JavaSourceFile));
			Assert.True(trace.JavaLine > 0, "Trace row needs a Java line breadcrumb.");
		});
	}

	private static ProtectionStopTriggerJavaTraceArtifact ParseInlineArtifact()
	{
		// Java parity breadcrumb: future schema-v1 artifact for CM_MOVE.runImpl ->
		// PlayerController.stopProtectionActiveTask -> CreatureController.cancelTask(TaskId.PROTECTION_ACTIVE).
		const string json = """
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "cm-move-z-threshold-stop",
			  "runtimeFacts": {
			    "serverFlavor": "java",
			    "packetName": "CM_MOVE",
			    "playerObjectId": 1001,
			    "worldId": 210010000,
			    "expectedReturnReason": "cm_move_z_drop_threshold"
			  },
			  "javaSources": [
			    "CM_MOVE.runImpl",
			    "PlayerController.stopProtectionActiveTask",
			    "CreatureController.cancelTask"
			  ],
			  "traces": [
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 0,
			      "phase": "packet_enter",
			      "packetName": "CM_MOVE",
			      "returnReason": "cm_move_z_drop_threshold",
			      "stopCalled": false,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000000,
			      "monotonicNanos": 1000000,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "CM_MOVE.java",
			      "javaLine": 76,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": true,
			        "protectionActiveAfter": true,
			        "visualStateBefore": ["BLINKING"],
			        "visualStateAfter": ["BLINKING"]
			      },
			      "movement": null,
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "actionBranchName": "packet_enter"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 1,
			      "phase": "stop_condition_eval",
			      "packetName": "CM_MOVE",
			      "returnReason": "cm_move_z_drop_threshold",
			      "stopCalled": false,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000001,
			      "monotonicNanos": 1000100,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "CM_MOVE.java",
			      "javaLine": 139,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": true,
			        "protectionActiveAfter": true,
			        "visualStateBefore": ["BLINKING"],
			        "visualStateAfter": ["BLINKING"]
			      },
			      "movement": {
			        "oldX": 100.0,
			        "oldY": 200.0,
			        "oldZ": 50.0,
			        "packetX": 100.0,
			        "packetY": 200.0,
			        "packetZ": 49.4,
			        "zDelta": 0.6,
			        "heading": 90,
			        "movementType": "walk",
			        "antiHackAccepted": true,
			        "teleportationModeAbsoluteMove": false,
			        "stopThresholdExceeded": true
			      },
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "actionBranchName": "cm_move_z_drop_threshold"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 2,
			      "phase": "stop_call_enter",
			      "packetName": "CM_MOVE",
			      "returnReason": "cm_move_z_drop_threshold",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000002,
			      "monotonicNanos": 1000200,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 641,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": true,
			        "protectionActiveAfter": true,
			        "visualStateBefore": ["BLINKING"],
			        "visualStateAfter": ["BLINKING"]
			      },
			      "movement": null,
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "actionBranchName": "packet_origin"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 3,
			      "phase": "task_cancel",
			      "packetName": "CM_MOVE",
			      "returnReason": "cm_move_z_drop_threshold",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000003,
			      "monotonicNanos": 1000300,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "CreatureController.java",
			      "javaLine": 364,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": true,
			        "protectionActiveAfter": true,
			        "visualStateBefore": ["BLINKING"],
			        "visualStateAfter": ["BLINKING"]
			      },
			      "movement": null,
			      "taskCancellation": {
			        "taskIdName": "PROTECTION_ACTIVE",
			        "taskIdOrdinal": 3,
			        "taskPresentBeforeCancel": true,
			        "taskRemovedBeforeCancel": true,
			        "futureCancelArgument": false,
			        "futureCancelResult": true,
			        "scheduledDelayMillis": 60000,
			        "stopOrigin": "first_action_packet"
			      },
			      "fanout": null,
			      "aiNotify": null,
			      "actionBranchName": "cancel_task"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 4,
			      "phase": "visual_mutate",
			      "packetName": "CM_MOVE",
			      "returnReason": "cm_move_z_drop_threshold",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000004,
			      "monotonicNanos": 1000400,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 644,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": true,
			        "protectionActiveAfter": false,
			        "visualStateBefore": ["BLINKING"],
			        "visualStateAfter": []
			      },
			      "movement": null,
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "actionBranchName": "unset_blinking"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 5,
			      "phase": "state_broadcast",
			      "packetName": "CM_MOVE",
			      "returnReason": "cm_move_z_drop_threshold",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000005,
			      "monotonicNanos": 1000500,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 645,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": false,
			        "protectionActiveAfter": false,
			        "visualStateBefore": [],
			        "visualStateAfter": []
			      },
			      "movement": null,
			      "taskCancellation": null,
			      "fanout": {
			        "packetName": "SM_PLAYER_STATE",
			        "includeSelf": true,
			        "recipientCount": 2,
			        "knownListOrderIsParityKey": false
			      },
			      "aiNotify": null,
			      "actionBranchName": "broadcast_state"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 6,
			      "phase": "ai_notify_enqueue",
			      "packetName": "CM_MOVE",
			      "returnReason": "cm_move_z_drop_threshold",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000006,
			      "monotonicNanos": 1000600,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 646,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": false,
			        "protectionActiveAfter": false,
			        "visualStateBefore": [],
			        "visualStateAfter": []
			      },
			      "movement": null,
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": {
			        "notifyAiOnMoveCalled": true,
			        "ordering": "after_state_broadcast"
			      },
			      "actionBranchName": "ai_notify"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "pst-001",
			      "eventSeq": 7,
			      "phase": "packet_return",
			      "packetName": "CM_MOVE",
			      "returnReason": "stop_completed",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000007,
			      "monotonicNanos": 1000700,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "CM_MOVE.java",
			      "javaLine": 146,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": false,
			        "protectionActiveAfter": false,
			        "visualStateBefore": [],
			        "visualStateAfter": []
			      },
			      "movement": null,
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "actionBranchName": "packet_return"
			    }
			  ],
			  "notes": [
			    "Timestamps are diagnostics only.",
			    "Known-list recipient ordering is not a parity key.",
			    "No Java runtime parity is claimed until generated artifacts exist."
			  ]
			}
			""";

		var artifact = JsonSerializer.Deserialize<ProtectionStopTriggerJavaTraceArtifact>(json, JsonOptions);
		Assert.NotNull(artifact);
		AssertArtifactSemantics(artifact);
		return artifact;
	}

	private static float RequiredFloat(float? value, string fieldName)
	{
		Assert.True(value.HasValue, $"Missing trace movement {fieldName}.");
		return value.Value;
	}

	private static string GetArtifactRoot() =>
		Path.Combine(FindRepositoryRoot(), "parity-artifacts", "protection-stop-trigger", "java");

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, ".git")) && Directory.Exists(Path.Combine(directory.FullName, "docs")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate repository root.");
	}

	private sealed record ProtectionStopTriggerJavaTraceArtifact(
		int SchemaVersion,
		string JavaCommit,
		string Scenario,
		ProtectionStopTriggerRuntimeFacts RuntimeFacts,
		IReadOnlyList<string> JavaSources,
		IReadOnlyList<ProtectionStopTriggerTraceRow> Traces,
		IReadOnlyList<string> Notes);

	private sealed record ProtectionStopTriggerRuntimeFacts(
		string ServerFlavor,
		string PacketName,
		int PlayerObjectId,
		int WorldId,
		string ExpectedReturnReason);

	private sealed record ProtectionStopTriggerTraceRow(
		int SchemaVersion,
		string TraceId,
		int EventSeq,
		string Phase,
		string PacketName,
		string ReturnReason,
		bool StopCalled,
		bool ExpectsStopProtectionCall,
		long WallTimeEpochMillis,
		long MonotonicNanos,
		bool TimestampIsParityKey,
		string JavaSourceFile,
		int JavaLine,
		ProtectionStopTriggerPlayerSnapshot Player,
		ProtectionStopTriggerMovementSnapshot? Movement,
		ProtectionStopTriggerTaskCancellationSnapshot? TaskCancellation,
		ProtectionStopTriggerFanoutSnapshot? Fanout,
		ProtectionStopTriggerAiNotifySnapshot? AiNotify,
		string ActionBranchName);

	private sealed record ProtectionStopTriggerPlayerSnapshot(
		int ObjectId,
		bool Spawned,
		bool Flying,
		bool Dead,
		bool ProtectionActiveBefore,
		bool ProtectionActiveAfter,
		IReadOnlyList<string> VisualStateBefore,
		IReadOnlyList<string> VisualStateAfter);

	private sealed record ProtectionStopTriggerMovementSnapshot(
		float? OldX,
		float? OldY,
		float? OldZ,
		float? PacketX,
		float? PacketY,
		float? PacketZ,
		float? ZDelta,
		int Heading,
		string MovementType,
		bool AntiHackAccepted,
		bool TeleportationModeAbsoluteMove,
		bool StopThresholdExceeded);

	private sealed record ProtectionStopTriggerTaskCancellationSnapshot(
		string TaskIdName,
		int TaskIdOrdinal,
		bool TaskPresentBeforeCancel,
		bool TaskRemovedBeforeCancel,
		bool FutureCancelArgument,
		bool FutureCancelResult,
		int ScheduledDelayMillis,
		string StopOrigin);

	private sealed record ProtectionStopTriggerFanoutSnapshot(
		string PacketName,
		bool IncludeSelf,
		int RecipientCount,
		bool KnownListOrderIsParityKey);

	private sealed record ProtectionStopTriggerAiNotifySnapshot(
		bool NotifyAiOnMoveCalled,
		string Ordering);
}
