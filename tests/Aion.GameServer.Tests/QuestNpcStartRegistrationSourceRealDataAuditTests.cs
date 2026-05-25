using Aion.GameServer.Dataholders;
using Xunit.Abstractions;

namespace Aion.GameServer.Tests;

public sealed class QuestNpcStartRegistrationSourceRealDataAuditTests
{
	private const int ExpectedTotalSources = 5214;
	private const int ExpectedXmlSources = 4400;
	private const int ExpectedJavaHandlerSources = 814;
	private const int ExpectedDistinctNpcIds = 1668;
	private const int ExpectedDistinctQuestIds = 4503;

	private readonly ITestOutputHelper _output;

	public QuestNpcStartRegistrationSourceRealDataAuditTests(ITestOutputHelper output)
	{
		_output = output;
	}

	[Fact]
	public void RealDataAudit_LoadsStagedQuestStartSourcesWithoutProductionWiring()
	{
		var repoRoot = FindRepoRoot();
		var questScriptDirectory = Path.Combine(repoRoot, "game-server", "data", "static_data", "quest_script_data");
		var javaHandlerDirectory = Path.Combine(repoRoot, "game-server", "data", "handlers", "quest");
		var loader = new QuestNpcStartRegistrationSourceLoader();

		var result = loader.Load(questScriptDirectory, javaHandlerDirectory);
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
