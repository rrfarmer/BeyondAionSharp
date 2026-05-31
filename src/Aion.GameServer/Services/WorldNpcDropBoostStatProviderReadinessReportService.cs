using Aion.GameServer.Dataholders;

namespace Aion.GameServer.Services;

public static class WorldNpcDropBoostStatProviderReadinessReportService
{
	public static WorldNpcDropBoostStatProviderReadinessReport CreateReport(
		SkillTemplateTable? skillTemplates,
		bool hasLiveEffectStateProvider = false,
		bool hasLiveCreatureGameStatsProvider = false,
		int staticPreviewSkillLevel = 1,
		float boostDropRateBase = 100f,
		float drBoostBase = 100f)
	{
		var missingInputs = new List<string>();
		var staticEvaluationPreviews = new List<WorldNpcDropBoostStaticEvaluationPreview>();
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
					{
						dropBoostEffects++;
						staticEvaluationPreviews.Add(CreatePreview(
							template.SkillId,
							effect,
							"BOOST_DROP_RATE",
							staticPreviewSkillLevel,
							boostDropRateBase));
					}
					else if (string.Equals(effect.EffectName, "drboost", StringComparison.Ordinal))
					{
						drBoostEffects++;
						staticEvaluationPreviews.Add(CreatePreview(
							template.SkillId,
							effect,
							"DR_BOOST",
							staticPreviewSkillLevel,
							drBoostBase));
					}

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
			StaticEvaluationPreviews: staticEvaluationPreviews,
			MissingInputs: missingInputs,
			JavaSource: "DropRegistrationService.calculateBoostDropRate -> CreatureGameStats.getStat(BOOST_DROP_RATE/DR_BOOST); BufEffect.startEffect -> CreatureGameStats.addEffect");
	}

	private static WorldNpcDropBoostStaticEvaluationPreview CreatePreview(
		int skillId,
		SkillBuffStatEffectSummary effect,
		string statName,
		int skillLevel,
		float baseValue)
	{
		return new WorldNpcDropBoostStaticEvaluationPreview(
			skillId,
			effect.EffectName,
			statName,
			skillLevel,
			baseValue,
			SkillBuffStatChangeEvaluatorService.Evaluate(statName, baseValue, effect.Changes, skillLevel));
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
	IReadOnlyList<WorldNpcDropBoostStaticEvaluationPreview> StaticEvaluationPreviews,
	IReadOnlyList<string> MissingInputs,
	string JavaSource)
{
	public bool IsReadyForWorkflow => Status == WorldNpcDropBoostStatProviderReadinessStatus.Ready;

	public int StaticEvaluationPreviewCount => StaticEvaluationPreviews.Count;
}

public sealed record WorldNpcDropBoostStaticEvaluationPreview(
	int SkillId,
	string EffectName,
	string StatName,
	int SkillLevel,
	float BaseValue,
	SkillBuffStatChangeEvaluation Evaluation);
