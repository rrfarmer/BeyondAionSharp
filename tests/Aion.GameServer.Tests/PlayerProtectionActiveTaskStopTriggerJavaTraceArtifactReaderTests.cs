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

	[Fact]
	public void NoStopCmMoveArtifacts_ClassifyGuardReturnsWithoutControllerObservables()
	{
		var artifacts = new[]
		{
			ParseNoStopArtifact("cm-move-anti-hack-reject", "CM_MOVE", "anti_hack_reject", "CM_MOVE.java", 132, includeMovement: true),
			ParseNoStopArtifact("cm-move-not-spawned", "CM_MOVE", "not_spawned", "CM_MOVE.java", 137, includeMovement: true),
			ParseNoStopArtifact("cm-move-teleportation-absolute-move", "CM_MOVE", "teleportation_absolute_move_return", "CM_MOVE.java", 102, includeMovement: true),
			ParseNoStopArtifact("cm-move-same-position-turn", "CM_MOVE", "cm_move_same_position_turn", "CM_MOVE.java", 139, includeMovement: true),
		};

		foreach (var artifact in artifacts)
		{
			AssertNoStopArtifact(artifact);
			Assert.All(artifact.Traces.Where(trace => trace.Movement != null), trace =>
			{
				Assert.False(trace.Movement!.StopThresholdExceeded);
				Assert.False(trace.Movement.TeleportationModeAbsoluteMove && trace.ReturnReason != "teleportation_absolute_move_return");
			});
		}
	}

	[Fact]
	public void NoStopCmEmotionArtifacts_ClassifyEarlyReturnsWithoutControllerObservables()
	{
		var artifacts = new[]
		{
			ParseNoStopArtifact("cm-emotion-stance-rejection-no-stop", "CM_EMOTION", "emotion_stance_reject", "CM_EMOTION.java", 131, includeMovement: false),
			ParseNoStopArtifact("cm-emotion-abnormal-guard-no-stop", "CM_EMOTION", "emotion_abnormal_guard", "CM_EMOTION.java", 110, includeMovement: false),
			ParseNoStopArtifact("cm-emotion-validation-return-no-stop", "CM_EMOTION", "emotion_validation_return", "CM_EMOTION.java", 214, includeMovement: false),
		};

		foreach (var artifact in artifacts)
		{
			AssertNoStopArtifact(artifact);
			Assert.All(artifact.Traces, trace => Assert.Null(trace.Movement));
			Assert.All(artifact.Traces, trace =>
			{
				Assert.NotNull(trace.Emotion);
				Assert.False(trace.Emotion!.EmotionCanUse);
				Assert.False(trace.Emotion.EmotionBroadcasted);
			});
		}
	}

	[Fact]
	public void StopAfterInvalidUseItemArtifacts_KeepControllerStopBeforeInvalidBranches()
	{
		var artifacts = new[]
		{
			ParseStopAfterInvalidArtifact("cm-use-item-not-found-after-stop", "CM_USE_ITEM", "item_not_found", "CM_USE_ITEM.java", 62, "item_lookup_missing"),
			ParseStopAfterInvalidArtifact("cm-use-item-restricted-after-stop", "CM_USE_ITEM", "item_use_restricted", "CM_USE_ITEM.java", 79, "player_restrictions_can_use_item"),
			ParseStopAfterInvalidArtifact("cm-use-item-not-usable-after-stop", "CM_USE_ITEM", "item_not_usable", "CM_USE_ITEM.java", 88, "item_actions_empty_and_quest_not_handled"),
			ParseStopAfterInvalidArtifact("cm-use-item-action-rejected-after-stop", "CM_USE_ITEM", "item_action_rejected", "CM_USE_ITEM.java", 109, "item_actions_rejected_by_can_act"),
		};

		foreach (var artifact in artifacts)
		{
			AssertStopAfterInvalidArtifact(artifact);
			var invalidBranch = artifact.Traces.Single(trace => trace.Phase == "post_stop_packet_side_effect");
			Assert.NotNull(invalidBranch.ActionPayload);
			Assert.Equal(987654, invalidBranch.ActionPayload!.ItemObjectId);
			switch (invalidBranch.ReturnReason)
			{
				case "item_not_found":
					Assert.Equal("not_found", invalidBranch.ActionPayload.ItemLookupResult);
					break;
				case "item_use_restricted":
					Assert.Equal("restricted", invalidBranch.ActionPayload.RestrictionResult);
					break;
				case "item_not_usable":
					Assert.Equal("no_template_actions_and_quest_not_handled", invalidBranch.ActionPayload.ItemActionResult);
					break;
				case "item_action_rejected":
					Assert.Equal("all_can_act_false", invalidBranch.ActionPayload.ItemActionResult);
					break;
				default:
					throw new Xunit.Sdk.XunitException($"Unexpected use-item return reason {invalidBranch.ReturnReason}.");
			}
			Assert.Null(invalidBranch.ActionPayload.CompositeToolObjectId);
		}
	}

	[Fact]
	public void StopAfterInvalidCompositeArtifacts_KeepControllerStopBeforeInvalidBranches()
	{
		var artifacts = new[]
		{
			ParseStopAfterInvalidArtifact("cm-composite-tool-missing-after-stop", "CM_COMPOSITE_STONES", "composition_tool_not_found", "CM_COMPOSITE_STONES.java", 58, "tool_item_lookup_missing"),
			ParseStopAfterInvalidArtifact("cm-composite-first-missing-after-stop", "CM_COMPOSITE_STONES", "composition_first_item_not_found", "CM_COMPOSITE_STONES.java", 61, "first_item_lookup_missing"),
			ParseStopAfterInvalidArtifact("cm-composite-second-missing-after-stop", "CM_COMPOSITE_STONES", "composition_second_item_not_found", "CM_COMPOSITE_STONES.java", 64, "second_item_lookup_missing"),
			ParseStopAfterInvalidArtifact("cm-composite-tool-restricted-after-stop", "CM_COMPOSITE_STONES", "composition_tool_use_restricted", "CM_COMPOSITE_STONES.java", 67, "composition_tool_use_restricted"),
			ParseStopAfterInvalidArtifact("cm-composite-can-act-after-stop", "CM_COMPOSITE_STONES", "composition_action_rejected", "CM_COMPOSITE_STONES.java", 72, "composition_action_can_act_failed"),
		};

		foreach (var artifact in artifacts)
		{
			AssertStopAfterInvalidArtifact(artifact);
			var invalidBranch = artifact.Traces.Single(trace => trace.Phase == "post_stop_packet_side_effect");
			Assert.NotNull(invalidBranch.ActionPayload);
			Assert.Equal(222001, invalidBranch.ActionPayload!.CompositeToolObjectId);
			Assert.Equal(222002, invalidBranch.ActionPayload.CompositeFirstObjectId);
			Assert.Equal(222003, invalidBranch.ActionPayload.CompositeSecondObjectId);
			Assert.Null(invalidBranch.ActionPayload.ItemObjectId);
			Assert.Contains("composition", invalidBranch.ReturnReason);
		}
	}

	[Fact]
	public void ScheduledCallbackArtifacts_RecordDelayAndStopCallbackOrdering()
	{
		var artifact = ParseScheduledCallbackArtifact("protection-active-scheduled-callback-stop", replacementCancelsOldFuture: false);

		AssertArtifactSemantics(artifact);
		Assert.Contains(artifact.JavaSources, source => source == "PlayerController.startProtectionActiveTask");
		var schedule = artifact.Traces.Single(trace => trace.Phase == "schedule_enter");
		var taskAdd = artifact.Traces.Single(trace => trace.Phase == "task_add");
		var callback = artifact.Traces.Single(trace => trace.Phase == "callback_enter");
		var stopCall = artifact.Traces.Single(trace => trace.Phase == "stop_call_enter");
		var taskCancel = artifact.Traces.Single(trace => trace.Phase == "task_cancel");

		Assert.NotNull(schedule.Scheduler);
		Assert.Equal(60000, schedule.Scheduler!.DelayMillis);
		Assert.Equal("MILLISECONDS", schedule.Scheduler.TimeUnit);
		Assert.True(schedule.Scheduler.RunnableWrapperApplied);
		Assert.Equal("PlayerController.stopProtectionActiveTask", schedule.Scheduler.CallbackMethod);
		Assert.NotNull(taskAdd.Scheduler);
		Assert.False(taskAdd.Scheduler!.OldFuturePresent);
		Assert.True(taskAdd.Scheduler.NewFutureStored);
		Assert.True(schedule.EventSeq < taskAdd.EventSeq);
		Assert.True(taskAdd.EventSeq < callback.EventSeq);
		Assert.True(callback.EventSeq < stopCall.EventSeq);
		Assert.True(stopCall.EventSeq < taskCancel.EventSeq);
		Assert.Equal("scheduled_callback", taskCancel.TaskCancellation!.StopOrigin);
	}

	[Fact]
	public void ReplacementRaceArtifacts_RecordOldFutureCancellationBeforeNewTaskStorage()
	{
		var artifact = ParseScheduledCallbackArtifact("protection-active-replacement-race", replacementCancelsOldFuture: true);

		AssertArtifactSemantics(artifact);
		var taskAdd = artifact.Traces.Single(trace => trace.Phase == "task_add");
		var taskCancel = artifact.Traces.Single(trace => trace.Phase == "task_cancel");

		Assert.NotNull(taskAdd.Scheduler);
		Assert.True(taskAdd.Scheduler!.OldFuturePresent);
		Assert.True(taskAdd.Scheduler.OldFutureCancelArgument.HasValue);
		Assert.False(taskAdd.Scheduler.OldFutureCancelArgument!.Value);
		Assert.True(taskAdd.Scheduler.OldFutureCancelResult.HasValue);
		Assert.False(taskAdd.Scheduler.OldFutureCancelResult!.Value);
		Assert.True(taskAdd.Scheduler.NewFutureStored);
		Assert.NotNull(taskCancel.TaskCancellation);
		Assert.True(taskCancel.TaskCancellation!.TaskPresentBeforeCancel);
		Assert.True(taskCancel.TaskCancellation.TaskRemovedBeforeCancel);
		Assert.False(taskCancel.TaskCancellation.FutureCancelArgument);
		Assert.True(taskCancel.TaskCancellation.FutureCancelResult);
		Assert.Equal("replacement_race_current_future", taskCancel.TaskCancellation.StopOrigin);
		Assert.True(taskAdd.EventSeq < taskCancel.EventSeq);
	}

	private static void AssertNoStopArtifact(ProtectionStopTriggerJavaTraceArtifact artifact)
	{
		Assert.Equal(1, artifact.SchemaVersion);
		Assert.NotEmpty(artifact.Scenario);
		Assert.NotEmpty(artifact.Traces);
		Assert.Contains(artifact.Traces, trace => trace.Phase == "packet_enter");
		Assert.Contains(artifact.Traces, trace => trace.Phase == "guard_return");
		Assert.DoesNotContain(artifact.Traces, trace => trace.Phase is "stop_call_enter" or "task_cancel" or "state_broadcast" or "ai_notify_enqueue");
		Assert.All(artifact.Traces, trace =>
		{
			Assert.False(trace.StopCalled);
			Assert.False(trace.ExpectsStopProtectionCall);
			Assert.Null(trace.TaskCancellation);
			Assert.Null(trace.Fanout);
			Assert.Null(trace.AiNotify);
			Assert.False(trace.TimestampIsParityKey);
			Assert.True(trace.JavaLine > 0);
		});
	}

	private static void AssertStopAfterInvalidArtifact(ProtectionStopTriggerJavaTraceArtifact artifact)
	{
		AssertArtifactSemantics(artifact);
		Assert.Contains(artifact.Traces, trace => trace.Phase == "visual_mutate");
		Assert.Contains(artifact.Traces, trace => trace.Phase == "post_stop_packet_side_effect");

		var stopCall = artifact.Traces.Single(trace => trace.Phase == "stop_call_enter");
		var taskCancel = artifact.Traces.Single(trace => trace.Phase == "task_cancel");
		var invalidBranch = artifact.Traces.Single(trace => trace.Phase == "post_stop_packet_side_effect");
		var packetReturn = artifact.Traces.Single(trace => trace.Phase == "packet_return");

		Assert.True(stopCall.EventSeq < taskCancel.EventSeq);
		Assert.True(taskCancel.EventSeq < invalidBranch.EventSeq);
		Assert.True(invalidBranch.EventSeq < packetReturn.EventSeq);
		Assert.True(stopCall.StopCalled);
		Assert.True(taskCancel.StopCalled);
		Assert.True(invalidBranch.StopCalled);
		Assert.True(invalidBranch.ExpectsStopProtectionCall);
		Assert.Equal(artifact.RuntimeFacts.ExpectedReturnReason, invalidBranch.ReturnReason);
		Assert.Equal(invalidBranch.ReturnReason, packetReturn.ReturnReason);
		Assert.Null(invalidBranch.TaskCancellation);
		Assert.Null(invalidBranch.Fanout);
		Assert.Null(invalidBranch.AiNotify);
		Assert.NotNull(invalidBranch.ActionPayload);
		Assert.All(artifact.Traces, trace => Assert.False(trace.TimestampIsParityKey));
	}

	private static void AssertArtifactSemantics(ProtectionStopTriggerJavaTraceArtifact artifact)
	{
		Assert.Equal(1, artifact.SchemaVersion);
		Assert.False(string.IsNullOrWhiteSpace(artifact.Scenario));
		Assert.False(string.IsNullOrWhiteSpace(artifact.JavaCommit));
		Assert.NotEmpty(artifact.JavaSources);
		Assert.NotEmpty(artifact.Traces);
		Assert.Contains(artifact.Traces, trace => trace.Phase is "packet_enter" or "schedule_enter");
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

	private static ProtectionStopTriggerJavaTraceArtifact ParseStopAfterInvalidArtifact(
		string scenario,
		string packetName,
		string returnReason,
		string javaSourceFile,
		int invalidBranchLine,
		string actionBranchName)
	{
		var itemLookupResult = returnReason == "item_not_found" ? "not_found" : "found";
		var restrictionResult = returnReason == "item_use_restricted" ? "restricted" : "passed";
		var itemActionResult = returnReason switch
		{
			"item_not_usable" => "no_template_actions_and_quest_not_handled",
			"item_action_rejected" => "all_can_act_false",
			_ => "not_evaluated",
		};
		var compositeCanActResult = returnReason == "composition_action_rejected" ? "false" : "not_evaluated";
		var actionPayloadJson = packetName == "CM_USE_ITEM"
			? $$"""
				  {
				    "itemObjectId": 987654,
				    "itemLookupResult": "{{itemLookupResult}}",
				    "restrictionResult": "{{restrictionResult}}",
				    "itemActionResult": "{{itemActionResult}}",
				    "compositeToolObjectId": null,
				    "compositeFirstObjectId": null,
				    "compositeSecondObjectId": null,
				    "compositeCanActResult": null
				  }
				"""
			: $$"""
				  {
				    "itemObjectId": null,
				    "itemLookupResult": null,
				    "restrictionResult": null,
				    "itemActionResult": null,
				    "compositeToolObjectId": 222001,
				    "compositeFirstObjectId": 222002,
				    "compositeSecondObjectId": 222003,
				    "compositeCanActResult": "{{compositeCanActResult}}"
				  }
				""";
		var json = $$"""
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "{{scenario}}",
			  "runtimeFacts": {
			    "serverFlavor": "java",
			    "packetName": "{{packetName}}",
			    "playerObjectId": 1001,
			    "worldId": 210010000,
			    "expectedReturnReason": "{{returnReason}}"
			  },
			  "javaSources": [
			    "{{packetName}}.runImpl",
			    "PlayerController.stopProtectionActiveTask",
			    "CreatureController.cancelTask"
			  ],
			  "traces": [
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 0,
			      "phase": "packet_enter",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": false,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000000,
			      "monotonicNanos": 3000000,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "{{javaSourceFile}}",
			      "javaLine": {{(packetName == "CM_USE_ITEM" ? 55 : 44)}},
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
			      "emotion": null,
			      "actionPayload": {{actionPayloadJson}},
			      "actionBranchName": "packet_enter"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 1,
			      "phase": "stop_call_enter",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000001,
			      "monotonicNanos": 3000100,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "{{javaSourceFile}}",
			      "javaLine": {{(packetName == "CM_USE_ITEM" ? 58 : 49)}},
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
			      "emotion": null,
			      "actionPayload": null,
			      "actionBranchName": "active_protection_stop"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 2,
			      "phase": "task_cancel",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000002,
			      "monotonicNanos": 3000200,
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
			      "emotion": null,
			      "actionPayload": null,
			      "actionBranchName": "cancel_task"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 3,
			      "phase": "visual_mutate",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000003,
			      "monotonicNanos": 3000300,
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
			      "emotion": null,
			      "actionPayload": null,
			      "actionBranchName": "unset_blinking"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 4,
			      "phase": "state_broadcast",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000004,
			      "monotonicNanos": 3000400,
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
			      "emotion": null,
			      "actionPayload": null,
			      "actionBranchName": "broadcast_state"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 5,
			      "phase": "ai_notify_enqueue",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000005,
			      "monotonicNanos": 3000500,
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
			      "emotion": null,
			      "actionPayload": null,
			      "actionBranchName": "ai_notify"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 6,
			      "phase": "post_stop_packet_side_effect",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000006,
			      "monotonicNanos": 3000600,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "{{javaSourceFile}}",
			      "javaLine": {{invalidBranchLine}},
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
			      "emotion": null,
			      "actionPayload": {{actionPayloadJson}},
			      "actionBranchName": "{{actionBranchName}}"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 7,
			      "phase": "packet_return",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000007,
			      "monotonicNanos": 3000700,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "{{javaSourceFile}}",
			      "javaLine": {{invalidBranchLine}},
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
			      "emotion": null,
			      "actionPayload": null,
			      "actionBranchName": "packet_return"
			    }
			  ],
			  "notes": [
			    "Java calls stopProtectionActiveTask before this packet validation branch returns.",
			    "Inline fixture proves reader binding only, not Java runtime parity."
			  ]
			}
			""";

		var artifact = JsonSerializer.Deserialize<ProtectionStopTriggerJavaTraceArtifact>(json, JsonOptions);
		Assert.NotNull(artifact);
		AssertStopAfterInvalidArtifact(artifact);
		return artifact;
	}

	private static ProtectionStopTriggerJavaTraceArtifact ParseScheduledCallbackArtifact(
		string scenario,
		bool replacementCancelsOldFuture)
	{
		var stopOrigin = replacementCancelsOldFuture ? "replacement_race_current_future" : "scheduled_callback";
		var oldFuturePresent = replacementCancelsOldFuture ? "true" : "false";
		var oldFutureCancelArgument = replacementCancelsOldFuture ? "false" : "null";
		var oldFutureCancelResult = replacementCancelsOldFuture ? "false" : "null";
		var json = $$"""
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "{{scenario}}",
			  "runtimeFacts": {
			    "serverFlavor": "java",
			    "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			    "playerObjectId": 1001,
			    "worldId": 210010000,
			    "expectedReturnReason": "{{stopOrigin}}"
			  },
			  "javaSources": [
			    "PlayerController.startProtectionActiveTask",
			    "ThreadPoolManager.schedule",
			    "CreatureController.addTask",
			    "PlayerController.stopProtectionActiveTask",
			    "CreatureController.cancelTask"
			  ],
			  "traces": [
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 0,
			      "phase": "schedule_enter",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": false,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000000,
			      "monotonicNanos": 4000000,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 634,
			      "player": {
			        "objectId": 1001,
			        "spawned": true,
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": false,
			        "protectionActiveAfter": true,
			        "visualStateBefore": [],
			        "visualStateAfter": ["BLINKING"]
			      },
			      "movement": null,
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": {
			        "delayMillis": 60000,
			        "timeUnit": "MILLISECONDS",
			        "runnableWrapperApplied": true,
			        "callbackMethod": "PlayerController.stopProtectionActiveTask",
			        "oldFuturePresent": false,
			        "oldFutureCancelArgument": null,
			        "oldFutureCancelResult": null,
			        "newFutureStored": false
			      },
			      "actionBranchName": "schedule_protection_active_task"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 1,
			      "phase": "task_add",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": false,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000000001,
			      "monotonicNanos": 4000100,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "CreatureController.java",
			      "javaLine": 383,
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
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": {
			        "delayMillis": 60000,
			        "timeUnit": "MILLISECONDS",
			        "runnableWrapperApplied": true,
			        "callbackMethod": "PlayerController.stopProtectionActiveTask",
			        "oldFuturePresent": {{oldFuturePresent}},
			        "oldFutureCancelArgument": {{oldFutureCancelArgument}},
			        "oldFutureCancelResult": {{oldFutureCancelResult}},
			        "newFutureStored": true
			      },
			      "actionBranchName": "{{(replacementCancelsOldFuture ? "replace_old_protection_future" : "store_new_protection_future")}}"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 2,
			      "phase": "callback_enter",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": false,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000060000,
			      "monotonicNanos": 100000000,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "ThreadPoolManager.java",
			      "javaLine": 53,
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
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": {
			        "delayMillis": 60000,
			        "timeUnit": "MILLISECONDS",
			        "runnableWrapperApplied": true,
			        "callbackMethod": "PlayerController.stopProtectionActiveTask",
			        "oldFuturePresent": false,
			        "oldFutureCancelArgument": null,
			        "oldFutureCancelResult": null,
			        "newFutureStored": true
			      },
			      "actionBranchName": "scheduled_future_callback_enter"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 3,
			      "phase": "stop_call_enter",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000060001,
			      "monotonicNanos": 100000100,
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
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": null,
			      "actionBranchName": "stop_protection_active_task"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 4,
			      "phase": "task_cancel",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000060002,
			      "monotonicNanos": 100000200,
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
			        "stopOrigin": "{{stopOrigin}}"
			      },
			      "fanout": null,
			      "aiNotify": null,
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": null,
			      "actionBranchName": "cancel_current_protection_future"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 5,
			      "phase": "visual_mutate",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000060003,
			      "monotonicNanos": 100000300,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 645,
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
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": null,
			      "actionBranchName": "unset_blinking"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 6,
			      "phase": "state_broadcast",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000060004,
			      "monotonicNanos": 100000400,
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
			      "fanout": {
			        "packetName": "SM_PLAYER_STATE",
			        "includeSelf": true,
			        "recipientCount": 2,
			        "knownListOrderIsParityKey": false
			      },
			      "aiNotify": null,
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": null,
			      "actionBranchName": "broadcast_state"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 7,
			      "phase": "ai_notify_enqueue",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000060005,
			      "monotonicNanos": 100000500,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 647,
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
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": null,
			      "actionBranchName": "ai_notify"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 8,
			      "phase": "callback_return",
			      "packetName": "SCHEDULED_PROTECTION_ACTIVE_CALLBACK",
			      "returnReason": "{{stopOrigin}}",
			      "stopCalled": true,
			      "expectsStopProtectionCall": true,
			      "wallTimeEpochMillis": 1760000060006,
			      "monotonicNanos": 100000600,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "PlayerController.java",
			      "javaLine": 648,
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
			      "emotion": null,
			      "actionPayload": null,
			      "scheduler": null,
			      "actionBranchName": "callback_return"
			    }
			  ],
			  "notes": [
			    "Wall-clock timestamps are diagnostics only; eventSeq is the ordering key.",
			    "Inline fixture proves reader binding only, not Java runtime parity."
			  ]
			}
			""";

		var artifact = JsonSerializer.Deserialize<ProtectionStopTriggerJavaTraceArtifact>(json, JsonOptions);
		Assert.NotNull(artifact);
		AssertArtifactSemantics(artifact);
		return artifact;
	}

	private static ProtectionStopTriggerJavaTraceArtifact ParseNoStopArtifact(
		string scenario,
		string packetName,
		string returnReason,
		string javaSourceFile,
		int javaLine,
		bool includeMovement)
	{
		var movementJson = includeMovement
			? $$"""
				  {
				    "oldX": 100.0,
				    "oldY": 200.0,
				    "oldZ": 50.0,
				    "packetX": 100.0,
				    "packetY": 200.0,
				    "packetZ": 50.0,
				    "zDelta": 0.0,
				    "heading": 91,
				    "movementType": "turn",
				    "antiHackAccepted": {{(returnReason == "anti_hack_reject" ? "false" : "true")}},
				    "teleportationModeAbsoluteMove": {{(returnReason == "teleportation_absolute_move_return" ? "true" : "false")}},
				    "stopThresholdExceeded": false
				  }
				"""
			: "null";
		var emotionJson = packetName == "CM_EMOTION"
			? $$"""
				  {
				    "emotionType": "EMOTE",
				    "emotionId": 131,
				    "emotionStance": "{{returnReason}}",
				    "emotionCanUse": false,
				    "emotionBroadcasted": false
				  }
				"""
			: "null";
		var json = $$"""
			{
			  "schemaVersion": 1,
			  "javaCommit": "abcdef1",
			  "scenario": "{{scenario}}",
			  "runtimeFacts": {
			    "serverFlavor": "java",
			    "packetName": "{{packetName}}",
			    "playerObjectId": 1001,
			    "worldId": 210010000,
			    "expectedReturnReason": "{{returnReason}}"
			  },
			  "javaSources": [
			    "{{packetName}}.runImpl"
			  ],
			  "traces": [
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 0,
			      "phase": "packet_enter",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": false,
			      "expectsStopProtectionCall": false,
			      "wallTimeEpochMillis": 1760000000000,
			      "monotonicNanos": 2000000,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "{{javaSourceFile}}",
			      "javaLine": 76,
			      "player": {
			        "objectId": 1001,
			        "spawned": {{(returnReason == "not_spawned" ? "false" : "true")}},
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": true,
			        "protectionActiveAfter": true,
			        "visualStateBefore": ["BLINKING"],
			        "visualStateAfter": ["BLINKING"]
			      },
			      "movement": {{movementJson}},
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "emotion": {{emotionJson}},
			      "actionBranchName": "{{returnReason}}"
			    },
			    {
			      "schemaVersion": 1,
			      "traceId": "{{scenario}}-001",
			      "eventSeq": 1,
			      "phase": "guard_return",
			      "packetName": "{{packetName}}",
			      "returnReason": "{{returnReason}}",
			      "stopCalled": false,
			      "expectsStopProtectionCall": false,
			      "wallTimeEpochMillis": 1760000000001,
			      "monotonicNanos": 2000100,
			      "timestampIsParityKey": false,
			      "javaSourceFile": "{{javaSourceFile}}",
			      "javaLine": {{javaLine}},
			      "player": {
			        "objectId": 1001,
			        "spawned": {{(returnReason == "not_spawned" ? "false" : "true")}},
			        "flying": false,
			        "dead": false,
			        "protectionActiveBefore": true,
			        "protectionActiveAfter": true,
			        "visualStateBefore": ["BLINKING"],
			        "visualStateAfter": ["BLINKING"]
			      },
			      "movement": {{movementJson}},
			      "taskCancellation": null,
			      "fanout": null,
			      "aiNotify": null,
			      "emotion": {{emotionJson}},
			      "actionBranchName": "{{returnReason}}"
			    }
			  ],
			  "notes": [
			    "No stopProtectionActiveTask call is expected for this branch.",
			    "Inline fixture proves reader binding only, not Java runtime parity."
			  ]
			}
			""";

		var artifact = JsonSerializer.Deserialize<ProtectionStopTriggerJavaTraceArtifact>(json, JsonOptions);
		Assert.NotNull(artifact);
		AssertNoStopArtifact(artifact);
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
		ProtectionStopTriggerEmotionSnapshot? Emotion,
		ProtectionStopTriggerActionPayloadSnapshot? ActionPayload,
		ProtectionStopTriggerSchedulerSnapshot? Scheduler,
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

	private sealed record ProtectionStopTriggerEmotionSnapshot(
		string EmotionType,
		int EmotionId,
		string EmotionStance,
		bool EmotionCanUse,
		bool EmotionBroadcasted);

	private sealed record ProtectionStopTriggerActionPayloadSnapshot(
		int? ItemObjectId,
		string? ItemLookupResult,
		string? RestrictionResult,
		string? ItemActionResult,
		int? CompositeToolObjectId,
		int? CompositeFirstObjectId,
		int? CompositeSecondObjectId,
		string? CompositeCanActResult);

	private sealed record ProtectionStopTriggerSchedulerSnapshot(
		int DelayMillis,
		string TimeUnit,
		bool RunnableWrapperApplied,
		string CallbackMethod,
		bool OldFuturePresent,
		bool? OldFutureCancelArgument,
		bool? OldFutureCancelResult,
		bool NewFutureStored);
}
