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
	}

	[Fact]
	public void Validate_RejectsUnsupportedSchemaVersion()
	{
		var report = PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidatorService.Validate(
			RepresentativeArtifactJson.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));

		Assert.False(report.IsValidSchemaV1);
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
		Assert.Contains(report.Issues, issue =>
			issue.Code == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactValidationIssueCode.TimestampMarkedAsParityKey);
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
