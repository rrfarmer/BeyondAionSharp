using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorServiceTests
{
	[Fact]
	public void Validate_AcceptsRepresentativeTeleportSchemaV1ArtifactButKeepsRuntimeComparisonBlocked()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(RepresentativeArtifactJson);

		Assert.True(report.IsValidSchemaV1, string.Join("; ", report.Issues.Select(issue => $"{issue.Code}:{issue.Path}:{issue.Message}")));
		Assert.False(report.ReadyForRuntimeComparison);
		Assert.Empty(report.Issues);
		Assert.Contains("generated Java artifacts", report.Notes, StringComparison.Ordinal);
		Assert.NotNull(report.Metadata);
		Assert.Equal(1, report.Metadata.SchemaVersion);
		Assert.Equal("abcdef1", report.Metadata.JavaCommit);
		Assert.Equal("teleport-animation-done-validator-contract", report.Metadata.Scenario);
		Assert.Equal("CM_TELEPORT_ANIMATION_DONE", report.Metadata.RuntimePacketName);
		Assert.Equal("animation_done_no_pending_runnable_teleport_task", report.Metadata.RuntimeExpectedReturnReason);
		Assert.Equal(2, report.Metadata.TraceRows.Count);
		Assert.Equal(0, report.Metadata.TraceRows[0].EventSeq);
		Assert.Equal("teleport_task_remove", report.Metadata.TraceRows[0].Phase);
		Assert.False(report.Metadata.TraceRows[0].StopCalled);
		Assert.False(report.Metadata.TraceRows[0].ExpectsStopProtectionCall);
		Assert.False(report.Metadata.TraceRows[0].TimestampIsParityKey);
		Assert.Equal(1001, report.Metadata.TraceRows[0].Player.ObjectId);
		Assert.True(report.Metadata.TraceRows[0].Player.ProtectionActiveBefore);
		Assert.Equal(["BLINKING"], report.Metadata.TraceRows[0].Player.VisualStateBefore);
	}

	[Fact]
	public void Validate_RejectsUnsupportedSchemaVersion()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnsupportedSchemaVersion
			&& issue.Path == "$.schemaVersion");
	}

	[Fact]
	public void Validate_RequiresTopLevelArtifactFields()
	{
		const string json = """
			{
			  "schemaVersion": 1,
			  "scenario": "missing-required-fields"
			}
			""";

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue => issue.Path == "$.javaCommit");
		Assert.Contains(report.Issues, issue => issue.Path == "$.runtimeFacts");
		Assert.Contains(report.Issues, issue => issue.Path == "$.traces");
	}

	[Fact]
	public void Validate_RejectsOutOfOrderEventSeq()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"eventSeq\": 1", "\"eventSeq\": 0", StringComparison.Ordinal));

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.OutOfOrderEventSequence);
	}

	[Fact]
	public void Validate_RejectsUnknownPhaseAndReturnReason()
	{
		var json = RepresentativeArtifactJson
			.Replace("\"phase\": \"teleport_task_remove\"", "\"phase\": \"mystery_phase\"", StringComparison.Ordinal)
			.Replace("\"returnReason\": \"animation_done_no_pending_runnable_teleport_task\"", "\"returnReason\": \"mystery_reason\"", StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnknownPhase
			&& issue.Path == "$.traces[0].phase");
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.UnknownReturnReason
			&& issue.Path == "$.traces[0].returnReason");
	}

	[Fact]
	public void Validate_RejectsTimestampParityKeys()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"timestampIsParityKey\": false", "\"timestampIsParityKey\": true", StringComparison.Ordinal));

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.TimestampMarkedAsParityKey);
	}

	[Fact]
	public void Validate_RejectsMissingPlayerSnapshotNestedPayloadFields()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			        "visualStateAfter": ["BLINKING"]
			""",
			"""
			        "visualStateAfterMissing": ["BLINKING"]
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].player.visualStateAfter"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RejectsMissingSchedulerNestedPayloadFieldsWhenSchedulerIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			        "oldFutureCancelResult": null,
			""",
			string.Empty,
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].scheduler.oldFutureCancelResult"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RejectsMissingTaskCancellationNestedPayloadFieldsWhenTaskCancellationIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			      "taskCancellation": null,
			""",
			"""
			      "taskCancellation": {
			        "taskIdName": "PROTECTION_ACTIVE",
			        "taskIdOrdinal": 3,
			        "taskPresentBeforeCancel": true,
			        "taskRemovedBeforeCancel": true,
			        "futureCancelArgument": false,
			        "scheduledDelayMillis": 60000,
			        "stopOrigin": "first_action_packet"
			      },
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].taskCancellation.futureCancelResult"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RejectsMissingMovementNestedPayloadFieldsWhenMovementIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			      "movement": null,
			""",
			"""
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
			        "teleportationModeAbsoluteMove": false
			      },
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].movement.stopThresholdExceeded"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RejectsMissingFanoutNestedPayloadFieldsWhenFanoutIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			      "fanout": null,
			""",
			"""
			      "fanout": {
			        "packetName": "SM_PLAYER_STATE",
			        "includeSelf": true,
			        "recipientCount": 2
			      },
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].fanout.knownListOrderIsParityKey"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RejectsMissingAiNotifyNestedPayloadFieldsWhenAiNotifyIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			      "aiNotify": null,
			""",
			"""
			      "aiNotify": {
			        "notifyAiOnMoveCalled": true
			      },
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].aiNotify.ordering"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RejectsMissingEmotionNestedPayloadFieldsWhenEmotionIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			      "emotion": null,
			""",
			"""
			      "emotion": {
			        "emotionType": "EMOTE",
			        "emotionId": 131,
			        "emotionStance": "cm_emotion_validation_return",
			        "emotionCanUse": false
			      },
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].emotion.emotionBroadcasted"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RejectsMissingActionPayloadNestedPayloadFieldsWhenActionPayloadIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"""
			      "actionPayload": null,
			""",
			"""
			      "actionPayload": {
			        "itemObjectId": 987654,
			        "itemLookupResult": "found",
			        "restrictionResult": "passed",
			        "itemActionResult": "all_can_act_false",
			        "compositeToolObjectId": null,
			        "compositeFirstObjectId": null,
			        "compositeSecondObjectId": null
			      },
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].actionPayload.compositeCanActResult"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	[Fact]
	public void Validate_RequiresTraceActionBranchName()
	{
		var json = RepresentativeArtifactJson.Replace(
			"\"actionBranchName\": \"missing_task_returns_without_running\"",
			"\"actionBranchNameMissing\": \"missing_task_returns_without_running\"",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingTopLevelField
			&& issue.Path == "$.traces[0].actionBranchName");
	}

	[Fact]
	public void Validate_RejectsMissingCallerOriginNestedPayloadFieldsWhenCallerOriginIsPresent()
	{
		var json = RepresentativeArtifactJson.Replace(
			"\"callerOrigin\": null,",
			"""
			"callerOrigin": {
			  "callerName": "cm_level_ready_before_world_spawn",
			  "callerClass": "CM_LEVEL_READY",
			  "callerMethod": "runImpl",
			  "callerSourceFile": "CM_LEVEL_READY.java",
			  "callerLine": 53,
			  "startProtectionLine": 53,
			  "startsProtectionBeforeWorldSpawn": true,
			  "worldSpawnLine": 64,
			  "spawnedBeforeStart": false
			},
			""",
			StringComparison.Ordinal);

		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(json);

		Assert.False(report.IsValidSchemaV1);
		Assert.Null(report.Metadata);
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.MissingNestedPayloadField
			&& issue.Path == "$.traces[0].callerOrigin.ordering"
			&& issue.Message.Contains("nested payload", StringComparison.Ordinal));
	}

	private const string RepresentativeArtifactJson = """
		{
		  "schemaVersion": 1,
		  "javaCommit": "abcdef1",
		  "scenario": "teleport-animation-done-validator-contract",
		  "runtimeFacts": {
		    "serverFlavor": "java",
		    "packetName": "CM_TELEPORT_ANIMATION_DONE",
		    "playerObjectId": 1001,
		    "worldId": 301390000,
		    "expectedReturnReason": "animation_done_no_pending_runnable_teleport_task"
		  },
		  "javaSources": [
		    "CM_TELEPORT_ANIMATION_DONE.runImpl",
		    "CreatureController.getAndRemoveTask"
		  ],
		  "traces": [
		    {
		      "schemaVersion": 1,
		      "traceId": "teleport-animation-done-validator-contract-001",
		      "eventSeq": 0,
		      "phase": "teleport_task_remove",
		      "packetName": "CM_TELEPORT_ANIMATION_DONE",
		      "returnReason": "animation_done_no_pending_runnable_teleport_task",
		      "stopCalled": false,
		      "expectsStopProtectionCall": false,
		      "wallTimeEpochMillis": 1760000005000,
		      "monotonicNanos": 15000000,
		      "timestampIsParityKey": false,
		      "javaSourceFile": "CM_TELEPORT_ANIMATION_DONE.java",
		      "javaLine": 36,
		      "player": {
		        "objectId": 1001,
		        "spawned": false,
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
		        "delayMillis": 0,
		        "timeUnit": "CLIENT_ANIMATION_DONE",
		        "runnableWrapperApplied": false,
		        "callbackMethod": "none",
		        "oldFuturePresent": false,
		        "oldFutureCancelArgument": null,
		        "oldFutureCancelResult": null,
		        "newFutureStored": false
		      },
		      "callerOrigin": null,
		      "actionBranchName": "missing_task_returns_without_running"
		    },
		    {
		      "schemaVersion": 1,
		      "traceId": "teleport-animation-done-validator-contract-001",
		      "eventSeq": 1,
		      "phase": "packet_exit",
		      "packetName": "CM_TELEPORT_ANIMATION_DONE",
		      "returnReason": "animation_done_no_pending_runnable_teleport_task",
		      "stopCalled": false,
		      "expectsStopProtectionCall": false,
		      "wallTimeEpochMillis": 1760000005001,
		      "monotonicNanos": 15000100,
		      "timestampIsParityKey": false,
		      "javaSourceFile": "CM_TELEPORT_ANIMATION_DONE.java",
		      "javaLine": 47,
		      "player": {
		        "objectId": 1001,
		        "spawned": false,
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
		      "callerOrigin": null,
		      "actionBranchName": "packet_exit_noop"
		    }
		  ],
		  "notes": [
		    "Representative validator fixture only; not generated by Java runtime."
		  ]
		}
		""";
}
