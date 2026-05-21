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
		Assert.Equal(300, staticData.TitleTemplates.Count);
		Assert.Equal(staticData.GetElementCount("recipe_template"), staticData.RecipeTemplates.Count);
		Assert.Equal(staticData.GetElementCount("random_bonus"), staticData.ItemRandomBonuses.Count);
		Assert.Equal(staticData.GetElementCount("itemset"), staticData.ItemSets.Count);
		Assert.Equal(staticData.GetElementCount("enchant_list"), staticData.EnchantTemplates.Count);
		Assert.Equal(staticData.GetElementCount("tempering_list"), staticData.TemperingTemplates.Count);
		Assert.Equal(staticData.GetElementCount("instance_cooltime"), staticData.InstanceCooltimes.Count);
		Assert.Equal("SWORD", staticData.ItemTemplates.GetItemTemplate(100000001)?.ItemGroup);
		Assert.Equal([37, 44], staticData.ItemTemplates.GetItemTemplate(100000001)?.RequiredEquipSkills);
		Assert.Equal(3, staticData.ItemTemplates.GetItemTemplate(100000094)?.ValidEquipmentSlots);
		Assert.Equal(188950002, staticData.ItemTemplates.GetItemTemplate(100000216)?.DispositionItemId);
		Assert.Equal(6, staticData.ItemTemplates.GetItemTemplate(100000216)?.DispositionItemCount);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169500916)?.IsClassSpecific("RANGER"));
		Assert.False(staticData.ItemTemplates.GetItemTemplate(169500916)?.IsClassSpecific("ASSASSIN"));
		Assert.Equal(25, staticData.ItemTemplates.GetItemTemplate(100001115)?.GetRequiredLevel("GLADIATOR"));
		Assert.Equal(39, staticData.ItemTemplates.GetItemTemplate(100001115)?.GetMaxLevelRestrict("GLADIATOR"));
		Assert.Equal("FEMALE", staticData.ItemTemplates.GetItemTemplate(110900040)?.GenderPermitted);
		Assert.Equal(155000001, staticData.ItemTemplates.GetItemTemplate(152200001)?.CraftLearnRecipeId);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(152000065)?.ActivationCount);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000895)?.ExpireTimeMinutes);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000714)?.EnchantType);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100001276)?.CanTune);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(100000001)?.CanTune);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100001105)?.ConditioningMaxLevel);
		var chargeTemplate = staticData.ItemTemplates.GetItemTemplate(100001105);
		Assert.NotNull(chargeTemplate);
		Assert.Equal(1, chargeTemplate.Improvement?.ChargeWay);
		Assert.Equal(1, chargeTemplate.Improvement?.Level);
		Assert.Equal(200, chargeTemplate.Improvement?.BurnAttack);
		Assert.Equal(100, chargeTemplate.Improvement?.BurnDefend);
		Assert.Equal(10000, chargeTemplate.Improvement?.Price1);
		Assert.Equal(0, chargeTemplate.Improvement?.Price2);
		Assert.Equal(4, chargeTemplate.RecommendRank);
		Assert.Equal(3, chargeTemplate.MinRank);
		Assert.Equal(18, chargeTemplate.MaxRank);
		var fireSword = staticData.ItemTemplates.GetItemTemplate(100000125);
		Assert.NotNull(fireSword);
		Assert.Equal("PHYSICAL", fireSword.AttackType);
		Assert.Equal(70, fireSword.WeaponStats?.MeanDamage);
		Assert.Equal(1400, fireSword.WeaponStats?.AttackSpeed);
		Assert.Equal(29, fireSword.IdianInfo?.BurnAttack);
		Assert.Equal(12, fireSword.IdianInfo?.BurnDefend);
		Assert.Contains(fireSword.StatModifiers, modifier => modifier is { Operation: "add", Name: "PHYSICAL_ATTACK", Value: 7, Bonus: true });
		var conditionedDagger = staticData.ItemTemplates.GetItemTemplate(100201371);
		Assert.NotNull(conditionedDagger);
		Assert.Contains(conditionedDagger.StatModifiers, modifier => modifier is { Operation: "rate", Name: "ATTACK_SPEED", Value: -4, Bonus: true, ChargeCondition: 1 });
		Assert.Equal("WEAPON_TEST", staticData.ItemTemplates.GetItemTemplate(100001673)?.EnchantName);
		Assert.Contains(staticData.EnchantTemplates.GetModifiers(fireSword, 2, 1), modifier => modifier is { Operation: "add", Name: "PHYSICAL_ATTACK", Value: 4, Bonus: false });
		var temperingTestEarring = staticData.ItemTemplates.GetItemTemplate(120001486);
		Assert.NotNull(temperingTestEarring);
		Assert.Equal("TEST_1", temperingTestEarring.TemperingName);
		Assert.Contains(staticData.TemperingTemplates.GetModifiers(temperingTestEarring, 2, 0), modifier => modifier is { Operation: "add", Name: "PHYSICAL_DEFENSE", Value: 10, Bonus: false });
		var physicalPlume = staticData.ItemTemplates.GetItemTemplate(187100011);
		Assert.NotNull(physicalPlume);
		Assert.Contains(staticData.TemperingTemplates.GetModifiers(physicalPlume, 3, 7), modifier => modifier is { Operation: "add", Name: "PHYSICAL_ATTACK", Value: 19, Bonus: true });
		Assert.Contains(staticData.TemperingTemplates.GetModifiers(physicalPlume, 3, 7), modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 450, Bonus: true });
		Assert.Equal(3, staticData.ItemTemplates.GetItemTemplate(166050001)?.PolishSetId);
		Assert.Contains(staticData.ItemRandomBonuses.GetModifiers("POLISH", 3, 1), modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 347, Bonus: true });
		Assert.Equal(1, staticData.ItemRandomBonuses.SelectRandomBonusNumber("POLISH", 3, () => 0));
		Assert.Equal(2, staticData.ItemTemplates.GetItemTemplate(168300003)?.ChargeActionMaxLevel);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(168300003)?.Improvement?.ChargeWay);
		var testGodstone = staticData.ItemTemplates.GetItemTemplate(168000001);
		Assert.NotNull(testGodstone);
		Assert.Equal(8255, testGodstone.GodstoneInfo?.SkillId);
		Assert.Equal(1, testGodstone.GodstoneInfo?.SkillLevel);
		Assert.Equal(1000, testGodstone.GodstoneInfo?.Probability);
		var hpManastone = staticData.ItemTemplates.GetItemTemplate(167000226);
		Assert.NotNull(hpManastone);
		Assert.Contains(hpManastone.StatModifiers, modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 20, Bonus: true });
		var randomBonusModifiers = staticData.ItemRandomBonuses.GetModifiers("INVENTORY", 1, 1);
		Assert.Contains(randomBonusModifiers, modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 100, Bonus: true });
		Assert.Contains(randomBonusModifiers, modifier => modifier is { Operation: "add", Name: "MAXMP", Value: -50, Bonus: true });
		var swordShieldSet = staticData.ItemSets.GetItemSetTemplate(2);
		Assert.NotNull(swordShieldSet);
		Assert.Same(swordShieldSet, staticData.ItemSets.GetItemSetTemplateByItemId(100000714));
		Assert.Contains(115000817, swordShieldSet.ItemIds);
		Assert.Contains(swordShieldSet.PartBonuses, bonus => bonus.Count == 2 && bonus.Modifiers.Any(modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 100, Bonus: true }));
		Assert.Contains(swordShieldSet.FullBonus!.Modifiers, modifier => modifier is { Operation: "add", Name: "MAXMP", Value: 100, Bonus: true });
		Assert.Equal("kamikaze worm", staticData.NpcTemplates.GetNpcTemplate(201000)?.Name);
		var postmanNpc = staticData.NpcTemplates.GetNpcTemplate(798100);
		Assert.NotNull(postmanNpc);
		Assert.Equal(2256, postmanNpc.MaxHp);
		Assert.Equal(4.23f, postmanNpc.RunSpeed);
		Assert.Equal(0.595f, postmanNpc.BoundRadius);
		Assert.Equal(8, staticData.SkillTemplates.GetSkillTemplatesByGroup("RA_WHITETIGER").Count);
		var clothMastery = staticData.SkillTemplates.GetSkillTemplate(40);
		Assert.NotNull(clothMastery);
		var armorMastery = Assert.Single(clothMastery.ArmorMastery);
		Assert.Equal("CLOTHES", armorMastery.ArmorType);
		Assert.Equal(1, armorMastery.Value);
		var armorChange = Assert.Single(armorMastery.Changes);
		Assert.Equal("PHYSICAL_DEFENSE", armorChange.Stat);
		Assert.Equal("PERCENT", armorChange.Func);
		Assert.Equal(10, armorChange.Value);
		var swordTraining = staticData.SkillTemplates.GetSkillTemplate(37);
		Assert.NotNull(swordTraining);
		var weaponMastery = Assert.Single(swordTraining.WeaponMastery);
		Assert.Equal("SWORD", weaponMastery.WeaponGroup);
		var weaponChange = Assert.Single(weaponMastery.Changes);
		Assert.Equal("PHYSICAL_ATTACK", weaponChange.Stat);
		Assert.Equal("PERCENT", weaponChange.Func);
		Assert.Equal(16, weaponChange.Value);
		var shieldTraining = staticData.SkillTemplates.GetSkillTemplate(50);
		Assert.NotNull(shieldTraining);
		var shieldMastery = Assert.Single(shieldTraining.ShieldMastery);
		var shieldChange = Assert.Single(shieldMastery.Changes);
		Assert.Equal("BLOCK", shieldChange.Stat);
		Assert.Equal("PERCENT", shieldChange.Func);
		Assert.Equal(5, shieldChange.Value);
		var dualWieldTraining = staticData.SkillTemplates.GetSkillTemplate(55);
		Assert.NotNull(dualWieldTraining);
		var weaponDual = Assert.Single(dualWieldTraining.WeaponDual);
		Assert.Equal(70, weaponDual.Value);
		Assert.Equal(0, weaponDual.Delta);
		Assert.Equal(40, weaponDual.SkillEfficiency);
		Assert.Equal(400, weaponDual.MaxDamageChance);
		Assert.Equal(0, weaponDual.MaxDamageDelta);
		var poetaProtector = staticData.TitleTemplates.GetTitleTemplate(1);
		Assert.NotNull(poetaProtector);
		Assert.Equal("ELYOS", poetaProtector.Race);
		Assert.Contains(poetaProtector.Modifiers, modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 20, Bonus: true });
		Assert.Contains(poetaProtector.Modifiers, modifier => modifier is { Operation: "add", Name: "PHYSICAL_DEFENSE", Value: 5, Bonus: true });
		Assert.Equal(152000401, staticData.RecipeTemplates.GetRecipeTemplateById(155000001)?.ProductId);
		Assert.True(staticData.HousingTemplates.AddressCount > 1000);
		Assert.Equal(9, staticData.HousingTemplates.BuildingCount);
		Assert.Equal(326001, staticData.HousingTemplates.GetAddress(6001)?.LandId);
		Assert.Equal(810018, staticData.HousingTemplates.GetAddress(6001)?.ManagerNpcId);
		Assert.Equal(0, staticData.HousingTemplates.GetAddress(6001)?.TownId);
		Assert.Equal(1001, staticData.HousingTemplates.GetAddress(10001)?.TownId);
		Assert.Equal(40, staticData.HousingTemplates.GetAddress(6001)?.MinLevel);
		Assert.Equal(4_000_000, staticData.HousingTemplates.GetAddress(6001)?.MaintenanceFee);
		Assert.Equal(4, staticData.HousingTemplates.GetHouseTypeId(350000));
		Assert.Equal(1, staticData.HousingTemplates.GetHouseTypeId(353000));
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
