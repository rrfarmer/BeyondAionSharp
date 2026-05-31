using Aion.GameServer.Dataholders;
using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDropModifierService
{
	public WorldNpcDropModifiers CreateModifiers(
		IWorldNpcObject npc,
		Player looter,
		int? highestLevel = null,
		float boostDropRate = 1f,
		WorldNpcDropBoostRateContext? boostRateContext = null)
	{
		// Java parity: services/drop/DropRegistrationService.createDropModifiers, narrowed until live stats/rates/house boosts are modeled.
		// Zone names stay empty until the future CM_SUBZONE_CHANGE / MapRegion revalidation model can mirror npc.isInsideZone(...).
		var effectiveBoostDropRate = boostRateContext?.CalculateBoostDropRate() ?? boostDropRate;
		return new WorldNpcDropModifiers(
			looter.Race,
			effectiveBoostDropRate,
			GetReductionDropRate(npc.Template.Level, highestLevel ?? looter.Level),
			IsDropNpcChest: IsChest(npc));
	}

	public static float? GetReductionDropRate(int npcLevel, int highestLevel)
	{
		// Java parity: DropRegistrationService.getReductionDropRate uses DropRewardEnum.dropRewardFrom(npc.level - highestLevel).
		var dropChance = GetDropRewardPercent(npcLevel - highestLevel);
		return dropChance == 100 ? null : dropChance / 100f;
	}

	public static int GetDropRewardPercent(int levelDifference)
	{
		// Java parity: utils/stats/DropRewardEnum.dropRewardFrom.
		return levelDifference switch
		{
			<= -10 => 0,
			-9 => 40,
			-8 => 60,
			-7 => 70,
			-6 => 80,
			>= -5 => 100,
		};
	}

	public static float CalculateBoostDropRate(
		float configuredDropRate,
		int npcBoostDropRate = 100,
		int? killerBoostDropRate = null,
		int? killerDrBoost = null,
		bool hasReposeEnergy = false,
		bool hasSalvation = false,
		bool hasActivePalace = false)
	{
		// Java parity: DropRegistrationService.calculateBoostDropRate stat-default chain.
		var boostDropRate = killerBoostDropRate ?? npcBoostDropRate;
		boostDropRate = killerDrBoost ?? boostDropRate;
		if (hasReposeEnergy)
			boostDropRate += 5;
		if (hasSalvation)
			boostDropRate += 5;
		if (hasActivePalace)
			boostDropRate += 5;

		return configuredDropRate * boostDropRate / 100f;
	}

	private static bool IsChest(IWorldNpcObject npc)
	{
		// Java parity: services/drop/DropRegistrationService.createDropModifiers chest AI slice, narrowed until group-drop template names exist.
		return string.Equals(npc.AiName, "chest", StringComparison.OrdinalIgnoreCase);
	}
}

public sealed record WorldNpcDropBoostRateContext(
	float ConfiguredDropRate,
	int NpcBoostDropRate = 100,
	int? KillerBoostDropRate = null,
	int? KillerDrBoost = null,
	bool HasReposeEnergy = false,
	bool HasSalvation = false,
	bool HasActivePalace = false)
{
	public float CalculateBoostDropRate()
	{
		return WorldNpcDropModifierService.CalculateBoostDropRate(
			ConfiguredDropRate,
			NpcBoostDropRate,
			KillerBoostDropRate,
			KillerDrBoost,
			HasReposeEnergy,
			HasSalvation,
			HasActivePalace);
	}
}

