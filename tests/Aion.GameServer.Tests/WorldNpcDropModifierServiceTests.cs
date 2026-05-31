using Aion.GameServer.Configuration;
using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;
using Aion.GameServer.Services;
using Aion.GameServer.World;

namespace Aion.GameServer.Tests;

public sealed class WorldNpcDropModifierServiceTests
{
	[Theory]
	[InlineData(-11, 0)]
	[InlineData(-10, 0)]
	[InlineData(-9, 40)]
	[InlineData(-8, 60)]
	[InlineData(-7, 70)]
	[InlineData(-6, 80)]
	[InlineData(-5, 100)]
	[InlineData(0, 100)]
	public void GetDropRewardPercent_MatchesJavaDropRewardEnum(int levelDifference, int expectedPercent)
	{
		Assert.Equal(expectedPercent, WorldNpcDropModifierService.GetDropRewardPercent(levelDifference));
	}

	[Theory]
	[InlineData(10, 20, 0f)]
	[InlineData(11, 20, 0.4f)]
	[InlineData(12, 20, 0.6f)]
	[InlineData(13, 20, 0.7f)]
	[InlineData(14, 20, 0.8f)]
	[InlineData(15, 20, null)]
	public void GetReductionDropRate_MatchesJavaDropRewardReduction(int npcLevel, int highestLevel, float? expectedRate)
	{
		Assert.Equal(expectedRate, WorldNpcDropModifierService.GetReductionDropRate(npcLevel, highestLevel));
	}

	[Fact]
	public void CreateModifiers_UsesLooterRaceAndNpcLevelReduction()
	{
		var service = new WorldNpcDropModifierService();
		var npc = CreateNpc(level: 12);
		var looter = new Player { ObjectId = 1001, Race = "ASMODIANS", Level = 20 };

		var modifiers = service.CreateModifiers(npc, looter, boostDropRate: 1.5f);

		Assert.Equal("ASMODIANS", modifiers.DropRace);
		Assert.Equal(1.5f, modifiers.BoostDropRate);
		Assert.Equal(0.6f, modifiers.ReductionDropRate);
		Assert.Equal(45f, modifiers.CalculateDropChance(50f, allowReductionDropRate: true), precision: 3);
		Assert.Equal(75f, modifiers.CalculateDropChance(50f, allowReductionDropRate: false), precision: 3);
	}

	[Fact]
	public void CreateModifiers_UsesResolvedBoostRateContextWhenProvided()
	{
		var service = new WorldNpcDropModifierService();
		var npc = CreateNpc(level: 20);
		var looter = new Player { ObjectId = 1001, Race = "ELYOS", Level = 20 };
		var context = new WorldNpcDropBoostRateContext(
			ConfiguredDropRate: 2f,
			NpcBoostDropRate: 120,
			KillerBoostDropRate: 130,
			KillerDrBoost: 80,
			HasReposeEnergy: true,
			HasSalvation: true,
			HasActivePalace: true);

		var modifiers = service.CreateModifiers(npc, looter, boostDropRate: 9f, boostRateContext: context);

		Assert.Equal("ELYOS", modifiers.DropRace);
		Assert.Equal(1.9f, modifiers.BoostDropRate, precision: 3);
		Assert.Null(modifiers.ReductionDropRate);
	}

	[Theory]
	[InlineData(1f, 100, null, null, false, false, false, 1f)]
	[InlineData(1.5f, 120, null, null, false, false, false, 1.8f)]
	[InlineData(1f, 120, 130, null, false, false, false, 1.3f)]
	[InlineData(1f, 120, 130, 80, false, false, false, 0.8f)]
	[InlineData(2f, 100, null, null, true, true, true, 2.3f)]
	public void CalculateBoostDropRate_MatchesJavaStatDefaultChainAndBonusStack(
		float configuredDropRate,
		int npcBoostDropRate,
		int? killerBoostDropRate,
		int? killerDrBoost,
		bool hasReposeEnergy,
		bool hasSalvation,
		bool hasActivePalace,
		float expected)
	{
		Assert.Equal(
			expected,
			WorldNpcDropModifierService.CalculateBoostDropRate(
				configuredDropRate,
				npcBoostDropRate,
				killerBoostDropRate,
				killerDrBoost,
				hasReposeEnergy,
				hasSalvation,
				hasActivePalace),
			precision: 3);
	}

	[Fact]
	public void DropBoostRateContext_CalculatesJavaBoostRateFromResolvedInputs()
	{
		var context = new WorldNpcDropBoostRateContext(
			ConfiguredDropRate: 1.5f,
			NpcBoostDropRate: 110,
			KillerBoostDropRate: 115,
			HasReposeEnergy: true);

		Assert.Equal(1.8f, context.CalculateBoostDropRate(), precision: 3);
	}

