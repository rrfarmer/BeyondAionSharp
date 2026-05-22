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
		return new WorldNpcDropModifiers(
			looter.Race,
			boostDropRate,
			GetReductionDropRate(npc.Template.Level, highestLevel ?? looter.Level));
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
}
