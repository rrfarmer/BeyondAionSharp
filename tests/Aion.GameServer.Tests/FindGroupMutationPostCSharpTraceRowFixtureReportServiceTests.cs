using Aion.Commons.Network;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Network.Aion;
using Aion.GameServer.Network.Aion.ClientPackets;
using Aion.GameServer.Services;

namespace Aion.GameServer.Tests;

public sealed class FindGroupMutationPostCSharpTraceRowFixtureReportServiceTests
{
	[Fact]
	public void Create_DefaultReportBlocksOnMissingCSharpRows()
	{
		var report = FindGroupMutationPostCSharpTraceRowFixtureReportService.Create();

		Assert.Equal(FindGroupMutationPostCSharpTraceRowFixtureReportStatus.BlockedMissingCSharpRows, report.Status);
		Assert.Empty(report.Rows);
		Assert.False(report.IsLive);
		Assert.True(report.FeedsComparisonInputEnvelope);
		Assert.False(report.ReadyForComparisonExecution);
		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingJavaRows, report.Envelope.Status);
		Assert.False(report.HasShapeValidJavaRows);
		Assert.Equal("cm-find-group-direct-mutation-post-boundary", report.TraceName);
	}

	[Fact]
	public void Create_DisabledProjectionRowsAreShapeValidButDoNotSatisfyLiveGate()
	{
		var csharpRows = CreateDisabledProjectionRows();

		var report = FindGroupMutationPostCSharpTraceRowFixtureReportService.Create(csharpRows, RepositoryJavaArtifacts());

		Assert.Equal(FindGroupMutationPostCSharpTraceRowFixtureReportStatus.BlockedNonLiveRowsOnly, report.Status);
		Assert.True(report.HasActionTwoCSharpRow);
		Assert.True(report.HasActionSixCSharpRow);
		Assert.False(report.HasActionTwoLiveCSharpRow);
		Assert.False(report.HasActionSixLiveCSharpRow);
		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingLiveCSharpRows, report.Envelope.Status);
		Assert.All(report.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostCSharpTraceRowFixtureRowStatus.ShapeValidNonLiveProjection, row.Status);
			Assert.True(row.IsShapeValid);
			Assert.False(row.IsLiveEvidence);
			Assert.True(row.BlocksComparisonInput);
			Assert.Contains("executor=False", row.Evidence, StringComparison.Ordinal);
			Assert.Contains("registry=False", row.Evidence, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Create_DisabledProjectionRowsPreserveJavaActionTwoAndSixShape()
	{
		var csharpRows = CreateDisabledProjectionRows();

		var report = FindGroupMutationPostCSharpTraceRowFixtureReportService.Create(csharpRows, RepositoryJavaArtifacts());

		var actionTwo = Assert.Single(report.Rows, row => row.Action == 2);
		var actionSix = Assert.Single(report.Rows, row => row.Action == 6);

		Assert.Contains("CM_FIND_GROUP.runImpl", report.JavaSource, StringComparison.Ordinal);
		Assert.Contains("FindGroupService.addRecruitment/addApplication", report.JavaSource, StringComparison.Ordinal);
		Assert.Equal([2, 6], report.Envelope.CSharpRows.Select(row => row.Action));
		Assert.Contains(report.Envelope.CSharpRows, row =>
			row.Action == 2
			&& row.MutationKind == "Recruitment"
			&& row.PostedSystemMessageId == 1400392
			&& row.RefreshedListAction == 0
			&& !row.IsLiveEvidence);
		Assert.Contains(report.Envelope.CSharpRows, row =>
			row.Action == 6
			&& row.MutationKind == "Application"
			&& row.PostedSystemMessageId == 1400393
			&& row.RefreshedListAction == 4
			&& !row.IsLiveEvidence);
		Assert.Contains("schema shape only", actionTwo.Notes, StringComparison.Ordinal);
		Assert.Contains("schema shape only", actionSix.Notes, StringComparison.Ordinal);
	}

	[Fact]
	public void Create_LiveMarkedRowsSatisfyOnlyCSharpRowsGateAndStillNeedReadiness()
	{
		var liveRows = CreateDisabledProjectionRows()
			.Select(row => row with
			{
				ExecutorInvokedFromBoundary = true,
				RegistrySendsObservedInOrder = true,
			})
			.ToArray();

		var report = FindGroupMutationPostCSharpTraceRowFixtureReportService.Create(liveRows, RepositoryJavaArtifacts());

		Assert.Equal(FindGroupMutationPostCSharpTraceRowFixtureReportStatus.ReadyWithLiveRows, report.Status);
		Assert.True(report.HasActionTwoLiveCSharpRow);
		Assert.True(report.HasActionSixLiveCSharpRow);
		Assert.Equal(FindGroupMutationPostComparisonInputEnvelopeStatus.BlockedMissingReadiness, report.Envelope.Status);
		Assert.False(report.ReadyForComparisonExecution);
		Assert.All(report.Rows, row =>
		{
			Assert.Equal(FindGroupMutationPostCSharpTraceRowFixtureRowStatus.LiveBoundaryEvidence, row.Status);
			Assert.False(row.BlocksComparisonInput);
			Assert.Contains("live boundary", row.Notes, StringComparison.Ordinal);
		});
	}

	private static IReadOnlyList<FindGroupDirectPacketMutationPostBoundaryTraceExport> CreateDisabledProjectionRows()
	{
		return
		[
			CreateDisabledProjectionRowForActionTwo(),
			CreateDisabledProjectionRowForActionSix(),
		];
	}

	private static FindGroupMutationPostJavaTraceArtifactDirectoryReport RepositoryJavaArtifacts()
	{
		var root = FindRepositoryRoot();
		return FindGroupMutationPostJavaTraceArtifactDirectoryReportService.Create(
			Path.Combine(root, "parity-artifacts", "find-group", "mutation-post", "java"));
	}

	private static string FindRepositoryRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "docs", "csharp-port.md")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new InvalidOperationException("Repository root could not be located.");
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport CreateDisabledProjectionRowForActionTwo()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var recruiter = CreatePlayer(0x01020304, "Recruiter", "ELYOS", "CLERIC", 65);
		var hidden = CreatePlayer(0x01020306, "Hidden", "ASMODIANS", "RANGER", 61);
		findGroupService.AddRecruitment(hidden, "Hidden entry", groupType: 4, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(2);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need healer");
				buffer.WriteC(3);
			});

		return Project(findGroupService, recruiter, packet, nowEpochSeconds: 200);
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport CreateDisabledProjectionRowForActionSix()
	{
		var findGroupService = new FindGroupRecruitmentPlanService();
		var applicant = CreatePlayer(0x01020305, "Applicant", "ELYOS", "RANGER", 65);
		var hidden = CreatePlayer(0x01020307, "Hidden", "ASMODIANS", "CLERIC", 61);
		findGroupService.AddApplication(hidden, "Hidden app", groupType: 2, classId: 10, level: 61, nowEpochSeconds: 100);
		var packet = CreateFindGroupPacket(
			buffer =>
			{
				buffer.WriteC(6);
				buffer.WriteD(0x7F7F7F7F);
				buffer.WriteS("Need group");
				buffer.WriteC(2);
				buffer.WriteC(5);
				buffer.WriteC(65);
			});

		return Project(findGroupService, applicant, packet, nowEpochSeconds: 201);
	}

	private static FindGroupDirectPacketMutationPostBoundaryTraceExport Project(
		FindGroupRecruitmentPlanService findGroupService,
		Player player,
		CmFindGroup packet,
		int nowEpochSeconds)
	{
		var compositionPlan = new FindGroupConnectionClientActionCompositionPlanService(
				new FindGroupClientActionPlanService(findGroupService))
			.CreateDisabledPlan(player, packet, nowEpochSeconds);
		var projection = FindGroupDirectPacketMutationPostBoundaryTraceSchemaService.CreateExportFromDisabledPlan(compositionPlan);

		Assert.Equal(FindGroupDirectPacketMutationPostBoundaryTraceExportProjectionStatus.Created, projection.Status);
		return projection.Export;
	}

	private static CmFindGroup CreateFindGroupPacket(Action<PacketBuffer> writePayload)
	{
		using var buffer = new PacketBuffer();
		writePayload(buffer);
		var packet = new CmFindGroup(opCode: 0, validStates: new HashSet<GameConnectionState>());
		packet.ReadFrom(new PacketBuffer(buffer.ToArray()));
		return packet;
	}

	private static Player CreatePlayer(int objectId, string name, string race, string playerClass, int level)
	{
		return new Player
		{
			ObjectId = objectId,
			Name = name,
			Race = race,
			PlayerClass = playerClass,
			Level = level,
		};
	}
}
