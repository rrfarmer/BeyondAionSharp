using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;

namespace Aion.GameServer.Tests;

public sealed class StaticDataLoadingTests
{
	[Fact]
	public void XmlMerger_MergesSingleRootDirectoryAndReusesCache()
	{
		using var temp = TempDirectory.Create();
		var dataDirectory = Directory.CreateDirectory(Path.Combine(temp.Path, "data", "static_data"));
		var itemsDirectory = Directory.CreateDirectory(Path.Combine(dataDirectory.FullName, "items"));
		File.WriteAllText(
			Path.Combine(dataDirectory.FullName, "static_data.xml"),
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<import file="items" singleRootTag="true" />
			</static_data>
			""");
		File.WriteAllText(Path.Combine(itemsDirectory.FullName, "a.xml"), """<items><item id="1" /></items>""");
		File.WriteAllText(Path.Combine(itemsDirectory.FullName, "b.xml"), """<items><item id="2" /></items>""");

		var cacheFile = Path.Combine(temp.Path, "cache", "static_data.xml");
		var merger = new XmlMerger(Path.Combine(dataDirectory.FullName, "static_data.xml"), cacheFile);

		var firstMerge = merger.Merge();
		var secondMerge = merger.Merge();
		var document = XDocument.Load(cacheFile);

		Assert.True(firstMerge.FileWasModified);
		Assert.False(secondMerge.FileWasModified);
		Assert.True(File.Exists(cacheFile + ".properties"));
		Assert.Equal(2, firstMerge.ImportedFiles.Count);
		Assert.Equal(2, document.Descendants("item").Count());
		Assert.Single(document.Root!.Elements("items"));
		Assert.Empty(document.Descendants("import"));
	}

	[Fact]
	public async Task XmlDataLoader_RunsAsyncValidationForModifiedCache()
	{
		using var temp = TempDirectory.Create();
		var dataDirectory = Directory.CreateDirectory(Path.Combine(temp.Path, "data", "static_data"));
		var itemsDirectory = Directory.CreateDirectory(Path.Combine(dataDirectory.FullName, "items"));
		File.WriteAllText(
			Path.Combine(dataDirectory.FullName, "static_data.xml"),
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<static_data>
				<import file="items/items.xml" />
			</static_data>
			""");
		File.WriteAllText(Path.Combine(itemsDirectory.FullName, "items.xml"), """<items><item id="1" /></items>""");
		File.WriteAllText(
			Path.Combine(dataDirectory.FullName, "static_data.xsd"),
			"""
			<?xml version="1.0" encoding="UTF-8"?>
			<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
				<xs:element name="static_data">
					<xs:complexType>
						<xs:sequence>
							<xs:element name="items">
								<xs:complexType>
									<xs:sequence>
										<xs:element name="item" maxOccurs="unbounded">
											<xs:complexType>
												<xs:attribute name="id" type="xs:int" use="required" />
											</xs:complexType>
										</xs:element>
									</xs:sequence>
								</xs:complexType>
							</xs:element>
						</xs:sequence>
					</xs:complexType>
				</xs:element>
			</xs:schema>
			""");

		var staticData = await XmlDataLoader.LoadStaticDataAsync(
			new XmlDataLoaderOptions
			{
				MainXmlFilePath = Path.Combine(dataDirectory.FullName, "static_data.xml"),
				CacheXmlFilePath = Path.Combine(temp.Path, "cache", "static_data.xml"),
				SchemaFilePath = Path.Combine(dataDirectory.FullName, "static_data.xsd"),
				ValidateWhenCacheChanges = true,
			});

		Assert.NotNull(staticData.ValidationTask);
		await staticData.ValidationTask;
		Assert.Equal(1, staticData.GetElementCount("item"));
		Assert.Equal(1, staticData.ImportedFileCount);
	}

