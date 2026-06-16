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
		Assert.True(sd.NpcDataDh.Size() > 0, "NpcDataDh empty after boot");
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

		// TRADE_LIST_DATA: faithful TradeListData loads npc_trade_list.xml at boot.
		Assert.True(sd.TradeListDataDh.Size() > 0, "TradeListDataDh empty after boot");
		var tl = sd.TradeListDataDh.GetTradeListTemplate(203060);
		Assert.NotNull(tl);
		Assert.Equal(4, tl!.GetTradeTablist().Count);

		// PET_DATA: faithful PetData loads pets/pets.xml at boot.
		Assert.True(sd.PetDataDh.Size() > 0, "PetDataDh empty after boot");
		var pet900000 = sd.PetDataDh.GetPetTemplate(900000);
		Assert.NotNull(pet900000);
		Assert.Equal("siberian wild tiger (purebred)", pet900000!.GetName());

		// NPC_SKILL_DATA: faithful NpcSkillData merges npc_skills/ recursively at boot.
		Assert.True(sd.NpcSkillDataDh.Size() > 0, "NpcSkillDataDh empty after boot");
		var nsk = sd.NpcSkillDataDh.GetNpcSkillList(201010);
		Assert.NotNull(nsk);
		Assert.Contains(nsk!.GetNpcSkills()!, s => s.GetSkillId() == 17424);

		// WALKER_DATA: faithful WalkerData merges npc_walker/ recursively at boot (route-step indexing applied).
		Assert.True(sd.WalkerDataDh.Size() > 0, "WalkerDataDh empty after boot");
		var walker = sd.WalkerDataDh.GetWalkerTemplate("0222C904738D487BE4D4F76C9DC76E63A8FD1EB9");
		Assert.NotNull(walker);
		Assert.True(walker!.GetRouteSteps()[walker.GetRouteSteps().Count - 1].IsLastStep());

		var absStats1 = sd.AbsoluteStatsDataDh.GetTemplate(1);
		Assert.NotNull(absStats1);
		Assert.True(absStats1!.GetModifiers().Count > 1, "AbsoluteStats modifiers dropped at boot");
		Assert.Contains(absStats1.GetModifiers(),
			m => m.GetName() == Model.Stats.Container.StatEnum.POWER && m.GetValue() == 1 && !m.IsBonus());

		Assert.True(sd.TribeRelations.Size() > 0, "TribeRelations empty after boot");
		Assert.Equal(Model.TribeClass.MONSTER, sd.TribeRelations.GetBaseTribe(Model.TribeClass.BROWNIEGUARD));

		Assert.True(sd.NpcShouts.Size() > 0, "NpcShouts empty after boot");
		Assert.Contains(sd.NpcShouts.GetNpcShouts(0, 210308)!, s => s.GetStringId() == 340662);

		Assert.Equal(6, sd.UpgradeArcade.GetMinResumableLevel());
		Assert.True(sd.UpgradeArcade.Size() > 0, "UpgradeArcade rewards empty after boot");

		Assert.True(sd.SiegeLocations.Size() > 0, "SiegeLocations empty after boot");
		Assert.Equal(400010000, sd.SiegeLocations.GetSiegeLocations()[1011].GetWorldId());

		Assert.True(sd.ItemGroups.BonusSize() > 0, "ItemGroups empty after boot");
		Assert.True(sd.ItemGroups.PetFoodSize() > 0, "ItemGroups pet food empty after boot");

		Assert.True(sd.Materials.Size() > 0, "Materials empty after boot");
		Assert.True(sd.Materials.IsMaterialSkill(8269));

		Assert.True(sd.SkillCharges.Size() > 0, "SkillCharges empty after boot");
		Assert.Equal(400, sd.SkillCharges.GetChargedSkillEntry(1)!.GetMinTime());

		Assert.True(sd.GuideHtml.Size() > 0, "GuideHtml empty after boot");
		Assert.Equal(3, sd.GuideHtml.GetTemplateByTitle("adventurers_guide3_asmo")!.GetLevel());

		Assert.True(sd.InstanceCooltimeDataDh.Size() > 0, "InstanceCooltimeData empty after boot");
		Assert.Equal(5, sd.InstanceCooltimeDataDh.GetInstanceCooltimeByWorldId(310090000)!.GetMaxCount());

		Assert.True(sd.HouseBuildings.Size() > 0, "HouseBuildings empty after boot");
		Assert.Equal(Model.Templates.Housing.HouseType.PALACE, sd.HouseBuildings.GetBuilding(350000)!.GetSize());

		Assert.True(sd.Motions.Size() > 0, "Motions empty after boot");
		Assert.NotNull(sd.Motions.GetMotionTime("20ebuff1"));

		Assert.True(sd.ZoneInfo.Size() > 0, "ZoneInfo empty after boot");
		Assert.True(sd.ZoneInfo.GetZones().ContainsKey(110010000), "ZoneInfo missing map 110010000 after boot");

		Assert.True(sd.SystemMailTemplates.Size() > 0, "SystemMailTemplates empty after boot");
		Assert.NotNull(sd.SystemMailTemplates.GetMailTemplate("$$ABYSS_REWARD_MAIL", "", Model.Race.ELYOS));

		// TOWN_SPAWNS_DATA boot-wiring: the town_spawns/ dir merge loads (Spawn/SpawnSpotTemplate nullable attrs bind
		// via string proxies); a known town spawn (map 700010000 / town 1001 / level 1 / npc 831222) resolves.
		Assert.True(sd.TownSpawns.GetSpawnsCount() > 0, "TownSpawns empty after boot");
		Assert.Equal(700010000, sd.TownSpawns.GetWorldIdForTown(1001));
		var townSpawns = sd.TownSpawns.GetSpawns(1001, 1);
		Assert.NotNull(townSpawns);
		Assert.Contains(townSpawns!, s => s.GetNpcId() == 831222 && s.GetRespawnTime() == 295);

		// EVENT_DATA boot-wiring: the events/timed_events/ dir merge loads (custom + retail). The EventTemplate
		// GlobalRule drop-rule cone binds nullable restriction_race via a string proxy; a known event + drop rule
		// (present AND absent restriction_race) resolves intact.
		Assert.True(sd.Events.Size() > 0, "Events empty after boot");
		var solorius = sd.Events.GetEvents().First(e => e.GetName() == "Celebrate Solorius");
		Assert.Equal(Model.EventTheme.CHRISTMAS, solorius.GetTheme());
		var rules = solorius.GetEventDropRules();
		Assert.NotNull(rules);
		// Absent restriction_race -> null; present "ELYOS" -> typed enum (proves the string proxy both ways).
		Assert.Contains(rules!, r => r.GetMemberLimit() == 12 && r.GetRestrictionRace() == null);
		Assert.Contains(rules!, r => r.GetRestrictionRace() == Model.Templates.GlobalDrops.GlobalRule.RestrictionRace.ELYOS);

		// GLOBAL_DROP_DATA boot-wiring: the faithful GlobalDropData holder is loaded from the merged
		// global_drops/rules dir (singleRootTag) and GLOBAL_DROP_DATA.processRules(NPC_DATA) ran after NPC load,
		// expanding gd_npc_names -> resolved gd_npc ids (Java parity: DataManager.init()). At least one rule that
		// declared <gd_npc_names> must now have a resolved <gd_npcs> set with names cleared (proves ProcessRules ran).
		Assert.True(sd.GlobalDropDataDh.Size() > 0, "GlobalDropDataDh empty after boot");
		var allGlobalRules = sd.GlobalDropDataDh.GetAllRules();
		Assert.Contains(allGlobalRules, r =>
			r.GetGlobalRuleNpcs() != null
			&& r.GetGlobalRuleNpcs()!.GetGlobalDropNpcs().Count > 0
			&& (r.GetGlobalRuleNpcNames() == null || r.GetGlobalRuleNpcNames()!.GetGlobalDropNpcNames().Count == 0));

		// GLOBAL_EXCLUSION_DATA boot-wiring: the faithful GlobalNpcExclusionData holder is loaded from the single
		// global_drops/global_npc_exclusions.xml (Java parity: DataManager.init() binds data.globalExclusionData).
		// Previously hollow new() -> always empty -> DropRegistrationService.HasGlobalNpcExclusions always false.
		// Assert known exclusions from the XML resolved (npc id 219501, type GENERAL, tribe PET, abyss type DOOR)
		// and the afterUnmarshal isEmpty flag is false (proves the @XmlList Set proxies bound + callback fired).
		Assert.False(sd.GlobalNpcExclusionDataDh.IsEmpty(), "GlobalNpcExclusionDataDh empty after boot");
		Assert.Contains(219501, sd.GlobalNpcExclusionDataDh.GetNpcIds());
		Assert.Contains(Model.Templates.Npc.NpcTemplateType.GENERAL, sd.GlobalNpcExclusionDataDh.GetNpcTemplateTypes());
		Assert.Contains(Model.TribeClass.PET, sd.GlobalNpcExclusionDataDh.GetNpcTribes());
		Assert.Contains(Model.Templates.Npc.AbyssNpcType.DOOR, sd.GlobalNpcExclusionDataDh.GetNpcAbyssTypes());

		// instance_exit/instance_exit.xml: faithful InstanceExitData (Java parity DataManager.init binds
		// data.instanceExitData). Previously hollow new() -> always empty -> TeleportService instance-exit lookup
		// returned null. A known row (instance 300110000, exit_world 400010000) resolves per-race (race enum bound
		// by member name) and the computeIfAbsent worldId index fired in AfterUnmarshal.
		Assert.True(sd.InstanceExitDataDh.Size() > 0, "InstanceExitDataDh empty after boot");
		var elyExit = sd.InstanceExitDataDh.GetInstanceExit(300110000, Model.Race.ELYOS);
		Assert.NotNull(elyExit);
		Assert.Equal(400010000, elyExit!.GetExitWorld());
		Assert.Equal(Model.Race.ELYOS, elyExit.GetRace());

		// staticdoors/staticdoor_templates.xml: faithful StaticDoorData (Java parity DataManager.init binds
		// data.staticDoorData). Previously hollow new() -> always empty -> StaticDoorSpawnManager spawned no doors.
		// A known world (300040000) and its door (staticId 33, state 2) resolve; proves the parent AfterUnmarshal
		// cascaded each StaticDoorWorld.AfterUnmarshal children-first (the per-staticId index built).
		Assert.True(sd.StaticDoorDataDh.Size() > 0, "StaticDoorDataDh empty after boot");
		var door33 = sd.StaticDoorDataDh.GetStaticDoor(300040000, 33);
		Assert.NotNull(door33);
		Assert.Equal(2, door33!.GetState());

		// skill_tree dir (skill_tree.xml + craft_skill_tree.xml, singleRootTag): faithful SkillTreeData (Java parity
		// DataManager.init binds data.skillTreeData). Previously hollow new() -> always empty -> StigmaService/
		// SkillLearnService/PlayerSkillList saw no learnable skills. Proves the merged AfterUnmarshal built the
		// per-class/race templates map (GetTemplatesFor) + templatesById (IsLearnedSkill): skill id 1 is a RANGER
		// ELYOS skill learnable at minLevel 10 (skill_tree.xml row <skill skillId="1" minLevel="10" race="ELYOS" classId="RANGER"/>).
		Assert.True(sd.SkillTreeDataDh.Size() > 0, "SkillTreeDataDh empty after boot");
		Assert.True(sd.SkillTreeDataDh.IsLearnedSkill(1), "SkillTreeData missing known skill id 1");
		var rangerLvl10 = sd.SkillTreeDataDh.GetTemplatesFor(Model.PlayerClass.RANGER, 10, Model.Race.ELYOS);
		Assert.Contains(rangerLvl10, t => t.GetSkillId() == 1);

		// decomposable_items/decomposable_items.xml: faithful DecomposableItemsData (Java parity DataManager.init binds
		// data.decomposableItemsData). Previously hollow new() -> always empty -> decompose granted nothing. Known
		// non-selectable box item_id 152000065 (Juicy Pepento) decomposes to item 152000064 (Pepento, min_count 2);
		// proves the children-first AfterUnmarshal cascade ran (ResultedItem validated vs ITEM_DATA, indexed).
		Assert.True(sd.DecomposableItemsDataDh.Size() > 0, "DecomposableItemsDataDh empty after boot");
		var pepento = sd.DecomposableItemsDataDh.GetInfoByItemId(152000065);
		Assert.NotNull(pepento);
		Assert.Contains(pepento!.SelectMany(c => c.GetItems()), r => r.GetItemId() == 152000064);

		// quest_data/challenge_tasks.xml: faithful ChallengeData (Java parity DataManager.init binds data.challengeData).
		// Previously hollow new() -> always empty -> ChallengeTaskService had no tasks. Known task id 300 (LEGION/ELYOS)
		// resolves and its quest id 17000 maps back to task 300; proves the quest cone bound + AfterUnmarshal indexed.
		Assert.True(sd.ChallengeDataDh.Size() > 0, "ChallengeDataDh empty after boot");
		var task300 = sd.ChallengeDataDh.GetTaskByTaskId(300);
		Assert.NotNull(task300);
		Assert.Equal(Model.Templates.Challenge.ChallengeType.LEGION, task300!.GetType_());
		Assert.Equal(300, sd.ChallengeDataDh.GetTaskByQuestId(17000)!.GetId());

		// storage_expander/warehouse_expander.xml + cube_expander.xml: faithful StorageExpansionTemplate index.
		Assert.True(sd.WarehouseExpandDataDh.Size() > 0, "WarehouseExpandDataDh empty after boot");
		Assert.Equal(24000, sd.WarehouseExpandDataDh.GetWarehouseExpansionTemplate(203221)!.GetPrice(2));
		Assert.True(sd.CubeExpandDataDh.Size() > 0, "CubeExpandDataDh empty after boot");
		Assert.Equal(1000, sd.CubeExpandDataDh.GetCubeExpansionTemplate(798008)!.GetPrice(1));

		// pets/pet_feed.xml: faithful PetFeedData (FoodType enum wire-name binding + nested result cone).
		Assert.True(sd.PetFeedDataDh.Size() > 0, "PetFeedDataDh empty after boot");
		Assert.Equal(100, sd.PetFeedDataDh.GetFlavourById(7)!.GetFullCount());

		// world_maps.xml: faithful WorldMapsData (flags wire-token proxy + world_type enum).
		Assert.True(sd.WorldMaps2.Size() > 0, "WorldMaps2 empty after boot");
		var sanctumMap = sd.WorldMaps2.GetTemplate(110010000);
		Assert.NotNull(sanctumMap);
		Assert.Equal(World.WorldType.ELYSEA, sanctumMap!.GetWorldType());
		Assert.True(sanctumMap.IsPvpAllowed());

		// enchants/tempering_templates.xml: faithful TemperingData (Java parity DataManager.init binds data.temperingData).
		// Previously hollow new() -> always empty -> TemperingEffect granted no stats. The item_group -> level -> stats map
		// built in AfterUnmarshal; Size() = number of item_group rows (TEST_1, etc.). Java exposes only Size() +
		// GetTemplates(ItemTemplate), so the by-group lookup is proven via a real item that carries a tempering group.
		Assert.True(sd.TemperingDataDh.Size() > 0, "TemperingDataDh empty after boot");

		// portals/portal_template2.xml: faithful Portal2Data (Java parity DataManager.init binds data.portalTemplate2).
		// Previously hollow new() -> always empty -> PortalAI/TeleportService never teleported. A known portal_use npc
		// (730357, Dredgion Captains Cabin ELYOS) is indexed (IsPortalNpc) and its portal_path resolved race + loc_id.
		Assert.True(sd.Portal2DataDh.Size() > 0, "Portal2DataDh empty after boot");
		Assert.True(sd.Portal2DataDh.IsPortalNpc(730357), "Portal2Data missing known portal_use npc 730357");

		// items/item_random_bonuses.xml: faithful ItemRandomBonusData (Java parity DataManager.init binds
		// data.itemRandomBonuses). Previously hollow new() -> always empty -> ItemPurificationService/PolishAction/
		// TuningAction/RandomBonusEffect rolled nothing. Known set (INVENTORY id 1) has its first <modifiers> (chance 50)
		// bound with the polymorphic <add MAXHP> StatFunction (proves the shared ModifiersTemplate cone bound).
		Assert.True(sd.ItemRandomBonusDataDh.Size() > 0, "ItemRandomBonusDataDh empty after boot");
		var bonus1Mods = sd.ItemRandomBonusDataDh.GetTemplate(Model.Templates.Items.Bonuses.StatBonusType.INVENTORY, 1, 1);
		Assert.NotNull(bonus1Mods);
		Assert.Equal(50.0f, bonus1Mods!.GetChance());
		Assert.Contains(bonus1Mods.GetModifiers(), m => m.GetName() == Model.Stats.Container.StatEnum.MAXHP);

		// quest_script_data/ merged dir: faithful XMLQuests (16-subtype polymorphic DSL). XML_QUESTS non-empty at
		// boot and a known scripted quest resolves to its correct subtype (poeta.xml xml_quest 1127 -> XmlQuestData).
		Assert.True(sd.XmlQuests.GetAllQuests().Count > 0, "XmlQuests empty after boot");
		var scriptedQuest = sd.XmlQuests.GetQuest(1127);
		Assert.NotNull(scriptedQuest);
		Assert.IsType<QuestEngine.Handlers.Models.XmlQuestData>(scriptedQuest);
		Assert.IsType<QuestEngine.Handlers.Models.MonsterHuntData>(sd.XmlQuests.GetQuest(1102));
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