	[Fact]
	public void CreateDisabledPlan_ResolvesModeledRateAndReposeButBlocksMissingLiveSources()
	{
		var looter = new Player
		{
			ObjectId = 1001,
			AccountMembership = 7,
			ReposeEnergy = 25,
		};

		var plan = WorldNpcDropBoostRateContextPlanService.CreateDisabledPlan(
			looter,
			[1f, 2f, 3f]);

		Assert.Equal(WorldNpcDropBoostRateContextPlanStatus.Blocked, plan.Status);
		Assert.False(plan.IsReadyForWorkflow);
		Assert.Equal(3f, plan.ConfiguredDropRate);
		Assert.True(plan.HasReposeEnergy);
		Assert.NotNull(plan.Context);
		Assert.Equal(3.15f, plan.Context.CalculateBoostDropRate(), precision: 3);
		Assert.Contains("npc BOOST_DROP_RATE stat source", plan.MissingInputs);
		Assert.Contains("killer salvation percent source", plan.MissingInputs);
		Assert.Contains("active palace source", plan.MissingInputs);
		Assert.Contains("DropRegistrationService.calculateBoostDropRate", plan.JavaSource, StringComparison.Ordinal);
	}

	[Fact]
	public void CreateDisabledPlan_MarksReadyOnlyWhenAllLiveSourcesAreExplicit()
	{
		var looter = new Player { ObjectId = 1001, AccountMembership = 1 };

		var plan = WorldNpcDropBoostRateContextPlanService.CreateDisabledPlan(
			looter,
			[1f, 2f],
			hasNpcBoostStatSource: true,
			hasKillerBoostStatSource: true,
			hasKillerDrBoostStatSource: true,
			hasSalvationSource: true,
			hasActivePalaceSource: true);

		Assert.Equal(WorldNpcDropBoostRateContextPlanStatus.Ready, plan.Status);
		Assert.True(plan.IsReadyForWorkflow);
		Assert.Empty(plan.MissingInputs);
		Assert.Equal(2f, plan.ConfiguredDropRate);
		Assert.False(plan.HasReposeEnergy);
	}

	[Fact]
	public void FindActiveHouse_ReturnsFirstNonInactiveLoadedHouse()
	{
		var activeHouse = new PlayerHouse(51, 700200, 350000, DateTime.UtcNow, null, IsInactive: false);
		var player = new Player
		{
			ObjectId = 1001,
			Houses =
			[
				new PlayerHouse(50, 700100, 353000, DateTime.UtcNow, null, IsInactive: true),
				activeHouse,
				new PlayerHouse(52, 700300, 355000, DateTime.UtcNow, null, IsInactive: false),
			],
		};

		Assert.Same(activeHouse, PlayerActiveHouseResolverService.FindActiveHouse(player));
	}

	[Fact]
	public void CreateDisabledPlan_ConsumesResolvedActivePalaceSource()
	{
		var looter = new Player
		{
			ObjectId = 1001,
			AccountMembership = 0,
			Houses =
			[
				new PlayerHouse(50, 700100, 353000, DateTime.UtcNow, null, IsInactive: true),
				new PlayerHouse(51, 700200, 350000, DateTime.UtcNow, null, IsInactive: false),
			],
		};
		var housingTemplates = new HousingTemplateTable(
			[],
			[
				new HousingBuildingSummary(353000, "studio", HouseTypeId: 0),
				new HousingBuildingSummary(350000, "palace", HouseTypeId: 4),
			]);

		var plan = WorldNpcDropBoostRateContextPlanService.CreateDisabledPlan(
			looter,
			[1f],
			housingTemplates: housingTemplates);

		Assert.Equal(WorldNpcDropBoostRateContextPlanStatus.Blocked, plan.Status);
		Assert.True(plan.HasActivePalace);
		Assert.True(plan.HasActivePalaceSource);
		Assert.DoesNotContain("active palace source", plan.MissingInputs);
		Assert.NotNull(plan.Context);
		Assert.Equal(1.05f, plan.Context.CalculateBoostDropRate(), precision: 3);
	}

	[Fact]
	public void CreateDisabledPlan_ConsumesDropRatesFromGameServerRateOptions()
	{
		var looter = new Player
		{
			ObjectId = 1001,
			AccountMembership = 4,
		};
		var rateOptions = new GameServerRateOptions
		{
			DropRates = [1f, 2f, 3f],
		};

		var plan = WorldNpcDropBoostRateContextPlanService.CreateDisabledPlan(
			looter,
			rateOptions,
			hasActivePalaceSource: true);

		Assert.Equal(WorldNpcDropBoostRateContextPlanStatus.Blocked, plan.Status);
		Assert.Equal(3f, plan.ConfiguredDropRate);
		Assert.NotNull(plan.Context);
		Assert.Equal(3f, plan.Context.CalculateBoostDropRate(), precision: 3);
		Assert.DoesNotContain("RatesConfig.DROP_RATES", plan.MissingInputs);
		Assert.Contains("npc BOOST_DROP_RATE stat source", plan.MissingInputs);
	}

	private static WorldNpc CreateNpc(int level)
	{
		return new WorldNpc(
			5001,
			203001,
			new NpcTemplateSummary(203001, "drop_npc", 0, level, "NORMAL", "NORMAL", "NONE", "NONE", "GENERAL"),
			new WorldPosition(210010000, 1, 2, 3, 0));
	}
}
