namespace Aion.GameServer.Services;

public enum PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus
{
	SatisfiedByNonLiveMetadata,
	BlockedMissingJavaKeys,
	BlockedMissingCSharpKeys,
	BlockedKeyMismatch,
	BlockedComparisonNotExecuted,
}

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey(
	string Source,
	string Scenario,
	int? EventSeq,
	string Phase,
	string PacketName,
	string ReturnReason,
	bool? StopCalled,
	bool? ExpectsStopProtectionCall,
	bool? TimestampIsParityKey,
	int? PlayerObjectId,
	bool? PlayerSpawned,
	bool? PlayerFlying,
	bool? PlayerDead,
	bool? ProtectionActiveBefore,
	bool? ProtectionActiveAfter,
	IReadOnlyList<string> VisualStateBefore,
	IReadOnlyList<string> VisualStateAfter,
	string Fingerprint);

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow(
	int Order,
	PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus Status,
	bool BlocksComparison,
	string Evidence,
	string Notes);

public sealed record PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport(
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow> Rows,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> JavaKeys,
	IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> CSharpKeys,
	bool HasJavaKeys,
	bool HasCSharpKeys,
	bool HasKeyAlignment,
	bool NeedsJavaKeys,
	bool NeedsCSharpKeys,
	bool NeedsKeyAlignment,
	bool NeedsComparisonExecution,
	bool ReadyForRuntimeComparison,
	string JavaSource,
	bool IsLive);

