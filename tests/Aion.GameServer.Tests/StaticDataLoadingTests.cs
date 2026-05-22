using System.Xml.Linq;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Dataholders.LoadingUtils;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;

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
		Assert.Equal(staticData.GetElementCount("cosmetic_item"), staticData.CosmeticItems.Count);
		Assert.Equal(staticData.GetElementCount("npc_template"), staticData.NpcTemplates.Count);
		Assert.Equal(staticData.GetElementCount("skill_template"), staticData.SkillTemplates.Count);
		Assert.Equal(300, staticData.TitleTemplates.Count);
		Assert.Equal(staticData.GetElementCount("recipe_template"), staticData.RecipeTemplates.Count);
		Assert.Equal(staticData.GetElementCount("ride_info"), staticData.RideInfos.Count);
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
		Assert.True(staticData.ItemTemplates.GetItemTemplate(110900040)?.IsItemDyePermitted);
		Assert.Equal(155000001, staticData.ItemTemplates.GetItemTemplate(152200001)?.CraftLearnRecipeId);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(152000065)?.ActivationCount);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000895)?.ExpireTimeMinutes);
		Assert.Equal(21, staticData.ItemTemplates.GetItemTemplate(160000001)?.UseDelayId);
		Assert.Equal(5000, staticData.ItemTemplates.GetItemTemplate(160000001)?.UseDelayMillis);
		Assert.Equal(91, staticData.ItemTemplates.LearnableEmotionIds.Count);
		Assert.True(staticData.ItemTemplates.IsLearnableEmotion(64));
		Assert.True(staticData.ItemTemplates.IsLearnableEmotion(155));
		Assert.False(staticData.ItemTemplates.IsLearnableEmotion(140));
		Assert.Equal(64, staticData.ItemTemplates.GetItemTemplate(169600001)?.EmotionLearnId);
		Assert.Equal(0, staticData.ItemTemplates.GetItemTemplate(169600001)?.EmotionLearnMinutes);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169600001)?.HasEmotionLearnAction);
		Assert.Equal(64, staticData.ItemTemplates.GetItemTemplate(169600009)?.EmotionLearnId);
		Assert.Equal(5, staticData.ItemTemplates.GetItemTemplate(169600009)?.EmotionLearnMinutes);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169600009)?.HasEmotionLearnAction);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169945000)?.HasTitleAddAction);
		Assert.Equal(269, staticData.ItemTemplates.GetItemTemplate(169945000)?.TitleAddTitleId);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(169945000)?.HasTitleAddMinutes);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(169945001)?.HasTitleAddMinutes);
		Assert.Equal(10081, staticData.ItemTemplates.GetItemTemplate(169945001)?.TitleAddMinutes);
		Assert.Equal(new ItemSkillLearnActionInfo(1, 10, "RANGER"), staticData.ItemTemplates.GetItemTemplate(169500916)?.SkillLearnAction);
		Assert.Equal(new ItemExpandInventoryActionInfo(1, "CUBE"), staticData.ItemTemplates.GetItemTemplate(169630000)?.ExpandInventoryAction);
		Assert.Equal(new ItemExpandInventoryActionInfo(1, "WAREHOUSE"), staticData.ItemTemplates.GetItemTemplate(169640000)?.ExpandInventoryAction);
		Assert.Equal(staticData.GetElementCount("expextract"), staticData.ItemTemplates.Templates.Count(template => template.ExpExtractAction != null));
		Assert.Equal(new ItemExpExtractActionInfo(188052060, false, 33725505), staticData.ItemTemplates.GetItemTemplate(188920011)?.ExpExtractAction);
		Assert.Equal(new ItemExpExtractActionInfo(188052060, true, 100), staticData.ItemTemplates.GetItemTemplate(188920012)?.ExpExtractAction);
		Assert.Equal(staticData.GetElementCount("extract"), staticData.ItemTemplates.Templates.Count(template => template.HasExtractAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(165000001)?.HasExtractAction);
		Assert.Equal(staticData.GetElementCount("apextract"), staticData.ItemTemplates.Templates.Count(template => template.ApExtractAction != null));
		Assert.Equal(new ItemApExtractActionInfo(0.2f, "WEAPON"), staticData.ItemTemplates.GetItemTemplate(165005000)?.ApExtractAction);
		Assert.Equal(new ItemApExtractActionInfo(0.5f, "ARMOR"), staticData.ItemTemplates.GetItemTemplate(165005001)?.ApExtractAction);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100000363)?.CanApExtract);
		Assert.Equal(4900, staticData.ItemTemplates.GetItemTemplate(100000363)?.RequiredAbyssPoints);
		Assert.Equal(new ItemDyeActionInfo(null, 0, false), staticData.ItemTemplates.GetItemTemplate(169100000)?.DyeAction);
		Assert.Equal(new ItemDyeActionInfo(0xc22626, 0, false), staticData.ItemTemplates.GetItemTemplate(169120000)?.DyeAction);
		Assert.Equal(new ItemAnimationActionInfo(1, 2, 3, 4, null, 60), staticData.ItemTemplates.GetItemTemplate(188500000)?.AnimationAction);
		Assert.Equal("cash_hair_type_li_m_01a", staticData.ItemTemplates.GetItemTemplate(169800003)?.CosmeticActionName);
		Assert.Equal("test_preset_type_li_m_01a", staticData.ItemTemplates.GetItemTemplate(169890001)?.CosmeticActionName);
		Assert.Equal(new ItemRemodelActionInfo(1, 0), staticData.ItemTemplates.GetItemTemplate(122001250)?.RemodelAction);
		Assert.Equal(staticData.GetElementCount("houseobject"), staticData.ItemTemplates.Templates.Count(template => template.HasHouseObjectAction));
		Assert.Equal(3000001, staticData.ItemTemplates.GetItemTemplate(170000000)?.HouseObjectTemplateId);
		Assert.Equal(staticData.GetElementCount("housedeco"), staticData.ItemTemplates.Templates.Count(template => template.HasHouseDecorateAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(170000023)?.HasHouseDecorateAction);
		Assert.Equal(0, staticData.ItemTemplates.GetItemTemplate(170000023)?.HouseDecorateTemplateId);
		Assert.Equal(3550000, staticData.ItemTemplates.GetItemTemplate(171000000)?.HouseDecorateTemplateId);
		Assert.Equal(2, staticData.ItemTemplates.GetItemTemplate(122001250)?.ExtraInventoryId);
		Assert.Equal(-1, staticData.ItemTemplates.GetItemTemplate(152000065)?.ExtraInventoryId);
		Assert.Equal(staticData.GetElementCount("decompose"), staticData.ItemTemplates.Templates.Count(template => template.HasDecomposeAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(152000065)?.HasDecomposeAction);
		Assert.Equal(staticData.GetElementCount("composition"), staticData.ItemTemplates.Templates.Count(template => template.HasCompositionAction));
		Assert.True(staticData.ItemTemplates.GetItemTemplate(165010000)?.HasCompositionAction);
		Assert.Equal(staticData.GetElementCount("decomposable"), staticData.DecomposableItems.Count);
		Assert.True(staticData.DecomposableItems.NormalCount > staticData.DecomposableItems.SelectableCount);
		var pepentoRewards = staticData.DecomposableItems.GetInfoByItemId(152000065);
		Assert.NotNull(pepentoRewards);
		var pepentoGroup = Assert.Single(pepentoRewards);
		Assert.Equal(100f, pepentoGroup.Chance);
		Assert.Equal(0, pepentoGroup.MinLevel);
		Assert.Equal(99, pepentoGroup.MaxLevel);
		var pepentoItem = Assert.Single(pepentoGroup.Items);
		Assert.Equal(152000064, pepentoItem.ItemId);
		Assert.Equal(2, pepentoItem.MinCount);
		Assert.Equal(2, pepentoItem.MaxCount);
		Assert.Equal("PC_ALL", pepentoItem.Race);
		Assert.Empty(pepentoItem.PlayerClasses);
		var selectableRewards = staticData.DecomposableItems.GetSelectableItems(188051090);
		Assert.NotNull(selectableRewards);
		Assert.Contains(selectableRewards, item => item.ItemId == 125045164 && item.MinCount == 1 && item.MaxCount == 1);
		Assert.Contains(selectableRewards, item => item.ItemId == 188053609 && item.MinCount == 3 && item.MaxCount == 3);
		Assert.Null(staticData.DecomposableItems.GetInfoByItemId(188051090));
		var levelGatedRewards = staticData.DecomposableItems.GetInfoByItemId(188051162);
		Assert.NotNull(levelGatedRewards);
		Assert.Contains(
			levelGatedRewards,
			group => group is { Chance: 88f, MinLevel: 1, MaxLevel: 20 }
				&& group.Items.Any(item => item is { ItemId: 186000001, MinCount: 2, MaxCount: 3, Race: "ELYOS" }));
		var classRestrictedRewards = staticData.DecomposableItems.GetInfoByItemId(188051413);
		Assert.NotNull(classRestrictedRewards);
		Assert.Contains(
			classRestrictedRewards.SelectMany(group => group.Items),
			item => item.ItemId == 113600836 && item.HasClassRestrictions && item.PlayerClasses.SetEquals(["GLADIATOR", "TEMPLAR"]));
		var randomRewards = staticData.DecomposableItems.GetInfoByItemId(188050584);
		Assert.NotNull(randomRewards);
		Assert.Contains(
			randomRewards.SelectMany(group => group.RandomItems),
			item => item is { Type: "ENCHANTMENT", MinCount: 1, MaxCount: 3 });
		Assert.Equal(89, staticData.AssemblyItems.Count);
		Assert.Equal(staticData.GetElementCount("assemble"), staticData.ItemTemplates.Templates.Count(template => template.AssemblyItemId != 0));
		var assemblyItem = staticData.AssemblyItems.GetAssemblyItem(186000018);
		Assert.NotNull(assemblyItem);
		Assert.Equal([188100001, 188100002, 188100003, 188100004, 188100005], assemblyItem.Parts);
		Assert.Equal(186000018, staticData.ItemTemplates.GetItemTemplate(188100001)?.AssemblyItemId);
		Assert.Null(staticData.AssemblyItems.GetAssemblyItem(188100001));
		var hairCosmetic = staticData.CosmeticItems.GetCosmeticItemTemplate("cash_hair_type_li_m_01a");
		Assert.NotNull(hairCosmetic);
		Assert.Equal("hair_type", hairCosmetic.Type);
		Assert.Equal(0, hairCosmetic.Id);
		Assert.Equal("ELYOS", hairCosmetic.Race);
		Assert.Equal("MALE", hairCosmetic.GenderPermitted);
		var presetCosmetic = staticData.CosmeticItems.GetCosmeticItemTemplate("test_preset_type_li_m_01a");
		Assert.NotNull(presetCosmetic);
		Assert.Equal("preset_name", presetCosmetic.Type);
		Assert.Equal(1.0f, presetCosmetic.Preset?.Scale);
		Assert.Equal(1, presetCosmetic.Preset?.HairType);
		Assert.Equal(0, presetCosmetic.Preset?.FaceType);
		Assert.Equal(1515812, presetCosmetic.Preset?.HairColor);
		Assert.Equal(5402006, presetCosmetic.Preset?.EyeColor);
		Assert.Equal(13228789, presetCosmetic.Preset?.SkinColor);
		var sprintRide = staticData.RideInfos.GetRideInfo(2000000);
		Assert.NotNull(sprintRide);
		Assert.Equal(12.0f, sprintRide.MoveSpeed);
		Assert.Equal(16.0f, sprintRide.FlySpeed);
		Assert.Equal(15.0f, sprintRide.SprintSpeed);
		Assert.Equal(10, sprintRide.StartFp);
		Assert.Equal(10, sprintRide.CostFp);
		Assert.True(sprintRide.CanSprint());
		Assert.False(staticData.RideInfos.GetRideInfo(2000010)?.CanSprint());
		Assert.Equal(staticData.GetElementCount("ride"), staticData.ItemTemplates.Templates.Count(template => template.RideNpcId != 0));
		Assert.Equal(2000000, staticData.ItemTemplates.GetItemTemplate(190100000)?.RideNpcId);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100000714)?.EnchantType);
		Assert.Equal(15, staticData.ItemTemplates.GetItemTemplate(100100860)?.MaxEnchantLevel);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100100860)?.CanExceedEnchant);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(100000001)?.CanExceedEnchant);
		Assert.Equal("RANK1_SET2_PHYSICAL_WEAPON", staticData.ItemTemplates.GetItemTemplate(100000216)?.ExceedEnchantSkill);
		Assert.Equal(6, staticData.ItemTemplates.GetItemTemplate(100001384)?.ManastoneSlots);
		Assert.Equal(2, staticData.ItemTemplates.GetItemTemplate(100001384)?.SpecialManastoneSlots);
		Assert.True(staticData.ItemTemplates.GetItemTemplate(100001276)?.CanTune);
		Assert.Equal(1, staticData.ItemTemplates.GetItemTemplate(100001276)?.MaxTuneCount);
		Assert.False(staticData.ItemTemplates.GetItemTemplate(100000001)?.CanTune);
		Assert.Equal(0, staticData.ItemTemplates.GetItemTemplate(100000001)?.MaxTuneCount);
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
		var stigmaStone = staticData.ItemTemplates.GetItemTemplate(140001107);
		Assert.NotNull(stigmaStone);
		Assert.Equal(["FI_WHIRLDRAIN", "FI_WHIRLTORNADO"], stigmaStone.StigmaInfo?.GainSkillGroups);
		Assert.True(stigmaStone.StigmaInfo?.Chargeable);
		var testGodstone = staticData.ItemTemplates.GetItemTemplate(168000001);
		Assert.NotNull(testGodstone);
		Assert.Equal(8255, testGodstone.GodstoneInfo?.SkillId);
		Assert.Equal(1, testGodstone.GodstoneInfo?.SkillLevel);
		Assert.Equal(1000, testGodstone.GodstoneInfo?.Probability);
		var hpManastone = staticData.ItemTemplates.GetItemTemplate(167000226);
		Assert.NotNull(hpManastone);
		Assert.Contains(hpManastone.StatModifiers, modifier => modifier is { Operation: "add", Name: "MAXHP", Value: 20, Bonus: true });
		Assert.Equal(1, hpManastone.EnchantAction?.Count);
		var assuredSupplement = staticData.ItemTemplates.GetItemTemplate(166150017);
		Assert.NotNull(assuredSupplement);
		Assert.Equal(100f, assuredSupplement.EnchantAction?.Chance);
		Assert.Equal(1, assuredSupplement.EnchantAction?.MinLevel);
		Assert.Equal(65, assuredSupplement.EnchantAction?.MaxLevel);
		Assert.True(assuredSupplement.EnchantAction?.ManastoneOnly);
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
		var exhaustingWave = staticData.SkillTemplates.GetSkillTemplate(539);
		Assert.NotNull(exhaustingWave);
		Assert.Equal("ADVANCED", exhaustingWave.StigmaType);
		Assert.True(exhaustingWave.IsStigmaSkill);
		Assert.Contains(staticData.SkillTree.GetTemplatesForSkill(539, "GLADIATOR", "ELYOS"), skill => skill.Stigma == 2);
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
		Assert.Equal(210040000, staticData.HousingTemplates.GetAddress(6001)?.MapId);
		Assert.Equal(2668.545166f, staticData.HousingTemplates.GetAddress(6001)?.X);
		Assert.Equal(645.303955f, staticData.HousingTemplates.GetAddress(6001)?.Y);
		Assert.Equal(355.70212f, staticData.HousingTemplates.GetAddress(6001)?.Z);
		Assert.Equal(351000, staticData.HousingTemplates.GetAddress(6001)?.DefaultBuildingId);
		Assert.Equal("PERSONAL_FIELD", staticData.HousingTemplates.GetAddress(6001)?.DefaultBuildingType);
		Assert.Equal(1001, staticData.HousingTemplates.GetAddress(10001)?.TownId);
		var studioAddress = staticData.HousingTemplates.GetAddress(2001);
		Assert.NotNull(studioAddress);
		Assert.Equal(720010000, studioAddress.MapId);
		Assert.Equal(355000, studioAddress.DefaultBuildingId);
		Assert.Equal("PERSONAL_INS", studioAddress.DefaultBuildingType);
		Assert.Equal(700010000, studioAddress.ExitMapId);
		Assert.Equal(2573.0f, studioAddress.ExitX);
		Assert.Equal(1961.0f, studioAddress.ExitY);
		Assert.Equal(185.0f, studioAddress.ExitZ);
		Assert.Equal(40, staticData.HousingTemplates.GetAddress(6001)?.MinLevel);
		Assert.Equal(4_000_000, staticData.HousingTemplates.GetAddress(6001)?.MaintenanceFee);
		Assert.Equal(4, staticData.HousingTemplates.GetHouseTypeId(350000));
		Assert.Equal(1, staticData.HousingTemplates.GetHouseTypeId(353000));
		Assert.Equal(276, staticData.HousingTemplates.PartCount);
		Assert.Equal("CP_C", staticData.HousingTemplates.GetBuilding(353000)?.PartsMatch);
		Assert.True(staticData.HousingTemplates.IsPartValidForBuilding(3520000, 353000));
		Assert.False(staticData.HousingTemplates.IsPartValidForBuilding(3500000, 353000));
		var houseDefaultDecor = staticData.HousingTemplates.GetDefaultDecorIds(353000);
		Assert.Equal(19, houseDefaultDecor.Count);
		Assert.Equal(3520000, houseDefaultDecor[0]);
		Assert.Equal(3521000, houseDefaultDecor[1]);
		Assert.Equal(3522001, houseDefaultDecor[2]);
		Assert.Equal(3523000, houseDefaultDecor[3]);
		Assert.Equal(3526000, houseDefaultDecor[4]);
		Assert.Equal(3527000, houseDefaultDecor[5]);
		Assert.All(houseDefaultDecor.Skip(6).Take(6), partId => Assert.Equal(3524000, partId));
		Assert.All(houseDefaultDecor.Skip(12).Take(6), partId => Assert.Equal(3525000, partId));
		Assert.Equal(0, houseDefaultDecor[18]);
		Assert.Equal(
			[3520000, 3521000, 3522001, 3523000, 3526000, 3527000, 3524000, 3525000],
			staticData.HousingTemplates.GetDefaultPartIds(353000));
		Assert.Equal(1511, staticData.HousingObjectTemplates.Count);
		var chairObject = staticData.HousingObjectTemplates.GetTemplate(3000004);
		Assert.NotNull(chairObject);
		Assert.Equal((byte)5, chairObject.TypeId);
		Assert.Equal("chair", chairObject.Kind);
		Assert.Equal("INTERIOR", chairObject.Area);
		Assert.Equal("FLOOR", chairObject.Location);
		Assert.Equal("CHAIR", chairObject.Category);
		Assert.Equal(1, chairObject.UseDays);
		Assert.True(chairObject.CanDye);
		var storageObject = staticData.HousingObjectTemplates.GetTemplate(3000007);
		Assert.NotNull(storageObject);
		Assert.Equal((byte)2, storageObject.TypeId);
		Assert.Equal(1, storageObject.WarehouseId);
		Assert.Equal("STORAGE", storageObject.Limit);
		Assert.Equal(360007, storageObject.NameId);
		Assert.Equal(5.0f, storageObject.TalkingDistance);
		var npcObject = staticData.HousingObjectTemplates.GetTemplate(3001000);
		Assert.NotNull(npcObject);
		Assert.Equal((byte)7, npcObject.TypeId);
		Assert.Equal(810013, npcObject.NpcId);
		Assert.Equal(30, npcObject.UseDays);
		var useObject = staticData.HousingObjectTemplates.GetTemplate(3190001);
		Assert.NotNull(useObject);
		Assert.Equal((byte)1, useObject.TypeId);
		Assert.True(useObject.OwnerOnly);
		Assert.Equal(3000, useObject.DelayMilliseconds);
		Assert.Equal(2.0f, useObject.TalkingDistance);
		Assert.Equal(186000166, useObject.RequiredItemId);
		Assert.Equal(2, useObject.UseActionCheckType);
		Assert.Equal(1, useObject.UseActionRemoveCount);
		Assert.Equal(188051519, useObject.UseActionRewardId);
		var finalRewardUseObject = staticData.HousingObjectTemplates.GetTemplate(3190013);
		Assert.NotNull(finalRewardUseObject);
		Assert.Equal(188051562, finalRewardUseObject.UseActionRewardId);
		Assert.Equal(188051555, finalRewardUseObject.UseActionFinalRewardId);
		Assert.Equal(5, staticData.InstanceCooltimes.GetInstanceCooltimeByWorldId(300030000)?.MaxCount);
		Assert.Contains(staticData.RecipeTemplates.GetAutolearnRecipes("ELYOS", 40009, 1), recipe => recipe.RecipeId == 155000001);
		var craftPlayer = new Player
		{
			Name = "Kahrun",
			Race = "ELYOS",
			Skills = [new PlayerSkill { SkillId = 40009, SkillLevel = 1 }],
		};
		Assert.True(CraftLearnService.ValidateNewRecipe(craftPlayer, 155000001, staticData).Succeeded);
		craftPlayer.Recipes = [155000001];
		Assert.Equal(CraftLearnFailure.AlreadyKnown, CraftLearnService.ValidateNewRecipe(craftPlayer, 155000001, staticData).Failure);
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
