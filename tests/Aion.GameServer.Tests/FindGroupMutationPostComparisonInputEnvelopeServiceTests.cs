using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostComparisonInputEnvelopeServiceTests
{
	[Fact]
	public void Create_DefaultEnvelopeBlocksOnMissingJavaRowsAndIsNonLive()
	{
		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create();

		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingJavaRows, envelope.Status);
		Assert.False(envelope.IsLive);
		Assert.False(envelope.HasActionTwoJavaRow);
		Assert.False(envelope.HasActionSixJavaRow);
		Assert.False(envelope.HasActionTwoLiveCSharpRow);
		Assert.False(envelope.HasActionSixLiveCSharpRow);
		Assert.True(envelope.HasProjectionMetadata);
		Assert.True(envelope.HasReadinessAggregate);
		Assert.True(envelope.HasGuardedFixtureResultContract);
		Assert.True(envelope.HasResultContract);
		Assert.False(envelope.ReadyForComparisonExecution);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", envelope.TraceName);
	}

	[Fact]
	public void Create_ShapeValidJavaRowsMoveBlockerToLiveCSharpRows()
	{
		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create(
			javaArtifacts: ShapeValidJavaArtifacts());

		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingLiveCSharpRows, envelope.Status);
		Assert.True(envelope.HasActionTwoJavaRow);
		Assert.True(envelope.HasActionSixJavaRow);
		Assert.Equal([2, 6], envelope.JavaRows.Select(row => row.Action));
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.JavaRows
			&& gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByShapeValidJavaRows
			&& gate.Evidence.Contains("shapeValidRows=2", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_DisabledCSharpProjectionRowsDoNotCountAsLiveRows()
	{
		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			csharpRows:
			[
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(2),
				FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(6),
			]);

		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingLiveCSharpRows, envelope.Status);
		Assert.False(envelope.HasActionTwoLiveCSharpRow);
		Assert.False(envelope.HasActionSixLiveCSharpRow);
		Assert.Equal(2, envelope.CSharpRows.Count);
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.CSharpRows
			&& gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingLiveCSharpRows
			&& gate.Notes.Contains("not disabled sample projections", StringComparison.Ordinal));
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.GuardedFixtureResultContract
			&& gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingLiveCSharpRows
			&& gate.Evidence.Contains("acceptedRows=0", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_LiveRowsWithoutReadinessStillBlockReadiness()
	{
		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			csharpRows: LiveCSharpRows());

		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingReadiness, envelope.Status);
		Assert.True(envelope.HasActionTwoLiveCSharpRow);
		Assert.True(envelope.HasActionSixLiveCSharpRow);
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.GuardedFixtureResultContract
			&& gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByLiveCSharpRows
			&& gate.Evidence.Contains("acceptedRows=2", StringComparison.Ordinal));
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.ReadinessAggregate
			&& gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.BlockedMissingReadiness
			&& gate.Evidence.Contains("ready=False", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_ReadyInputsProduceReadyEnvelopeWithoutExecutingComparison()
	{
		var keyProjection = FindGroupMutationPostComparisonKeyProjectionMetadataService.Create();
		var readiness = ReadyReadiness(keyProjection);
		var contract = FindGroupMutationPostComparisonExecutionResultContractService.Create(keyProjection, readiness);

		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			csharpRows: LiveCSharpRows(),
			keyProjection: keyProjection,
			readiness: readiness,
			resultContract: contract);

		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.ReadyForComparisonExecution, envelope.Status);
		Assert.True(envelope.ReadyForComparisonExecution);
		Assert.DoesNotContain(envelope.Gates, gate => gate.BlocksComparisonExecution);
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.ResultContract
			&& gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByReadyContract
			&& gate.Evidence.Contains("ReadyForComparisonExecution", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_KeepsProjectionAndResultContractEvidenceInEnvelope()
	{
		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create();

		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.ProjectionMetadata
			&& gate.Status == FindGroupMutationPostComparisonInputEnvelopeGateStatus.SatisfiedByNonLiveMetadata
			&& gate.Evidence.Contains("ignoredRuntimeFields=traceSource/serverEpochSeconds", StringComparison.Ordinal));
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.ResultContract
			&& gate.Evidence.Contains("differenceKinds=", StringComparison.Ordinal)
			&& gate.Notes.Contains("mismatch reports", StringComparison.Ordinal));
	}

	[Fact]
	public void Create_BadShapeRowsDoNotEnterLiveHandoffThroughBooleanFlagsAlone()
	{
		var envelope = FindGroupMutationPostComparisonInputEnvelopeService.Create(
			javaArtifacts: ShapeValidJavaArtifacts(),
			csharpRows:
			[
				LiveCSharpRow(2) with { PostedSystemMessageId = 1400393 },
				LiveCSharpRow(6),
			]);

		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingLiveCSharpRows, envelope.Status);
		Assert.False(envelope.HasActionTwoLiveCSharpRow);
		Assert.True(envelope.HasActionSixLiveCSharpRow);
		Assert.Contains(envelope.CSharpRows, row =>
			row.Action == 2
			&& !row.IsShapeValid
			&& !row.IsLiveEvidence
			&& row.Evidence.Contains("RejectedUnexpectedPacketShape", StringComparison.Ordinal));
		Assert.Contains(envelope.Gates, gate =>
			gate.Gate == FindGroupMutationPostComparisonInputEnvelopeGate.GuardedFixtureResultContract
			&& gate.Evidence.Contains("action2Live=False", StringComparison.Ordinal)
			&& gate.Evidence.Contains("action6Live=True", StringComparison.Ordinal));
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport ShapeValidJavaArtifacts() =>
		new(
			FindGroupMutationPostJavaTraceArtifactDirectoryStatus.AllExpectedArtifactsShapeValid,
			FindGroupMutationPostJavaTraceArtifactFileReportService.DefaultArtifactRoot,
			[
				ShapeValidFile(2),
				ShapeValidFile(6),
			],
			HasGeneratedJavaArtifacts: true,
			HasAllExpectedFiles: true,
			HasOnlyShapeValidArtifacts: true,
			ReadyForRuntimeComparison: false,
			"shape-valid only");

	private static FindGroupMutationPostJavaTraceArtifactDirectoryFileRow ShapeValidFile(int action) =>
		new(
			action,
			FindGroupMutationPostJavaTraceArtifactFileReportService.FileNameForAction(action),
			FindGroupMutationPostJavaTraceArtifactDirectoryFileStatus.ShapeValid,
			new FindGroupMutationPostJavaTraceArtifactValidationReport(
				[],
				IsValid: true,
				new FindGroupMutationPostJavaTraceArtifactMetadata(
					SchemaVersion: 1,
					TraceName: "cm-find-group-direct-mutation-post-boundary",
					[
						new FindGroupMutationPostJavaTraceArtifactValidationTraceRow(
							SchemaVersion: 1,
							TraceName: "cm-find-group-direct-mutation-post-boundary",
							TraceSource: "Java",
							action,
							MutationKind: action == 2 ? "Recruitment" : "Application",
							PostedSystemMessageId: action == 2 ? 1400392 : 1400393,
							RefreshedListAction: action == 2 ? 0 : 4)
					])),
			"shape-valid only");

	private static IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceExport> LiveCSharpRows() =>
		[
			LiveCSharpRow(2),
			LiveCSharpRow(6),
		];

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport LiveCSharpRow(int action) =>
		FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateSampleExport(action) with
		{
			BoundaryAccepted = true,
			ExecutorInvokedFromBoundary = true,
			RegistrySendsObservedInOrder = true,
		};

	private static FindGroupMutationPostTraceRowReadinessAggregate ReadyReadiness(
		FindGroupMutationPostComparisonKeyProjectionMetadata keyProjection) =>
		new(
			FindGroupMutationPostTraceRowReadinessStatus.Ready,
			[],
			HasJavaCaptureRunbook: true,
			HasCSharpLiveTraceRowFixturePlan: true,
			HasGuardedLiveBoundaryFixtureSkeleton: true,
			HasRegistryObservationContract: true,
			HasArtifactComparisonPreflight: true,
			NeedsJavaFixture: false,
			NeedsJavaInstrumentation: false,
			NeedsGeneratedJavaArtifacts: false,
			HasCSharpTraceRowShapeInputs: true,
			NeedsGuardedBoundaryFixture: false,
			NeedsCSharpLiveRows: false,
			NeedsRegistryObservation: false,
			NeedsComparisonExecution: false,
			ReadyForRuntimeComparison: true,
			TraceName: keyProjection.TraceName,
			JavaSource: keyProjection.JavaSource,
			IsLive: false);
}