/// <summary>
/// Java parity breadcrumb: non-live comparison-key projection for future protection stop-trigger runtime comparison.
/// This converts parsed schema-v1 Java metadata and C# trace rows into deterministic non-time keys only.
/// </summary>
public static class PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReportService
{
	public static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport Create(
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace)
	{
		var rows = new List<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow>();
		var javaKeys = ProjectJavaKeys(javaArtifacts);
		var csharpKeys = ProjectCSharpKeys(csharpTrace);

		AddJavaKeys(rows, javaArtifacts, javaKeys);
		AddCSharpKeys(rows, csharpTrace, csharpKeys);
		AddKeyAlignment(rows, javaArtifacts, csharpTrace, javaKeys, csharpKeys);
		AddComparisonExecution(rows);

		var rowArray = rows.ToArray();

		return new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionReport(
			rowArray,
			javaKeys,
			csharpKeys,
			HasJavaKeys: javaKeys.Count > 0,
			HasCSharpKeys: csharpKeys.Count > 0,
			HasKeyAlignment: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.SatisfiedByNonLiveMetadata && row.Evidence.StartsWith("alignedKeys=", StringComparison.Ordinal)),
			NeedsJavaKeys: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedMissingJavaKeys),
			NeedsCSharpKeys: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedMissingCSharpKeys),
			NeedsKeyAlignment: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedKeyMismatch),
			NeedsComparisonExecution: rowArray.Any(row => row.Status == PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedComparisonNotExecuted),
			ReadyForRuntimeComparison: false,
			"Parsed Java protection stop-trigger metadata and future C# runtime trace rows",
			IsLive: false);
	}

	private static void AddJavaKeys(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts,
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> javaKeys)
	{
		var valid = javaArtifacts?.Status == PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid
			&& javaArtifacts.Files.All(file => file.ValidationReport.Metadata != null)
			&& javaKeys.Count > 0
			&& javaKeys.All(key => key.TimestampIsParityKey != true);

		Add(rows,
			valid
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.SatisfiedByNonLiveMetadata
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedMissingJavaKeys,
			blocks: !valid,
			javaArtifacts == null
				? "no Java artifact directory report supplied"
				: $"status={javaArtifacts.Status}; files={javaArtifacts.Files.Count}; keys={javaKeys.Count}; metadataFiles={javaArtifacts.Files.Count(file => file.ValidationReport.Metadata != null)}",
			valid
				? "Parsed Java trace metadata was projected into non-time comparison keys."
				: "Shape-valid parsed Java metadata with non-time parity keys is required before key alignment.");
	}

	private static void AddCSharpKeys(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow> rows,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace,
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> csharpKeys)
	{
		var valid = csharpTrace is { HasLivePacketHooks: true, ValidationIssues.Count: 0 }
			&& csharpKeys.Count > 0
			&& csharpKeys.All(key => key.TimestampIsParityKey != true);

		Add(rows,
			valid
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.SatisfiedByNonLiveMetadata
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedMissingCSharpKeys,
			blocks: !valid,
			csharpTrace == null
				? "no C# runtime trace report supplied"
				: $"rows={csharpTrace.TraceRows.Count}; keys={csharpKeys.Count}; validationIssues={csharpTrace.ValidationIssues.Count}; hasLivePacketHooks={csharpTrace.HasLivePacketHooks}",
			valid
				? "C# trace rows were projected into non-time comparison keys."
				: "Valid C# trace rows with live-hook metadata and no timestamp parity keys are required before key alignment.");
	}

	private static void AddKeyAlignment(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow> rows,
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts,
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace,
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> javaKeys,
		IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> csharpKeys)
	{
		if (javaArtifacts?.Status != PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid
			|| javaArtifacts.Files.Any(file => file.ValidationReport.Metadata == null)
			|| csharpTrace is not { HasLivePacketHooks: true, ValidationIssues.Count: 0 }
			|| javaKeys.Count == 0
			|| csharpKeys.Count == 0)
		{
			Add(rows,
				PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedKeyMismatch,
				blocks: true,
				"key alignment prerequisites missing",
				"Need parsed Java metadata and valid C# trace rows before comparison keys can be aligned.");
			return;
		}

		var javaFingerprints = javaKeys.Select(key => key.Fingerprint).OrderBy(value => value, StringComparer.Ordinal).ToArray();
		var csharpFingerprints = csharpKeys.Select(key => key.Fingerprint).OrderBy(value => value, StringComparer.Ordinal).ToArray();
		var matches = javaFingerprints.SequenceEqual(csharpFingerprints, StringComparer.Ordinal);

		Add(rows,
			matches
				? PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.SatisfiedByNonLiveMetadata
				: PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedKeyMismatch,
			blocks: !matches,
			matches
				? $"alignedKeys={javaFingerprints.Length}"
				: $"javaOnly={string.Join(",", javaFingerprints.Except(csharpFingerprints, StringComparer.Ordinal))}; csharpOnly={string.Join(",", csharpFingerprints.Except(javaFingerprints, StringComparer.Ordinal))}",
			matches
				? "Projected Java and C# non-time keys align, but this is still synthetic metadata only."
				: "Projected Java and C# non-time keys differ; runtime comparison must not execute.");
	}

	private static void AddComparisonExecution(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow> rows)
	{
		Add(rows,
			PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus.BlockedComparisonNotExecuted,
			blocks: true,
			"key projection only; no Java/C# runtime comparison executed",
			"Verified parity cannot be claimed from projected keys without generated Java artifacts and live C# trace output.");
	}

	private static IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> ProjectJavaKeys(
		PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryReport? javaArtifacts)
	{
		if (javaArtifacts?.Status != PlayerProtectionActiveTaskStopTriggerJavaTraceArtifactDirectoryStatus.AllArtifactsShapeValid)
			return [];

		return javaArtifacts.Files
			.Select(file => file.ValidationReport.Metadata)
			.Where(metadata => metadata != null)
			.SelectMany(metadata => metadata!.TraceRows.Select(row => CreateKey(
				"java",
				metadata.Scenario,
				row.EventSeq,
				row.Phase,
				row.PacketName,
				row.ReturnReason,
				row.StopCalled,
				row.ExpectsStopProtectionCall,
				row.TimestampIsParityKey,
				row.Player.ObjectId,
				row.Player.Spawned,
				row.Player.Flying,
				row.Player.Dead,
				row.Player.ProtectionActiveBefore,
				row.Player.ProtectionActiveAfter,
				row.Player.VisualStateBefore,
				row.Player.VisualStateAfter)))
			.ToArray();
	}

	private static IReadOnlyList<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey> ProjectCSharpKeys(
		PlayerProtectionActiveTaskStopTriggerCSharpRuntimeTraceReport? csharpTrace)
	{
		if (csharpTrace == null)
			return [];

		return csharpTrace.TraceRows
			.Select(row => CreateKey(
				"csharp",
				row.Scenario,
				row.EventSeq,
				row.Phase,
				row.PacketName,
				row.ReturnReason,
				row.StopCalled,
				row.ExpectsStopProtectionCall,
				row.TimestampIsParityKey,
				row.Player.ObjectId,
				row.Player.Spawned,
				row.Player.Flying,
				row.Player.Dead,
				row.Player.ProtectionActiveBefore,
				row.Player.ProtectionActiveAfter,
				row.Player.VisualStateBefore,
				row.Player.VisualStateAfter))
			.ToArray();
	}

	private static PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey CreateKey(
		string source,
		string scenario,
		int? eventSeq,
		string phase,
		string packetName,
		string returnReason,
		bool? stopCalled,
		bool? expectsStopProtectionCall,
		bool? timestampIsParityKey,
		int? playerObjectId,
		bool? playerSpawned,
		bool? playerFlying,
		bool? playerDead,
		bool? protectionActiveBefore,
		bool? protectionActiveAfter,
		IReadOnlyList<string> visualStateBefore,
		IReadOnlyList<string> visualStateAfter)
	{
		var fingerprint = string.Join("|",
			scenario,
			Format(eventSeq),
			phase,
			packetName,
			returnReason,
			Format(stopCalled),
			Format(expectsStopProtectionCall),
			Format(timestampIsParityKey),
			Format(playerObjectId),
			Format(playerSpawned),
			Format(playerFlying),
			Format(playerDead),
			Format(protectionActiveBefore),
			Format(protectionActiveAfter),
			Format(visualStateBefore),
			Format(visualStateAfter));

		return new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKey(
			source,
			scenario,
			eventSeq,
			phase,
			packetName,
			returnReason,
			stopCalled,
			expectsStopProtectionCall,
			timestampIsParityKey,
			playerObjectId,
			playerSpawned,
			playerFlying,
			playerDead,
			protectionActiveBefore,
			protectionActiveAfter,
			visualStateBefore,
			visualStateAfter,
			fingerprint);
	}

	private static string Format(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "<null>";

	private static string Format(bool? value) => value switch
	{
		true => "true",
		false => "false",
		null => "<null>",
	};

	private static string Format(IReadOnlyList<string> values) =>
		values.Count == 0
			? "[]"
			: $"[{string.Join(",", values.OrderBy(value => value, StringComparer.Ordinal))}]";

	private static void Add(
		ICollection<PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow> rows,
		PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionStatus status,
		bool blocks,
		string evidence,
		string notes)
	{
		rows.Add(new PlayerProtectionActiveTaskStopTriggerRuntimeComparisonKeyProjectionRow(
			rows.Count + 1,
			status,
			blocks,
			evidence,
			notes));
	}
}