public static class WorldNpcDropBoostRateContextPlanService
{
	public static WorldNpcDropBoostRateContextPlan CreateDisabledPlan(
		Player? looter,
		IReadOnlyList<float>? configuredDropRates,
		bool hasNpcBoostStatSource = false,
		bool hasKillerBoostStatSource = false,
		bool hasKillerDrBoostStatSource = false,
		bool hasSalvationSource = false,
		bool hasActivePalaceSource = false,
		HousingTemplateTable? housingTemplates = null)
	{
		// Java parity: DropRegistrationService.calculateBoostDropRate reads live
		// npc/player stats, RatesConfig.DROP_RATES, repose, salvation, and active house.
		var hasActivePalace = PlayerActiveHouseResolverService.HasActivePalace(looter, housingTemplates);
		var hasResolvedActivePalaceSource = hasActivePalaceSource || hasActivePalace.HasValue;
		var missingInputs = new List<string>();
		if (looter == null)
			missingInputs.Add("looter");
		if (configuredDropRates is not { Count: > 0 })
			missingInputs.Add("RatesConfig.DROP_RATES");
		if (!hasNpcBoostStatSource)
			missingInputs.Add("npc BOOST_DROP_RATE stat source");
		if (!hasKillerBoostStatSource)
			missingInputs.Add("killer BOOST_DROP_RATE stat source");
		if (!hasKillerDrBoostStatSource)
			missingInputs.Add("killer DR_BOOST stat source");
		if (!hasSalvationSource)
			missingInputs.Add("killer salvation percent source");
		if (!hasResolvedActivePalaceSource)
			missingInputs.Add("active palace source");

		var configuredDropRate = looter != null && configuredDropRates is { Count: > 0 }
			? SelectMembershipRate(looter.AccountMembership, configuredDropRates)
			: 1f;
		var context = looter != null && configuredDropRates is { Count: > 0 }
			? new WorldNpcDropBoostRateContext(
				configuredDropRate,
				HasReposeEnergy: looter.ReposeEnergy > 0,
				HasActivePalace: hasActivePalace == true)
			: null;

		return new WorldNpcDropBoostRateContextPlan(
			missingInputs.Count == 0
				? WorldNpcDropBoostRateContextPlanStatus.Ready
				: WorldNpcDropBoostRateContextPlanStatus.Blocked,
			context,
			configuredDropRate,
			looter?.ReposeEnergy > 0,
			hasActivePalace,
			hasNpcBoostStatSource,
			hasKillerBoostStatSource,
			hasKillerDrBoostStatSource,
			hasSalvationSource,
			hasResolvedActivePalaceSource,
			missingInputs,
			"DropRegistrationService.calculateBoostDropRate -> Rates.get(killer, RatesConfig.DROP_RATES), npc BOOST_DROP_RATE, killer BOOST_DROP_RATE, killer DR_BOOST, repose, salvation, active palace");
	}

	private static float SelectMembershipRate(byte membershipLevel, IReadOnlyList<float> rates)
	{
		return rates[Math.Min(rates.Count - 1, membershipLevel)];
	}
}

public static class PlayerActiveHouseResolverService
{
	public static PlayerHouse? FindActiveHouse(Player? player)
	{
		// Java parity: HousingService.findActiveHouse returns the loaded studio first;
		// C# login loading already keeps the studio/oldest active house as the first non-inactive entry.
		return player?.Houses.FirstOrDefault(house => !house.IsInactive);
	}

	public static bool? HasActivePalace(Player? player, HousingTemplateTable? housingTemplates)
	{
		if (player == null || housingTemplates == null)
			return null;

		var activeHouse = FindActiveHouse(player);
		return activeHouse != null && housingTemplates.IsPalaceBuilding(activeHouse.BuildingId);
	}
}

public sealed record WorldNpcDropBoostRateContextPlan(
	WorldNpcDropBoostRateContextPlanStatus Status,
	WorldNpcDropBoostRateContext? Context,
	float ConfiguredDropRate,
	bool? HasReposeEnergy,
	bool? HasActivePalace,
	bool HasNpcBoostStatSource,
	bool HasKillerBoostStatSource,
	bool HasKillerDrBoostStatSource,
	bool HasSalvationSource,
	bool HasActivePalaceSource,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForWorkflow => Status == WorldNpcDropBoostRateContextPlanStatus.Ready;
}

public enum WorldNpcDropBoostRateContextPlanStatus
{
	Blocked,
	Ready,
}
