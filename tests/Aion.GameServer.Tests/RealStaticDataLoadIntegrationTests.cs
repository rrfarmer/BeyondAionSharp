using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Tests;

/// <summary>
/// Integration check for the "reuse the Java XML in place" principle: load the REAL
/// game-server static data (the 147 MB Java-generated <c>game-server/cache/static_data.xml</c>
/// plus the <c>game-server/data/static_data</c> tree) through the production
/// <see cref="DataManager.LoadAsync(string, string?, bool, Microsoft.Extensions.Logging.ILogger?, System.Threading.CancellationToken)"/>
/// path and assert it actually parses into non-empty tables. This is the linchpin of Front-A
/// (server boot): if the C# cannot consume Java's data as-is, nothing runs. NOT a unit test —
/// it reads the real on-disk data, so it is skipped (not failed) when that data is absent.
/// </summary>
public sealed class RealStaticDataLoadIntegrationTests
{
	[Fact]
	public async Task LoadAsync_ParsesRealJavaStaticDataCache_IntoNonEmptyTables()
	{
		var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
		var cacheFile = repoRoot is null
			? null
			: Path.Combine(repoRoot, "game-server", "cache", "static_data.xml");
		if (cacheFile is null || !File.Exists(cacheFile))
			return; // Real game-server/cache/static_data.xml not present; skip the reuse-in-place integration check.

		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
		// validateWhenCacheChanges:false — just parse the cache; we are proving the parse, not re-validating against source.
		var dataManager = await DataManager.LoadAsync(
			repoRoot!,
			cacheDirectory: null,
			validateWhenCacheChanges: false,
			logger: null,
			cancellationToken: cts.Token);

		var sd = dataManager.StaticData;

		// The merged cache must have parsed real content (these are the highest-traffic gameplay tables).
		Assert.True(sd.ImportedFileCount > 0, "no source files imported");
		Assert.True(sd.ItemTemplates.Count > 0, $"ItemTemplates empty (GetElementCount('item')={sd.GetElementCount("item")})");
		Assert.True(sd.NpcTemplates.Count > 0, "NpcTemplates empty");
		Assert.True(sd.WorldMaps.Count > 0, "WorldMaps empty");

		// Boot-wiring: the proven faithful per-feature leaf holders (model B) are now populated from their
		// per-feature XML during LoadAsync, so the DataManager.*_DATA accessors (which delegate to these *Dh
		// slots) return real data at runtime. Assert on the loaded StaticData directly to avoid depending on
		// the DataManager singleton bridge (RegisterInstance) / cross-test contamination.
		Assert.True(sd.BindPointDataDh.Size() > 0, "BindPointDataDh empty after boot");
		Assert.True(sd.ChestDataDh.Size() > 0, "ChestDataDh empty after boot");
		Assert.True(sd.CuringObjectsDataDh.Size() > 0, "CuringObjectsDataDh empty after boot");
		Assert.True(sd.RoadDataDh.Size() > 0, "RoadDataDh empty after boot");
		Assert.True(sd.HotspotDataDh.Size() > 0, "HotspotDataDh empty after boot");
		Assert.True(sd.MapWeathers.Size() > 0, "MapWeathers empty after boot");
		Assert.True(sd.KillBountyDataDh.Size() > 0, "KillBountyDataDh empty after boot");
		Assert.True(sd.BaseDataDh.Size() > 0, "BaseDataDh empty after boot");
		Assert.True(sd.LegionDominionDataDh.Size() > 0, "LegionDominionDataDh empty after boot");
		Assert.True(sd.GatherableDataDh.Size() > 0, "GatherableDataDh empty after boot");
		Assert.True(sd.MultiReturnItemDataDh.Size() > 0, "MultiReturnItemDataDh empty after boot");
		Assert.True(sd.FlyRingDataDh.Size() > 0, "FlyRingDataDh empty after boot");
		Assert.True(sd.WindstreamDataDh.Size() > 0, "WindstreamDataDh empty after boot");
		Assert.True(sd.TeleLocationDataDh.Size() > 0, "TeleLocationDataDh empty after boot");
		Assert.True(sd.PetDopingDataDh.Size() > 0, "PetDopingDataDh empty after boot");
		Assert.True(sd.FlyPathDataDh.Size() > 0, "FlyPathDataDh empty after boot");
		Assert.True(sd.ShieldDataDh.Size() > 0, "ShieldDataDh empty after boot");
		Assert.True(sd.PortalLocDataDh.Size() > 0, "PortalLocDataDh empty after boot");
		Assert.True(sd.SkillAliasLocationDataDh.Size() > 0, "SkillAliasLocationDataDh empty after boot");
		Assert.True(sd.InstanceBuffDataDh.Size() > 0, "InstanceBuffDataDh empty after boot");
		Assert.True(sd.HouseNpcsDataDh.Size() > 0, "HouseNpcsDataDh empty after boot");
		Assert.True(sd.CosmeticItemsDataDh.Size() > 0, "CosmeticItemsDataDh empty after boot");
		Assert.True(sd.AssembledNpcsDataDh.Size() > 0, "AssembledNpcsDataDh empty after boot");
		Assert.True(sd.SignetDataTemplatesDh.Size() > 0, "SignetDataTemplatesDh empty after boot");
		Assert.True(sd.ItemPurificationDataDh.Size() > 0, "ItemPurificationDataDh empty after boot");
		Assert.True(sd.PanelSkillsDataDh.Size() > 0, "PanelSkillsDataDh empty after boot");
		Assert.True(sd.RideDataDh.Size() > 0, "RideDataDh empty after boot");
		Assert.True(sd.WorldRaidDataDh.Size() > 0, "WorldRaidDataDh empty after boot");
		Assert.True(sd.GoodsListDataDh.Size() > 0, "GoodsListDataDh empty after boot");
		Assert.True(sd.NpcFactionsDataDh.Size() > 0, "NpcFactionsDataDh empty after boot");
		Assert.True(sd.TeleporterDataDh.Size() > 0, "TeleporterDataDh empty after boot");
		Assert.True(sd.HousePartsDataDh.Size() > 0, "HousePartsDataDh empty after boot");
		Assert.True(sd.ItemRestrictionCleanupDataDh.Size() > 0, "ItemRestrictionCleanupDataDh empty after boot");
		Assert.True(sd.AssemblyItemsDataDh.Size() > 0, "AssemblyItemsDataDh empty after boot");
		Assert.True(sd.AtreianPassportDataDh.Size() > 0, "AtreianPassportDataDh empty after boot");
		Assert.True(sd.AbsoluteStatsDataDh.Size() > 0, "AbsoluteStatsDataDh empty after boot");
		Assert.True(sd.ItemSetDataDh.Size() > 0, "ItemSetDataDh empty after boot");
		Assert.True(sd.TitleDataDh.Size() > 0, "TitleDataDh empty after boot");
		Assert.True(sd.ConquerorAndProtectorDataDh.Size() > 0, "ConquerorAndProtectorDataDh empty after boot");
		Assert.True(sd.VortexDataDh.Size() > 0, "VortexDataDh empty after boot");
		Assert.True(sd.NpcDataDh.Size() > 0, "NpcDataDh empty after boot");

		// NPC_DATA boot-wiring: a known named NPC (Sage Fasimedes, npc_id=203700) must load with key fields intact.
		var fasimedes = sd.NpcDataDh.GetNpcTemplate(203700);
		Assert.NotNull(fasimedes);
		Assert.Equal("fasimedes", fasimedes!.GetName());
		Assert.Equal((byte)60, fasimedes.GetLevel());
		Assert.Equal(Model.TribeClass.GUARD, fasimedes.GetTribe());
		Assert.Equal(Model.Race.ELYOS, fasimedes.GetRace());
		Assert.Equal(23691, fasimedes.GetStatsTemplate().GetMaxHp());

		// ITEM_DATA boot-wiring: the ~65MB item_templates.xml must load into a non-empty ItemData holder.
		Assert.True(sd.ItemDataDh.Size() > 0, "ItemDataDh empty after boot");

		// Known weapon (Fire Sword, id=100000125) must load with key fields + its <modifiers>/<add> intact.
		var fireSword = sd.ItemDataDh.GetItemTemplate(100000125);
		Assert.NotNull(fireSword);
		Assert.Equal("Fire Sword", fireSword!.GetName());
		Assert.Equal(23, fireSword.GetLevel());
		Assert.Equal(Model.Templates.Items.ItemQuality.COMMON, fireSword.GetItemQuality());
		Assert.Equal(Model.Templates.Items.Enums.ItemGroup.SWORD, fireSword.GetItemGroup());
		Assert.Contains(fireSword.GetModifiers(),
			m => m.GetName() == Model.Stats.Container.StatEnum.PHYSICAL_ATTACK && m.GetValue() == 7);

		// NPC->ITEM linkage lights up: fasimedes' first equipment id now resolves to a real ItemTemplate
		// in the freshly-loaded ItemData (the lazy IDREF resolution NpcEquippedGear.Init performs at runtime).
		var firstEquipId = fasimedes.equipmentList!.ItemIds[0];
		Assert.NotNull(sd.ItemDataDh.GetItemTemplate(firstEquipId));

		// SKILL_DATA: the faithful SkillData holder loads the real ~12MB skill_templates.xml at boot.
		Assert.True(sd.SkillDataDh.Size() > 0, "SkillDataDh empty after boot");
		// Known skill (Transformation: White Tiger, skill_id=1) with its polymorphic effect/condition subtree intact.
		var skill1 = sd.SkillDataDh.GetSkillTemplate(1);
		Assert.NotNull(skill1);
		Assert.Equal("Transformation: White Tiger", skill1!.GetName());
		Assert.Equal(SkillEngine.Model.SkillType.MAGICAL, skill1.GetTypeValue());
		// <effects> not dropped: first effect is the polymorphic <shapechange> -> ShapeChangeEffect, and the
		// Effects.AfterUnmarshal effectTypes set was built (children-first) during the holder's AfterUnmarshal.
		var skill1Effects = skill1.GetEffects();
		Assert.NotNull(skill1Effects);
		Assert.IsType<SkillEngine.Effects.ShapeChangeEffect>(skill1Effects!.GetEffects()[0]);
		Assert.True(skill1Effects.HasAnyEffectType(SkillEngine.Effects.EffectType.SHAPECHANGE));
		// <startconditions> polymorphic <dp> -> DpCondition not dropped.
		Assert.IsType<SkillEngine.Condition.DpCondition>(skill1.GetStartconditions()!.GetConditions()[0]);

		// Prove the stat-bearing leaf holders bound their modifiers at boot (not silently dropped):
		// item set 1 fullbonus carries a SPEED rate; title 1 carries a MAXHP add; conqueror rank 1 a PVP_ATTACK_RATIO add.
		var itemSet1 = sd.ItemSetDataDh.GetItemSetTemplate(1);
		Assert.NotNull(itemSet1);
		Assert.Contains(itemSet1!.GetFullbonus().GetModifiers(),
			m => m.GetName() == Model.Stats.Container.StatEnum.SPEED);
		var title1 = sd.TitleDataDh.GetTitleTemplate(1);
		Assert.NotNull(title1);
		Assert.Contains(title1!.GetModifiers(),
			m => m.GetName() == Model.Stats.Container.StatEnum.MAXHP);
		var conq1 = sd.ConquerorAndProtectorDataDh.GetRank(Model.Templates.Cp.CPType.CONQUEROR, 1);
		Assert.NotNull(conq1);
		Assert.Contains(conq1!.GetStatModifiers(),
			m => m.GetName() == Model.Stats.Container.StatEnum.PVP_ATTACK_RATIO);

		// Prove the StatFunction polymorphic modifiers actually bound (not silently dropped) at boot:
		// stats_set id=1 must carry a non-empty <modifiers> list with the known POWER/1 abs row.
		// QUEST_DATA: the faithful QuestsData holder loads the real ~82k-line quest_data.xml at boot
		// (feeds the 1025 ported quest handlers). Known quest 1001 (The Kerubim Threat) with nested data intact.
		Assert.True(sd.Quests.Size() > 0, "Quests empty after boot");
		var quest1001 = sd.Quests.GetQuestById(1001);
		Assert.NotNull(quest1001);
		Assert.Equal("The Kerubim Threat", quest1001!.GetName());
		Assert.Equal(Model.Templates.Quest.QuestCategory.MISSION, quest1001.GetCategory());
		Assert.Equal(Model.Race.ELYOS, quest1001.GetRacePermitted());
		Assert.Equal(182200001, quest1001.GetCollectItems().GetCollectItem()[0].GetItemId());
		Assert.Equal(2100, Assert.Single(quest1001.GetRewards()).GetExp());

		// AI_DATA: the faithful AIData holder merges every ai/*.xml at boot (feeds the 462 ported AI handlers
		// + SummonerAI). Known bombs row (npcId=281327) and summons row (npcId=212145) with nested data intact.
		Assert.True(sd.AiDataDh.Size() > 0, "AiDataDh empty after boot");
		var aiBomb = sd.AiDataDh.GetAiTemplate(281327);
		Assert.NotNull(aiBomb);
		Assert.Equal(16559, aiBomb!.GetBombs().GetBombTemplate().GetSkillId());
		var aiSummon = sd.AiDataDh.GetAiTemplate(212145);
		Assert.NotNull(aiSummon);
		Assert.Equal(280747, aiSummon!.GetSummons().GetPercentage()[0].GetSummons()[0].GetNpcId());

		// AUTO_GROUP: faithful AutoGroupData loads auto_group/auto_group.xml at boot.
		Assert.True(sd.AutoGroupDataDh.Size() > 0, "AutoGroupDataDh empty after boot");
		var ag1 = sd.AutoGroupDataDh.GetTemplateByInstanceMaskId(1);
		Assert.NotNull(ag1);
		Assert.Equal(300110000, ag1!.GetInstanceMapId());
		Assert.Contains(279055, ag1.GetNpcIds());

		// RECIPE_DATA: faithful RecipeData loads recipe/recipe_templates.xml at boot.
		Assert.True(sd.RecipeDataDh.Size() > 0, "RecipeDataDh empty after boot");
		var recipe1 = sd.RecipeDataDh.GetRecipeTemplateById(155000001);
		Assert.NotNull(recipe1);
		Assert.Equal(152000401, recipe1!.GetProductId());
		Assert.Equal(152000901, recipe1.GetComponents()[0].GetComponent()[0].GetItemId());

		var absStats1 = sd.AbsoluteStatsDataDh.GetTemplate(1);
		Assert.NotNull(absStats1);
		Assert.True(absStats1!.GetModifiers().Count > 1, "AbsoluteStats modifiers dropped at boot");
		Assert.Contains(absStats1.GetModifiers(),
			m => m.GetName() == Model.Stats.Container.StatEnum.POWER && m.GetValue() == 1 && !m.IsBonus());
	}

	private static string? FindRepoRoot(string startDirectory)
	{
		var directory = new DirectoryInfo(startDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "game-server", "data", "static_data", "static_data.xml")))
				return directory.FullName;
			directory = directory.Parent;
		}

		return null;
	}
}
