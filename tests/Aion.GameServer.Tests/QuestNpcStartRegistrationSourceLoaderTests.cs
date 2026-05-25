using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

public sealed class QuestNpcStartRegistrationSourceLoaderTests : IDisposable
{
	private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "aion-quest-start-loader-" + Guid.NewGuid().ToString("N"));

	[Fact]
	public void Load_ComposesXmlAndJavaHandlerExtractorOutputsInStableFileOrder()
	{
		var xmlDirectory = Path.Combine(_tempRoot, "quest_script_data");
		var javaDirectory = Path.Combine(_tempRoot, "handlers");
		Directory.CreateDirectory(xmlDirectory);
		Directory.CreateDirectory(javaDirectory);
		File.WriteAllText(Path.Combine(xmlDirectory, "b.xml"), """
			<quest_scripts>
				<report_to id="18506" start_npc_ids="799523" />
			</quest_scripts>
			""");
		File.WriteAllText(Path.Combine(xmlDirectory, "a.xml"), """
			<quest_scripts>
				<monster_hunt id="2561" start_npc_ids="204753 295178" />
			</quest_scripts>
			""");
		File.WriteAllText(Path.Combine(javaDirectory, "_30363FoolsRushIn.java"), """
			public class _30363FoolsRushIn extends AbstractQuestHandler {
				public _30363FoolsRushIn() {
					super(30363);
				}

				public void register() {
					qe.registerQuestNpc(278151).addOnQuestStart(questId);
				}
			}
			""");
		var loader = new QuestNpcStartRegistrationSourceLoader();

		var result = loader.Load(xmlDirectory, javaDirectory);

		Assert.Empty(result.Unresolved);
		Assert.Collection(
			result.Sources,
			source => AssertSource(source, 204753, 2561, QuestNpcStartRegistrationSourceKind.XmlQuest),
			source => AssertSource(source, 295178, 2561, QuestNpcStartRegistrationSourceKind.XmlQuest),
			source => AssertSource(source, 799523, 18506, QuestNpcStartRegistrationSourceKind.XmlQuest),
			source => AssertSource(source, 278151, 30363, QuestNpcStartRegistrationSourceKind.JavaHandler));
	}

	[Fact]
	public void Load_ReportsJavaHandlerUnresolvedRowsAlongsideResolvedSources()
	{
		var javaDirectory = Path.Combine(_tempRoot, "handlers");
		Directory.CreateDirectory(javaDirectory);
		File.WriteAllText(Path.Combine(javaDirectory, "mixed.java"), """
			public class Mixed extends AbstractQuestHandler {
				public Mixed() {
					super(30363);
				}

				public void register() {
					qe.registerQuestNpc(278151).addOnQuestStart(questId);
					qe.registerQuestNpc(npcIds[i]).addOnQuestStart(questId);
				}
			}
			""");
		var loader = new QuestNpcStartRegistrationSourceLoader();

		var result = loader.Load(null, javaDirectory);

		Assert.Collection(result.Sources, source => AssertSource(source, 278151, 30363, QuestNpcStartRegistrationSourceKind.JavaHandler));
		Assert.Collection(result.Unresolved, unresolved =>
		{
			Assert.Equal("npcIds[i]", unresolved.NpcExpression);
			Assert.Equal("questId", unresolved.QuestExpression);
			Assert.Contains("Unsupported expression", unresolved.Reason, StringComparison.Ordinal);
			Assert.Contains("mixed.java", unresolved.SourcePath, StringComparison.Ordinal);
		});
	}

	[Fact]
	public void Load_MissingDirectoriesReturnEmptyResult()
	{
		var loader = new QuestNpcStartRegistrationSourceLoader();

		var result = loader.Load(
			Path.Combine(_tempRoot, "missing-xml"),
			Path.Combine(_tempRoot, "missing-java"));

		Assert.Empty(result.Sources);
		Assert.Empty(result.Unresolved);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempRoot))
			Directory.Delete(_tempRoot, recursive: true);
	}

	private static void AssertSource(QuestNpcStartRegistrationSource source, int npcId, int questId, QuestNpcStartRegistrationSourceKind sourceKind)
	{
		Assert.Equal(npcId, source.NpcId);
		Assert.Equal(questId, source.QuestId);
		Assert.Equal(sourceKind, source.SourceKind);
		Assert.Equal(QuestNpcStartRegistration.DefaultQuestRange, source.QuestRange);
	}
}
