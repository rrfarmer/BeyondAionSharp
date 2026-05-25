using Aion.GameServer.Dataholders;
using Aion.GameServer.Services;
using Aion.GameServer.World;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class QuestNpcStartRegistrationSourceRealDataAuditTests
{
	private const int ExpectedTotalSources = 5214;
	private const int ExpectedXmlSources = 4400;
	private const int ExpectedJavaHandlerSources = 814;
	private const int ExpectedDistinctNpcIds = 1668;
	private const int ExpectedDistinctQuestIds = 4503;
	private const int ExpectedRegisteredQuestStartPairs = 5214;
	private const int ExpectedLargestNpcQuestCount = 50;
	private const int ExpectedProjectedWorldQuestIds = 4503;

	private readonly ITestOutputHelper _output;

	public QuestNpcStartRegistrationSourceRealDataAuditTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void RealDataAudit_LoadsStagedQuestStartSourcesWithoutProductionWiring()
	{
		var result = LoadRealDataSources();
		var xmlSources = result.Sources.Where(source => source.SourceKind == QuestNpcStartRegistrationSourceKind.XmlQuest).ToArray();
		var javaSources = result.Sources.Where(source => source.SourceKind == QuestNpcStartRegistrationSourceKind.JavaHandler).ToArray();

		_output.WriteLine($"TotalSources={result.Sources.Count}");
		_output.WriteLine($"XmlSources={xmlSources.Length}");
		_output.WriteLine($"JavaHandlerSources={javaSources.Length}");
		_output.WriteLine($"UnresolvedJavaHandlerRegistrations={result.Unresolved.Count}");
		_output.WriteLine($"DistinctNpcIds={result.Sources.Select(source => source.NpcId).Distinct().Count()}");
		_output.WriteLine($"DistinctQuestIds={result.Sources.Select(source => source.QuestId).Distinct().Count()}");
		foreach (var unresolved in result.Unresolved.Take(10))
		{
			_output.WriteLine(
				$"UnresolvedSample={unresolved.SourcePath}:{unresolved.LineNumber} npc={unresolved.NpcExpression} quest={unresolved.QuestExpression} reason={unresolved.Reason}");
		}

		Assert.Equal(ExpectedTotalSources, result.Sources.Count);
		Assert.Equal(ExpectedXmlSources, xmlSources.Length);
		Assert.Equal(ExpectedJavaHandlerSources, javaSources.Length);
		Assert.Empty(result.Unresolved);
		Assert.Equal(ExpectedDistinctNpcIds, result.Sources.Select(source => source.NpcId).Distinct().Count());
		Assert.Equal(ExpectedDistinctQuestIds, result.Sources.Select(source => source.QuestId).Distinct().Count());
		Assert.All(result.Sources, source => Assert.True(source.NpcId > 0));
		Assert.All(result.Sources, source => Assert.True(source.QuestId > 0));
		Assert.DoesNotContain(
			result.Sources,
			source => source.SourceKind == QuestNpcStartRegistrationSourceKind.XmlQuest
				&& source.SourcePath.EndsWith("altgard.xml", StringComparison.Ordinal)
				&& source.NpcId == 203622
				&& source.QuestId == 2274);
	}

	[Fact]
	public void RealDataAudit_PopulatesStagedQuestNpcStartTableWithoutProductionWiring()
	{
		var result = LoadRealDataSources();
		var table = new QuestNpcStartTable();

		foreach (var source in result.Sources)
			table.RegisterOnQuestStart(source);

		var registeredQuestStartPairs = table.Registrations.Values.Sum(registration => registration.OnQuestStart.Count);
		_output.WriteLine($"TableSources={table.Sources.Count}");
		_output.WriteLine($"RegisteredNpcIds={table.Registrations.Count}");
		_output.WriteLine($"RegisteredQuestStartPairs={registeredQuestStartPairs}");
		_output.WriteLine($"LargestNpcQuestCount={table.Registrations.Values.Max(registration => registration.OnQuestStart.Count)}");

		Assert.Equal(ExpectedTotalSources, table.Sources.Count);
		Assert.Equal(ExpectedDistinctNpcIds, table.Registrations.Count);
		Assert.Equal(ExpectedRegisteredQuestStartPairs, registeredQuestStartPairs);
		Assert.Equal(ExpectedLargestNpcQuestCount, table.Registrations.Values.Max(registration => registration.OnQuestStart.Count));
		Assert.All(table.Registrations.Values, registration => Assert.True(registration.NpcId > 0));
		Assert.All(table.Registrations.Values, registration => Assert.Equal(QuestNpcStartRegistration.DefaultQuestRange, registration.QuestRange));
	}

	[Fact]
	public void RealDataAudit_ProjectsStagedQuestIdsIntoWorldInstanceWithoutRefreshWiring()
	{
		var result = LoadRealDataSources();
		var table = new QuestNpcStartTable();
		foreach (var source in result.Sources)
			table.RegisterOnQuestStart(source);
		var instance = new WorldMapInstanceRuntimeState(instanceId: 1);

		var projection = NearbyQuestCandidateProjectionService.ProjectNpcStartQuestIds(
			instance,
			table,
			table.Registrations.Keys);

		_output.WriteLine($"ProjectedNpcIds={projection.MatchedNpcIds.Count}");
		_output.WriteLine($"ProjectedQuestIds={projection.ProjectedQuestIds.Count}");
		_output.WriteLine($"NewlyRegisteredWorldQuestIds={projection.NewlyRegisteredQuestIds.Count}");
		_output.WriteLine($"WorldQuestIds={projection.WorldQuestIds.Count}");

		Assert.Equal(ExpectedDistinctNpcIds, projection.InspectedNpcIds.Count);
		Assert.Equal(ExpectedDistinctNpcIds, projection.MatchedNpcIds.Count);
		Assert.Equal(ExpectedProjectedWorldQuestIds, projection.ProjectedQuestIds.Count);
		Assert.Equal(ExpectedProjectedWorldQuestIds, projection.NewlyRegisteredQuestIds.Count);
		Assert.Equal(ExpectedProjectedWorldQuestIds, projection.WorldQuestIds.Count);
		Assert.Empty(instance.QuestIds.Except(projection.WorldQuestIds));
	}

	private static QuestNpcStartRegistrationSourceLoadResult LoadRealDataSources()
	{
		var repoRoot = FindRepoRoot();
		var questScriptDirectory = Path.Combine(repoRoot, "game-server", "data", "static_data", "quest_script_data");
		var javaHandlerDirectory = Path.Combine(repoRoot, "game-server", "data", "handlers", "quest");
		var loader = new QuestNpcStartRegistrationSourceLoader();
		return loader.Load(questScriptDirectory, javaHandlerDirectory);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (Directory.Exists(Path.Combine(directory.FullName, "game-server"))
				&& Directory.Exists(Path.Combine(directory.FullName, "dotnetConversion")))
				return directory.FullName;

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Unable to locate repository root.");
	}
}
