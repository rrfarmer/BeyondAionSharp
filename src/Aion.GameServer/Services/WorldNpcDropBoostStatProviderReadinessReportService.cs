using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class WorldNpcDropBoostStatProviderReadinessReportService
{
	public static WorldNpcDropBoostStatProviderReadinessReport CreateReport(
		SkillTemplateTable? skillTemplates,
		bool hasLiveEffectStateProvider = false,
		bool hasLiveCreatureGameStatsProvider = false)
	{
		var missingInputs = new List<string>();
		var dropBoostEffects = 0;
		var drBoostEffects = 0;
		var boostDropRateChanges = 0;
		var drBoostChanges = 0;

		if (skillTemplates == null)
		{
			missingInputs.Add("skill_templates");
		}
		else
		{
			foreach (var template in skillTemplates.Templates)
			{
				foreach (var effect in template.BuffStatEffects)
				{
					if (string.Equals(effect.EffectName, "boostdroprate", StringComparison.Ordinal))
						dropBoostEffects++;
					else if (string.Equals(effect.EffectName, "drboost", StringComparison.Ordinal))
						drBoostEffects++;

					foreach (var change in effect.Changes)
					{
						if (string.Equals(change.Stat, "BOOST_DROP_RATE", StringComparison.Ordinal))
							boostDropRateChanges++;
						else if (string.Equals(change.Stat, "DR_BOOST", StringComparison.Ordinal))
							drBoostChanges++;
					}
				}
			}

			if (dropBoostEffects == 0 || boostDropRateChanges == 0)
				missingInputs.Add("static boostdroprate BOOST_DROP_RATE metadata");
			if (drBoostEffects == 0 || drBoostChanges == 0)
				missingInputs.Add("static drboost DR_BOOST metadata");
		}

		if (!hasLiveEffectStateProvider)
			missingInputs.Add("live effect state provider");
		if (!hasLiveCreatureGameStatsProvider)
			missingInputs.Add("live CreatureGameStats provider");

		var status = DetermineStatus(skillTemplates, dropBoostEffects, drBoostEffects, boostDropRateChanges, drBoostChanges, hasLiveEffectStateProvider, hasLiveCreatureGameStatsProvider);
		return new WorldNpcDropBoostStatProviderReadinessReport(
			status,
			dropBoostEffects,
			drBoostEffects,
			boostDropRateChanges,
			drBoostChanges,
			HasLiveEffectStateProvider: hasLiveEffectStateProvider,
			HasLiveCreatureGameStatsProvider: hasLiveCreatureGameStatsProvider,
			missingInputs,
			"DropRegistrationService.calculateBoostDropRate -> CreatureGameStats.getStat(BOOST_DROP_RATE/DR_BOOST); BufEffect.startEffect -> CreatureGameStats.addEffect");
	}

	private static WorldNpcDropBoostStatProviderReadinessStatus DetermineStatus(
		SkillTemplateTable? skillTemplates,
		int dropBoostEffects,
		int drBoostEffects,
		int boostDropRateChanges,
		int drBoostChanges,
		bool hasLiveEffectStateProvider,
		bool hasLiveCreatureGameStatsProvider)
	{
		if (skillTemplates == null)
			return WorldNpcDropBoostStatProviderReadinessStatus.MissingSkillTemplates;
		if (dropBoostEffects == 0 || boostDropRateChanges == 0 || drBoostEffects == 0 || drBoostChanges == 0)
			return WorldNpcDropBoostStatProviderReadinessStatus.MissingStaticMetadata;
		if (!hasLiveEffectStateProvider)
			return WorldNpcDropBoostStatProviderReadinessStatus.BlockedMissingLiveEffectStateProvider;
		if (!hasLiveCreatureGameStatsProvider)
			return WorldNpcDropBoostStatProviderReadinessStatus.BlockedMissingLiveCreatureGameStatsProvider;
		return WorldNpcDropBoostStatProviderReadinessStatus.Ready;
	}
}

public enum WorldNpcDropBoostStatProviderReadinessStatus
{
	MissingSkillTemplates,
	MissingStaticMetadata,
	BlockedMissingLiveEffectStateProvider,
	BlockedMissingLiveCreatureGameStatsProvider,
	Ready,
}

public sealed record WorldNpcDropBoostStatProviderReadinessReport(
	WorldNpcDropBoostStatProviderReadinessStatus Status,
	int DropBoostEffectCount,
	int DrBoostEffectCount,
	int BoostDropRateChangeCount,
	int DrBoostChangeCount,
	bool HasLiveEffectStateProvider,
	bool HasLiveCreatureGameStatsProvider,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForWorkflow => Status == WorldNpcDropBoostStatProviderReadinessStatus.Ready;
}