	[Fact]
	public async Task DataManager_LoadsRealJavaStaticDataManifestCounts()
	{
		using var temp = TempDirectory.Create();
		var repoRoot = FindRepoRoot();

		var manager = await DataManager.LoadAsync(
			repoRoot,
			cacheDirectory: temp.Path,
			validateWhenCacheChanges: false);
		var staticData = manager.StaticData;

		Assert.True(File.Exists(Path.Combine(temp.Path, "static_data.xml")));
		Assert.True(File.Exists(Path.Combine(temp.Path, "static_data.xml.properties")));
		Assert.True(staticData.ImportedFileCount > 600);
		Assert.Equal(102009, staticData.GetElementCount("item_template"));
		Assert.Equal(63287, staticData.GetElementCount("npc_template"));
		Assert.Equal(13570, staticData.GetElementCount("skill_template"));
		Assert.True(staticData.GetElementCount("quest") > 8000);
		Assert.Equal(12494, staticData.GetElementCount("recipe_template"));
		Assert.Equal(staticData.GetElementCount("item_template"), staticData.ItemTemplates.Count);
		Assert.Equal(staticData.GetElementCount("npc_template"), staticData.NpcTemplates.Count);
		Assert.Equal(staticData.GetElementCount("skill_template"), staticData.SkillTemplates.Count);
		Assert.Equal(staticData.GetElementCount("recipe_template"), staticData.RecipeTemplates.Count);
		Assert.Equal(staticData.GetElementCount("instance_cooltime"), staticData.InstanceCooltimes.Count);
		Assert.Equal("SWORD", staticData.ItemTemplates.GetItemTemplate(100000001)?.ItemGroup);
		Assert.Equal(3, staticData.ItemTemplates.GetItemTemplate(100000094)?.ValidEquipmentSlots);
		Assert.Equal(188950002, staticData.ItemTemplates.GetItemTemplate(100000216)?.DispositionItemId);
		Assert.Equal(6, staticData.ItemTemplates.GetItemTemplate(100000216)?.DispositionItemCount);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169500916)?.IsClassSpecific("RANGER"));
		Assert.False(staticData.ItemTemplates.GetItemTemplate(169500916)?.IsClassSpecific("ASSASSIN"));
		Assert.Equal(155000001, staticData.ItemTemplates.GetItemTemplate(152200001)?.CraftLearnRecipeId);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(152000065)?.ActivationCount);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000895)?.ExpireTimeMinutes);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000714)?.EnchantType);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100001276)?.CanTune);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(100000001)?.CanTune);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100001105)?.ConditioningMaxLevel);
		Assert.Equal("kamikaze worm", staticData.NpcTemplates.GetNpcTemplate(201000)?.Name);
		var postmanNpc = staticData.NpcTemplates.GetNpcTemplate(798100);
		Assert.NotNull(postmanNpc);
		Assert.Equal(2256, postmanNpc.MaxHp);
		Assert.Equal(4.23f, postmanNpc.RunSpeed);
		Assert.Equal(0.595f, postmanNpc.BoundRadius);
		Assert.Equal(8, staticData.SkillTemplates.GetSkillTemplatesByGroup("RA_WHITETIGER").Count);
		Assert.Equal(152000401, staticData.RecipeTemplates.GetRecipeTemplateById(155000001)?.ProductId);
		Assert.Equal(5, staticData.InstanceCooltimes.GetInstanceCooltimeByWorldId(300030000)?.MaxCount);
		Assert.Contains(staticData.RecipeTemplates.GetAutolearnRecipes("ELYOS", 40009, 1), recipe => recipe.RecipeId == 155000001);
		Assert.Equal(6, staticData.PlayerInitialData.Count);
		Assert.Equal(210010000, staticData.PlayerInitialData.GetSpawnLocation("ELYOS")?.MapId);
		Assert.Equal(220010000, staticData.PlayerInitialData.GetSpawnLocation("ASMODIANS")?.MapId);
		Assert.Contains(staticData.PlayerInitialData.GetPlayerCreationData("WARRIOR")!.Items, item => item.ItemId == 100000094 && item.Count == 1);
		Assert.Contains(staticData.SkillTree.GetAutoLearnSkills("WARRIOR", "ELYOS", 1, 1), skill => skill.SkillId == 37 && skill.SkillLevel > 0);
		Assert.Equal(staticData.GetElementCount("map"), staticData.WorldMaps.Count);
		Assert.True(staticData.PlayerExperienceTable.MaxLevel > 60, $"MaxLevel={staticData.PlayerExperienceTable.MaxLevel}");
		Assert.Equal(0, staticData.PlayerExperienceTable.GetStartExpForLevel(1));
		Assert.Equal(11, staticData.PlayerExperienceTable.GetLevelForExp(182252));
		Assert.Contains(new Aion.GameServer.Dataholders.WorldMapSummary(210010000, IsInstance: false, TwinCount: 5), staticData.WorldMaps);
		Assert.Contains(new Aion.GameServer.Dataholders.WorldMapSummary(300030000, IsInstance: true, TwinCount: 0), staticData.WorldMaps);
		Assert.Contains("item_templates", staticData.TopLevelElements);
		Assert.DoesNotContain("import", staticData.TopLevelElements);
	}

	private static string FindRepoRoot()
	{
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not find repository root from test output directory.");
	}

	private sealed class TempDirectory : IDisposable
	{
		private TempDirectory(string path)
		{
			Path = path;
		}

		public string Path { get; }

		public static TempDirectory Create()
		{
			var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aion-static-data-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return new TempDirectory(path);
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path, recursive: true);
			}
			catch
			{
			}
		}
	}
}
