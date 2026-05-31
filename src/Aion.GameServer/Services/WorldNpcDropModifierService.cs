using Aion.GameServer.Model.GameObjects;

namespace Aion.GameServer.Services;

public sealed class WorldNpcDropModifierService
{
	public WorldNpcDropModifiers CreateModifiers(
		IWorldNpcObject npc,
		Player looter,
		int? highestLevel = null,
		float boostDropRate = 1f)
	{
		// Java parity: services/drop/DropRegistrationService.createDropModifiers, narrowed until live stats/rates/house boosts are modeled.
		// Zone names stay empty until the future CM_SUBZONE_CHANGE / MapRegion revalidation model can mirror npc.isInsideZone(...).
		return new WorldNpcDropModifiers(
			looter.Race,
			boostDropRate,
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
